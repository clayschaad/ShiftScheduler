using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;
using Microsoft.Extensions.Configuration;
using ShiftScheduler.Shared;
using System.Net.Http.Headers;
using System.Text;
using System.Xml.Linq;

namespace ShiftScheduler.Services;

public class NextcloudCalendarService(
    IConfiguration configuration,
    IHttpClientFactory httpClientFactory,
    IConfigurationService configurationService) : INextcloudCalendarService
{
    private readonly string _baseUrl = configuration["Nextcloud:BaseUrl"]
        ?? throw new InvalidOperationException("Nextcloud:BaseUrl not configured");
    private readonly string _username = configuration["Nextcloud:Username"]
        ?? throw new InvalidOperationException("Nextcloud:Username not configured");
    private readonly string _appPassword = configuration["Nextcloud:AppPassword"]
        ?? throw new InvalidOperationException("Nextcloud:AppPassword not configured");

    private static readonly XNamespace DavNs = "DAV:";
    private static readonly XNamespace CalDavNs = "urn:ietf:params:xml:ns:caldav";

    public async Task<List<CalendarInfo>> GetCalendarsAsync()
    {
        var path = $"/remote.php/dav/calendars/{_username}/";
        var body = """
            <?xml version="1.0" encoding="utf-8" ?>
            <D:propfind xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
              <D:prop>
                <D:displayname/>
                <D:resourcetype/>
              </D:prop>
            </D:propfind>
            """;

        var request = CreateRequest(HttpMethod.Parse("PROPFIND"), path);
        request.Headers.Add("Depth", "1");
        request.Content = new StringContent(body, Encoding.UTF8, "application/xml");

        var client = httpClientFactory.CreateClient();
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var xml = await response.Content.ReadAsStringAsync();
        return ParseCalendarsFromPropfind(xml);
    }

    public async Task SyncShiftsToCalendarAsync(string calendarId, List<ShiftWithTransport> shifts)
    {
        if (shifts.Count == 0) return;

        var minDate = shifts.Min(s => s.Date).Date;
        var maxDate = shifts.Max(s => s.Date).Date.AddDays(1);

        await DeleteExistingShiftEventsAsync(calendarId, minDate, maxDate);
        await Task.Delay(500);
        await CreateShiftEventsAsync(calendarId, shifts);
    }

    private List<CalendarInfo> ParseCalendarsFromPropfind(string xml)
    {
        var doc = XDocument.Parse(xml);
        var calendars = new List<CalendarInfo>();

        foreach (var responseElement in doc.Descendants(DavNs + "response"))
        {
            var resourceType = responseElement.Descendants(DavNs + "resourcetype").FirstOrDefault();
            if (resourceType?.Element(CalDavNs + "calendar") == null)
                continue;

            var href = responseElement.Element(DavNs + "href")?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(href)) continue;

            var displayName = responseElement.Descendants(DavNs + "displayname").FirstOrDefault()?.Value ?? href;

            calendars.Add(new CalendarInfo { Id = href, Summary = displayName });
        }

        return calendars;
    }

    private async Task DeleteExistingShiftEventsAsync(string calendarId, DateTime startDate, DateTime endDate)
    {
        var startUtc = startDate.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
        var endUtc = endDate.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");

        var body = $"""
            <?xml version="1.0" encoding="utf-8" ?>
            <C:calendar-query xmlns:D="DAV:" xmlns:C="urn:ietf:params:xml:ns:caldav">
              <D:prop>
                <D:getetag/>
                <C:calendar-data/>
              </D:prop>
              <C:filter>
                <C:comp-filter name="VCALENDAR">
                  <C:comp-filter name="VEVENT">
                    <C:time-range start="{startUtc}" end="{endUtc}"/>
                  </C:comp-filter>
                </C:comp-filter>
              </C:filter>
            </C:calendar-query>
            """;

        var reportRequest = CreateRequest(HttpMethod.Parse("REPORT"), calendarId);
        reportRequest.Headers.Add("Depth", "1");
        reportRequest.Content = new StringContent(body, Encoding.UTF8, "application/xml");

        var client = httpClientFactory.CreateClient();
        var reportResponse = await client.SendAsync(reportRequest);
        reportResponse.EnsureSuccessStatusCode();

        var xml = await reportResponse.Content.ReadAsStringAsync();
        var eventUrls = ParseShiftEventUrls(xml);

        foreach (var url in eventUrls)
        {
            var deleteRequest = CreateRequest(HttpMethod.Delete, url);
            var deleteResponse = await client.SendAsync(deleteRequest);

            if (!deleteResponse.IsSuccessStatusCode &&
                deleteResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                deleteResponse.EnsureSuccessStatusCode();

            if (eventUrls.Count > 1)
                await Task.Delay(200);
        }
    }

    private List<string> ParseShiftEventUrls(string xml)
    {
        var doc = XDocument.Parse(xml);
        var urls = new List<string>();

        foreach (var responseElement in doc.Descendants(DavNs + "response"))
        {
            var calendarData = responseElement
                .Descendants(CalDavNs + "calendar-data")
                .FirstOrDefault()?.Value;

            if (calendarData == null || !calendarData.Contains("X-SHIFT-SCHEDULER:TRUE"))
                continue;

            var href = responseElement.Element(DavNs + "href")?.Value;
            if (!string.IsNullOrEmpty(href))
                urls.Add(href);
        }

        return urls;
    }

    private async Task CreateShiftEventsAsync(string calendarId, List<ShiftWithTransport> shifts)
    {
        var client = httpClientFactory.CreateClient();

        foreach (var shiftWithTransport in shifts)
        {
            var shift = shiftWithTransport.Shift;
            var date = shiftWithTransport.Date;

            if (string.IsNullOrEmpty(shift.MorningTime) && string.IsNullOrEmpty(shift.AfternoonTime))
                continue;

            if (!string.IsNullOrEmpty(shift.MorningTime))
            {
                var (startTime, endTime) = configurationService.GetZurichTime(
                    DateOnly.FromDateTime(date), shift.MorningTime);
                var uid = $"shift-{SanitizeForUid(shift.Name)}-{date:yyyyMMdd}-morning";
                var eventData = CalendarEventBuilder.BuildEventContent(shift, shiftWithTransport.MorningTransport);
                var icsContent = BuildIcsContent(uid, eventData, startTime, endTime);

                var putRequest = CreateRequest(HttpMethod.Put, $"{calendarId}{Uri.EscapeDataString(uid)}.ics");
                putRequest.Content = new StringContent(icsContent, Encoding.UTF8, "text/calendar");
                (await client.SendAsync(putRequest)).EnsureSuccessStatusCode();
            }

            if (!string.IsNullOrEmpty(shift.AfternoonTime))
            {
                var (startTime, endTime) = configurationService.GetZurichTime(
                    DateOnly.FromDateTime(date), shift.AfternoonTime);
                var uid = $"shift-{SanitizeForUid(shift.Name)}-{date:yyyyMMdd}-afternoon";
                var eventData = CalendarEventBuilder.BuildEventContent(shift, shiftWithTransport.AfternoonTransport);
                var icsContent = BuildIcsContent(uid, eventData, startTime, endTime);

                var putRequest = CreateRequest(HttpMethod.Put, $"{calendarId}{Uri.EscapeDataString(uid)}.ics");
                putRequest.Content = new StringContent(icsContent, Encoding.UTF8, "text/calendar");
                (await client.SendAsync(putRequest)).EnsureSuccessStatusCode();
            }

            if (shifts.Count > 1)
                await Task.Delay(300);
        }
    }

    private static string BuildIcsContent(string uid, CalendarEventData eventData, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var calendar = new Calendar();
        var vevent = new CalendarEvent
        {
            Uid = uid,
            Summary = eventData.Summary,
            Description = eventData.Description,
            DtStart = new CalDateTime(startTime.UtcDateTime, hasTime: true),
            DtEnd = new CalDateTime(endTime.UtcDateTime, hasTime: true)
        };
        vevent.Properties.Add(new Ical.Net.CalendarProperty("X-SHIFT-SCHEDULER", "TRUE"));
        calendar.Events.Add(vevent);
        return new CalendarSerializer().SerializeToString(calendar);
    }

    private static string SanitizeForUid(string name)
    {
        return new string(name.Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray());
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path)
    {
        // path may be a URL-path from a CalDAV href ("/remote.php/...") or a full URL
        var uri = path.StartsWith("http", StringComparison.OrdinalIgnoreCase)
            ? new Uri(path)
            : new Uri($"{_baseUrl.TrimEnd('/')}{path}");

        var request = new HttpRequestMessage(method, uri);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_username}:{_appPassword}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        return request;
    }
}
