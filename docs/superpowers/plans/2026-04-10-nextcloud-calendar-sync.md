# Nextcloud Calendar Sync Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Nextcloud CalDAV as a second calendar sync target alongside Google Calendar, with shared base logic extracted into a common interface and static helper.

**Architecture:** A new `ICalendarService` interface unifies both providers. A static `CalendarEventBuilder` holds shared pure logic (event content building, transport formatting, retry). `GoogleCalendarService` and `NextcloudCalendarService` each implement a marker interface extending `ICalendarService`. The controller routes to the right service based on a `provider` query/body parameter.

**Tech Stack:** .NET 9, ASP.NET Core, Blazor WASM, Google.Apis.Calendar.v3 (existing), Ical.Net (existing), CalDAV over HttpClient, System.Xml.Linq for CalDAV XML parsing.

**Design spec:** `docs/superpowers/specs/2026-04-10-nextcloud-calendar-sync-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|----------------|
| Create | `Shared/CalendarInfo.cs` | Shared DTO replacing `GoogleCalendar` |
| Modify | `Shared/GoogleCalendar.cs` | Empty out (class renamed to `CalendarInfo`) |
| Create | `Services/ICalendarService.cs` | Shared interface + marker interfaces |
| Create | `Services/CalendarEventBuilder.cs` | Pure helpers: event content, transport format, retry |
| Modify | `Services/GoogleCalendarService.cs` | Implement `IGoogleCalendarService`, use `CalendarEventBuilder` |
| Create | `Services/NextcloudCalendarService.cs` | CalDAV implementation of `INextcloudCalendarService` |
| Modify | `Server/appsettings.json` | Add `Nextcloud` config section with dummy values |
| Modify | `Server/Program.cs` | Register `NextcloudCalendarService`, `IHttpClientFactory` already present |
| Modify | `Server/Controllers/ShiftController.cs` | New provider-agnostic endpoints |
| Modify | `Client/Pages/Index.razor.cs` | Nextcloud sync flow, rename `GoogleCalendar` → `CalendarInfo` |
| Modify | `Client/Pages/Index.razor` | Add Nextcloud button, dynamic modal title |
| Modify | `DOCKER_CONFIG.md` | Document Nextcloud config section |
| Create | `ShiftScheduler.Services.Tests/CalendarEventBuilderTests.cs` | Unit tests for `CalendarEventBuilder` |

---

## Task 1: Add `CalendarInfo` shared DTO

**Files:**
- Create: `Shared/CalendarInfo.cs`
- Modify: `Shared/GoogleCalendar.cs`

- [ ] **Step 1: Create `Shared/CalendarInfo.cs`**

```csharp
namespace ShiftScheduler.Shared;

public class CalendarInfo
{
    public string Id { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}
```

- [ ] **Step 2: Empty out `Shared/GoogleCalendar.cs`**

Replace entire file content with:

```csharp
// Class renamed to CalendarInfo — see Shared/CalendarInfo.cs
namespace ShiftScheduler.Shared;
```

- [ ] **Step 3: Verify build**

```bash
cd /home/schaad/Documents/ShiftScheduler && dotnet build
```

Expected: Build succeeds (no code uses `GoogleCalendar` yet — references are updated in later tasks).

- [ ] **Step 4: Commit**

```bash
git add Shared/CalendarInfo.cs Shared/GoogleCalendar.cs
git commit -m "feat: add CalendarInfo shared DTO, retire GoogleCalendar"
```

---

## Task 2: Create `ICalendarService` interface

**Files:**
- Create: `Services/ICalendarService.cs`

- [ ] **Step 1: Create `Services/ICalendarService.cs`**

```csharp
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services;

public interface ICalendarService
{
    Task<List<CalendarInfo>> GetCalendarsAsync();
    Task SyncShiftsToCalendarAsync(string calendarId, List<ShiftWithTransport> shifts);
}

/// <summary>Marker interface for DI disambiguation.</summary>
public interface IGoogleCalendarService : ICalendarService { }

/// <summary>Marker interface for DI disambiguation.</summary>
public interface INextcloudCalendarService : ICalendarService { }
```

- [ ] **Step 2: Verify build**

```bash
cd /home/schaad/Documents/ShiftScheduler && dotnet build
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Services/ICalendarService.cs
git commit -m "feat: add ICalendarService interface and marker interfaces"
```

---

## Task 3: Create `CalendarEventBuilder` with tests

**Files:**
- Create: `ShiftScheduler.Services.Tests/CalendarEventBuilderTests.cs`
- Create: `Services/CalendarEventBuilder.cs`

- [ ] **Step 1: Write failing tests**

Create `ShiftScheduler.Services.Tests/CalendarEventBuilderTests.cs`:

```csharp
using ShiftScheduler.Services;
using ShiftScheduler.Shared;
using Shouldly;

namespace ShiftScheduler.Services.Tests;

public class CalendarEventBuilderTests
{
    [Fact]
    public void BuildEventContent_WithTransport_IncludesTransportInDescription()
    {
        var shift = new Shift { Name = "Früh" };
        var transport = new TransportConnection
        {
            Platform = "Gleis 3",
            DepartureTime = DateTimeOffset.Parse("2026-04-01T05:30:00+02:00"),
            ArrivalTime = DateTimeOffset.Parse("2026-04-01T05:55:00+02:00"),
            Duration = TimeSpan.FromMinutes(25)
        };

        var result = CalendarEventBuilder.BuildEventContent(shift, transport);

        result.Summary.ShouldBe("Früh");
        result.Description.ShouldBe("Transport: Gleis 3: 05:30 → 05:55 (25 Minutes)");
    }

    [Fact]
    public void BuildEventContent_WithoutTransport_EmptyDescription()
    {
        var shift = new Shift { Name = "Spät" };

        var result = CalendarEventBuilder.BuildEventContent(shift, null);

        result.Summary.ShouldBe("Spät");
        result.Description.ShouldBeEmpty();
    }

    [Fact]
    public void FormatTransportInfo_FormatsCorrectly()
    {
        var transport = new TransportConnection
        {
            Platform = "Gleis 3",
            DepartureTime = DateTimeOffset.Parse("2026-04-01T05:30:00+02:00"),
            ArrivalTime = DateTimeOffset.Parse("2026-04-01T05:55:00+02:00"),
            Duration = TimeSpan.FromMinutes(25)
        };

        var result = CalendarEventBuilder.FormatTransportInfo(transport);

        result.ShouldBe("Gleis 3: 05:30 → 05:55 (25 Minutes)");
    }

    [Fact]
    public void FormatTransportInfo_WithNullPlatform_UsesEmptyString()
    {
        var transport = new TransportConnection
        {
            Platform = null,
            DepartureTime = DateTimeOffset.Parse("2026-04-01T05:30:00+02:00"),
            ArrivalTime = DateTimeOffset.Parse("2026-04-01T05:55:00+02:00"),
            Duration = TimeSpan.FromMinutes(25)
        };

        var result = CalendarEventBuilder.FormatTransportInfo(transport);

        result.ShouldBe(": 05:30 → 05:55 (25 Minutes)");
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```bash
cd /home/schaad/Documents/ShiftScheduler && dotnet test ShiftScheduler.Services.Tests/ --filter "CalendarEventBuilderTests" -v minimal
```

Expected: Build error — `CalendarEventBuilder` does not exist yet.

- [ ] **Step 3: Create `Services/CalendarEventBuilder.cs`**

```csharp
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services;

public record CalendarEventData(string Summary, string Description);

public static class CalendarEventBuilder
{
    public static CalendarEventData BuildEventContent(Shift shift, TransportConnection? transport)
    {
        var description = transport != null
            ? $"Transport: {FormatTransportInfo(transport)}"
            : string.Empty;
        return new CalendarEventData(shift.Name, description);
    }

    public static string FormatTransportInfo(TransportConnection transport)
    {
        var departure = transport.DepartureTime.ToString("HH:mm");
        var arrival = transport.ArrivalTime.ToString("HH:mm");
        return $"{transport.Platform}: {departure} → {arrival} ({transport.Duration.TotalMinutes} Minutes)";
    }

    public static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
    {
        var delay = TimeSpan.FromSeconds(1);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429") && attempt < maxRetries)
            {
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            catch (Exception ex) when (ex.Message.Contains("Rate Limit") && attempt < maxRetries)
            {
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
            catch (Exception ex) when (ex.Message.Contains("Forbidden") && ex.Message.Contains("quota") && attempt < maxRetries)
            {
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2);
            }
        }

        return await operation();
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```bash
cd /home/schaad/Documents/ShiftScheduler && dotnet test ShiftScheduler.Services.Tests/ --filter "CalendarEventBuilderTests" -v minimal
```

Expected: 4 tests pass.

- [ ] **Step 5: Commit**

```bash
git add Services/CalendarEventBuilder.cs ShiftScheduler.Services.Tests/CalendarEventBuilderTests.cs
git commit -m "feat: add CalendarEventBuilder with shared event/transport logic"
```

---

## Task 4: Refactor `GoogleCalendarService`

**Files:**
- Modify: `Services/GoogleCalendarService.cs`

The refactoring:
1. Change implemented interface from `IGoogleCalendarService` (old, deleted) to the new `IGoogleCalendarService : ICalendarService`
2. Change `GetCalendarsAsync()` return type from `List<CalendarListEntry>` to `List<CalendarInfo>`
3. Replace `FormatTransportInfo` and the summary/description building with `CalendarEventBuilder`
4. Replace the private `ExecuteWithRetryAsync` with `CalendarEventBuilder.ExecuteWithRetryAsync`

- [ ] **Step 1: Replace `Services/GoogleCalendarService.cs`**

```csharp
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services;

public class GoogleCalendarService(IHttpContextAccessor httpContextAccessor, IConfigurationService configurationService)
    : IGoogleCalendarService
{
    public async Task<List<CalendarInfo>> GetCalendarsAsync()
    {
        var service = await CreateCalendarServiceAsync();
        var request = service.CalendarList.List();
        var response = await CalendarEventBuilder.ExecuteWithRetryAsync(async () => await request.ExecuteAsync());

        return response.Items?
            .Where(c => c.AccessRole == "owner" || c.AccessRole == "writer")
            .Select(c => new CalendarInfo { Id = c.Id, Summary = c.Summary })
            .ToList() ?? [];
    }

    public async Task SyncShiftsToCalendarAsync(string calendarId, List<ShiftWithTransport> shifts)
    {
        var service = await CreateCalendarServiceAsync();

        if (shifts.Count == 0) return;

        var minDate = shifts.Min(s => s.Date).Date;
        var maxDate = shifts.Max(s => s.Date).Date.AddDays(1);

        await DeleteExistingShiftEventsAsync(service, calendarId, minDate, maxDate);
        await Task.Delay(500);
        await CreateShiftEventsWithRateLimitingAsync(service, calendarId, shifts);
    }

    private async Task DeleteExistingShiftEventsAsync(CalendarService service, string calendarId, DateTime startDate, DateTime endDate)
    {
        var request = service.Events.List(calendarId);
        request.TimeMinDateTimeOffset = startDate;
        request.TimeMaxDateTimeOffset = endDate;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;

        var response = await CalendarEventBuilder.ExecuteWithRetryAsync(async () => await request.ExecuteAsync());

        if (response.Items != null)
        {
            var shiftsToDelete = response.Items
                .Where(e => e.ExtendedProperties?.Private__?.ContainsKey("shiftSchedulerEvent") == true)
                .ToList();

            foreach (var eventToDelete in shiftsToDelete)
            {
                await CalendarEventBuilder.ExecuteWithRetryAsync(async () =>
                {
                    await service.Events.Delete(calendarId, eventToDelete.Id).ExecuteAsync();
                    return true;
                });

                if (shiftsToDelete.Count > 1)
                    await Task.Delay(200);
            }
        }
    }

    private async Task CreateShiftEventsWithRateLimitingAsync(CalendarService service, string calendarId, List<ShiftWithTransport> shifts)
    {
        foreach (var shiftWithTransport in shifts)
        {
            await CreateShiftEventsAsync(service, calendarId, shiftWithTransport);

            if (shifts.Count > 1)
                await Task.Delay(300);
        }
    }

    private async Task CreateShiftEventsAsync(CalendarService service, string calendarId, ShiftWithTransport shiftWithTransport)
    {
        var shift = shiftWithTransport.Shift;
        var date = shiftWithTransport.Date;

        if (string.IsNullOrEmpty(shift.MorningTime) && string.IsNullOrEmpty(shift.AfternoonTime))
            return;

        if (!string.IsNullOrEmpty(shift.MorningTime))
        {
            var (startTime, endTime) = configurationService.GetZurichTime(DateOnly.FromDateTime(date), shift.MorningTime);
            var eventData = CalendarEventBuilder.BuildEventContent(shift, shiftWithTransport.MorningTransport);
            var googleEvent = CreateGoogleEvent(eventData, startTime, endTime);
            await CalendarEventBuilder.ExecuteWithRetryAsync(async () => await service.Events.Insert(googleEvent, calendarId).ExecuteAsync());
        }

        if (!string.IsNullOrEmpty(shift.AfternoonTime))
        {
            var (startTime, endTime) = configurationService.GetZurichTime(DateOnly.FromDateTime(date), shift.AfternoonTime);
            var eventData = CalendarEventBuilder.BuildEventContent(shift, shiftWithTransport.AfternoonTransport);
            var googleEvent = CreateGoogleEvent(eventData, startTime, endTime);
            await CalendarEventBuilder.ExecuteWithRetryAsync(async () => await service.Events.Insert(googleEvent, calendarId).ExecuteAsync());
        }
    }

    private static Event CreateGoogleEvent(CalendarEventData eventData, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        return new Event
        {
            Summary = eventData.Summary,
            Description = eventData.Description,
            Start = new EventDateTime { DateTimeDateTimeOffset = startTime },
            End = new EventDateTime { DateTimeDateTimeOffset = endTime },
            ExtendedProperties = new Event.ExtendedPropertiesData
            {
                Private__ = new Dictionary<string, string>
                {
                    ["shiftSchedulerEvent"] = "true",
                    ["shiftName"] = eventData.Summary
                }
            }
        };
    }

    private async Task<CalendarService> CreateCalendarServiceAsync()
    {
        var httpContext = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No HTTP context available");

        var accessToken = await httpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(accessToken))
            throw new UnauthorizedAccessException("No access token available");

        var credential = GoogleCredential.FromAccessToken(accessToken);

        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ShiftScheduler"
        });
    }
}
```

- [ ] **Step 2: Build and run all tests**

```bash
cd /home/schaad/Documents/ShiftScheduler && dotnet build && dotnet test ShiftScheduler.Services.Tests/ -v minimal
```

Expected: Build succeeds, all tests pass.

- [ ] **Step 3: Commit**

```bash
git add Services/GoogleCalendarService.cs
git commit -m "refactor: GoogleCalendarService implements ICalendarService, uses CalendarEventBuilder"
```

---

## Task 5: Add Nextcloud configuration

**Files:**
- Modify: `Server/appsettings.json`
- Modify: `DOCKER_CONFIG.md`

- [ ] **Step 1: Add Nextcloud section to `Server/appsettings.json`**

Add after the `"Authentication"` block:

```json
"Nextcloud": {
  "BaseUrl": "https://your-nextcloud-instance.example.com",
  "Username": "YOUR_NEXTCLOUD_USERNAME",
  "AppPassword": "YOUR_NEXTCLOUD_APP_PASSWORD"
},
```

The full `Authentication` + `Nextcloud` section in `appsettings.json` should look like:

```json
"Authentication": {
  "Google": {
    "ClientId": "YOUR_GOOGLE_CLIENT_ID",
    "ClientSecret": "YOUR_GOOGLE_CLIENT_SECRET"
  },
  "AuthorizedEmails": [
    "example1@gmail.com",
    "example2@gmail.com"
  ]
},

"Nextcloud": {
  "BaseUrl": "https://your-nextcloud-instance.example.com",
  "Username": "YOUR_NEXTCLOUD_USERNAME",
  "AppPassword": "YOUR_NEXTCLOUD_APP_PASSWORD"
},
```

- [ ] **Step 2: Update `DOCKER_CONFIG.md`**

Add a new section at the end of the file:

```markdown
## Nextcloud Calendar Sync

To enable Nextcloud CalDAV sync, populate the `Nextcloud` section in the production `appsettings.json` (not committed to git):

```json
"Nextcloud": {
  "BaseUrl": "https://your-nextcloud-instance.example.com",
  "Username": "your-nextcloud-username",
  "AppPassword": "your-nextcloud-app-password"
}
```

Generate an app password in Nextcloud: **Settings → Security → Devices & sessions → Create new app password**.
```

- [ ] **Step 3: Commit**

```bash
git add Server/appsettings.json DOCKER_CONFIG.md
git commit -m "feat: add Nextcloud config placeholder and Docker docs"
```

---

## Task 6: Implement `NextcloudCalendarService`

**Files:**
- Create: `Services/NextcloudCalendarService.cs`

- [ ] **Step 1: Create `Services/NextcloudCalendarService.cs`**

```csharp
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
            DtStart = new CalDateTime(startTime.UtcDateTime) { IsUniversalTime = true },
            DtEnd = new CalDateTime(endTime.UtcDateTime) { IsUniversalTime = true }
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
```

- [ ] **Step 2: Build**

```bash
cd /home/schaad/Documents/ShiftScheduler && dotnet build
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add Services/NextcloudCalendarService.cs
git commit -m "feat: implement NextcloudCalendarService with CalDAV"
```

---

## Task 7: Update `Program.cs` and `ShiftController`

**Files:**
- Modify: `Server/Program.cs`
- Modify: `Server/Controllers/ShiftController.cs`

- [ ] **Step 1: Update DI registration in `Server/Program.cs`**

Replace the line:
```csharp
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();
```

With:
```csharp
builder.Services.AddScoped<IGoogleCalendarService, GoogleCalendarService>();
builder.Services.AddScoped<INextcloudCalendarService, NextcloudCalendarService>();
```

Note: `IHttpClientFactory` is already registered by the existing `builder.Services.AddHttpClient<TransportApiService>();` call — no additional registration needed.

- [ ] **Step 2: Update `Server/Controllers/ShiftController.cs`**

Replace the full file with:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftScheduler.Services;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "AllowedEmails")]
    public class ShiftController : ControllerBase
    {
        private readonly IcsExportService _icsService;
        private readonly PdfExportService _pdfExportService;
        private readonly ITransportService _transportService;
        private readonly IConfigurationService _configurationService;
        private readonly IGoogleCalendarService _googleCalendarService;
        private readonly INextcloudCalendarService _nextcloudCalendarService;

        public ShiftController(
            IcsExportService icsService,
            PdfExportService pdfExportService,
            ITransportService transportService,
            IConfigurationService configurationService,
            IGoogleCalendarService googleCalendarService,
            INextcloudCalendarService nextcloudCalendarService)
        {
            _icsService = icsService;
            _pdfExportService = pdfExportService;
            _transportService = transportService;
            _configurationService = configurationService;
            _googleCalendarService = googleCalendarService;
            _nextcloudCalendarService = nextcloudCalendarService;
        }

        [HttpGet("shifts")]
        public IActionResult GetShifts()
        {
            return Ok(_configurationService.GetShifts());
        }

        [HttpPost("shift_transport")]
        public async Task<IActionResult> GetShiftTransport([FromBody] ShiftTransportRequest request)
        {
            var shift = _configurationService.GetShifts().FirstOrDefault(s => s.Name == request.ShiftName);
            if (shift == null)
                return NotFound($"Shift '{request.ShiftName}' not found");

            var transportConfig = _configurationService.GetTransportConfiguration();
            TransportConnection? morningTransport = null;
            TransportConnection? afternoonTransport = null;

            var shifTimes = _configurationService.ParseShiftTimes(request.Date, shift);

            if (!string.IsNullOrEmpty(shift.MorningTime) && shifTimes.MorningStart.HasValue)
                morningTransport = await _transportService.GetConnectionAsync(shifTimes.MorningStart.Value);

            if (!string.IsNullOrEmpty(shift.AfternoonTime))
            {
                var shouldLoad = true;

                if (!string.IsNullOrEmpty(shift.MorningTime) && shifTimes.MorningEnd.HasValue && shifTimes.AfternoonStart.HasValue)
                {
                    var breakMinutes = (shifTimes.AfternoonStart.Value - shifTimes.MorningEnd.Value).TotalMinutes;
                    shouldLoad = breakMinutes >= transportConfig.MinBreakMinutes;
                }

                if (shouldLoad && shifTimes.AfternoonStart.HasValue)
                    afternoonTransport = await _transportService.GetConnectionAsync(shifTimes.AfternoonStart.Value);
            }

            return Ok(new ShiftWithTransport
            {
                Date = request.Date,
                Shift = shift,
                MorningTransport = morningTransport,
                AfternoonTransport = afternoonTransport
            });
        }

        [HttpPost("export_ics")]
        public async Task<IActionResult> ExportIcs([FromBody] ExportRequest request)
        {
            var shiftsWithTransport = await BuildShiftsWithTransportAsync(request.Year, request.Month);
            var ics = _icsService.GenerateIcs(shiftsWithTransport);
            return File(System.Text.Encoding.UTF8.GetBytes(ics), "text/calendar", "schedule.ics");
        }

        [HttpPost("export_pdf")]
        public async Task<IActionResult> ExportPdf([FromBody] ExportRequest request)
        {
            var shiftsWithTransport = await BuildShiftsWithTransportAsync(request.Year, request.Month);
            var pdf = _pdfExportService.GenerateMonthlySchedulePdf(shiftsWithTransport);
            return File(pdf, "application/pdf", "schedule.pdf");
        }

        [HttpPost("save_schedule")]
        public async Task<IActionResult> SaveSchedule([FromBody] SaveScheduleRequest request)
        {
            try
            {
                await _configurationService.SaveScheduleAsync(request.Year, request.Month, request.Schedule);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error saving schedule: {ex.Message}");
            }
        }

        [HttpGet("load_schedule/{year}/{month}")]
        public async Task<IActionResult> LoadSchedule(int year, int month)
        {
            try
            {
                var schedule = await _configurationService.LoadScheduleAsync(year, month);
                return Ok(schedule);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error loading schedule: {ex.Message}");
            }
        }

        [HttpDelete("delete_schedule/{year}/{month}")]
        public async Task<IActionResult> DeleteSchedule(int year, int month)
        {
            try
            {
                await _configurationService.DeleteScheduleAsync(year, month);
                return Ok();
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error deleting schedule: {ex.Message}");
            }
        }

        [HttpGet("calendars")]
        public async Task<IActionResult> GetCalendars([FromQuery] string provider)
        {
            try
            {
                ICalendarService service = provider == "nextcloud"
                    ? _nextcloudCalendarService
                    : _googleCalendarService;

                var calendars = await service.GetCalendarsAsync();
                return Ok(calendars);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error retrieving calendars: {ex.Message}");
            }
        }

        [HttpPost("sync_to_calendar")]
        public async Task<IActionResult> SyncToCalendar([FromBody] SyncToCalendarRequest request)
        {
            try
            {
                var shiftsWithTransport = await BuildShiftsWithTransportAsync(request.Year, request.Month);

                ICalendarService service = request.Provider == "nextcloud"
                    ? _nextcloudCalendarService
                    : _googleCalendarService;

                await service.SyncShiftsToCalendarAsync(request.CalendarId, shiftsWithTransport);
                return Ok(new { message = "Shifts synced successfully!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error syncing to calendar: {ex.Message}");
            }
        }

        private async Task<List<ShiftWithTransport>> BuildShiftsWithTransportAsync(int year, int month)
        {
            var schedule = await _configurationService.LoadScheduleAsync(year, month);
            var shiftsWithTransport = new List<ShiftWithTransport>();
            var transportConfig = _configurationService.GetTransportConfiguration();

            foreach (var kvp in schedule)
            {
                var date = kvp.Key;
                var shiftName = kvp.Value;

                var shift = _configurationService.GetShifts().FirstOrDefault(s => s.Name == shiftName);
                if (shift == null) continue;

                var shiftTimes = _configurationService.ParseShiftTimes(date, shift);
                TransportConnection? morningTransport = null;
                TransportConnection? afternoonTransport = null;

                if (!string.IsNullOrEmpty(shift.MorningTime) && shiftTimes.MorningStart.HasValue)
                    morningTransport = await _transportService.GetConnectionAsync(shiftTimes.MorningStart.Value);

                if (!string.IsNullOrEmpty(shift.AfternoonTime))
                {
                    var shouldLoad = true;

                    if (!string.IsNullOrEmpty(shift.MorningTime) && shiftTimes.MorningEnd.HasValue && shiftTimes.AfternoonStart.HasValue)
                    {
                        var breakMinutes = (shiftTimes.AfternoonStart.Value - shiftTimes.MorningEnd.Value).TotalMinutes;
                        shouldLoad = breakMinutes >= transportConfig.MinBreakMinutes;
                    }

                    if (shouldLoad && shiftTimes.AfternoonStart.HasValue)
                        afternoonTransport = await _transportService.GetConnectionAsync(shiftTimes.AfternoonStart.Value);
                }

                shiftsWithTransport.Add(new ShiftWithTransport
                {
                    Date = date,
                    Shift = shift,
                    MorningTransport = morningTransport,
                    AfternoonTransport = afternoonTransport
                });
            }

            return shiftsWithTransport;
        }
    }

    public class ShiftTransportRequest
    {
        public string ShiftName { get; set; } = string.Empty;
        public DateTime Date { get; set; }
    }

    public class SaveScheduleRequest
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public Dictionary<DateTime, string> Schedule { get; set; } = new();
    }

    public class ExportRequest
    {
        public int Year { get; set; }
        public int Month { get; set; }
    }

    public class SyncToCalendarRequest
    {
        public string Provider { get; set; } = string.Empty;
        public string CalendarId { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
    }
}
```

- [ ] **Step 3: Build**

```bash
cd /home/schaad/Documents/ShiftScheduler && dotnet build
```

Expected: Build succeeds.

- [ ] **Step 4: Commit**

```bash
git add Server/Program.cs Server/Controllers/ShiftController.cs
git commit -m "feat: add provider-agnostic calendar endpoints, register NextcloudCalendarService"
```

---

## Task 8: Update Blazor client

**Files:**
- Modify: `Client/Pages/Index.razor.cs`
- Modify: `Client/Pages/Index.razor`

- [ ] **Step 1: Replace `Client/Pages/Index.razor.cs`**

Replace the full file:

```csharp
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Client.Pages
{
    public partial class Index : ComponentBase
    {
        [Inject] private HttpClient HttpClient { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;

        private List<Shift> Shifts { get; set; } = new();
        private Dictionary<DateTime, string> SelectedSchedule { get; set; } = new();
        private Dictionary<DateTime, ShiftWithTransport> SelectedShiftsWithTransport { get; set; } = new();
        private Dictionary<DateTime, bool> _isLoadingTransportPerDay { get; set; } = new();

        private bool _isCurrentMonth = true;
        private bool _isLoadingInitial = false;
        private bool _showConfigDialog = false;
        private bool _isSyncing = false;
        private bool _showCalendarSelector = false;
        private string _syncProvider = string.Empty;
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private List<CalendarInfo> _availableCalendars = new();

        private MonthAndYear SelectedDate => _isCurrentMonth ? MonthAndYear.Current() : MonthAndYear.Next();

        protected override async Task OnInitializedAsync()
        {
            _isLoadingInitial = true;

            Shifts = await HttpClient.GetFromJsonAsync<List<Shift>>("api/shift/shifts") ?? new();

            _isLoadingInitial = false;
            StateHasChanged();

            _ = Task.Run(async () =>
            {
                await LoadScheduleFromStorage();
                await InvokeAsync(StateHasChanged);
            });
        }

        private async Task SelectCurrentMonth()
        {
            _isCurrentMonth = true;
            await LoadScheduleFromStorage();
            StateHasChanged();
        }

        private async Task SelectNextMonth()
        {
            _isCurrentMonth = false;
            await LoadScheduleFromStorage();
            StateHasChanged();
        }

        private async Task SelectShift(DateTime day, string shiftName)
        {
            SelectedSchedule[day] = shiftName;

            _isLoadingTransportPerDay[day] = true;
            StateHasChanged();

            try
            {
                var request = new { ShiftName = shiftName, Date = day };
                var response = await HttpClient.PostAsJsonAsync("api/shift/shift_transport", request);

                if (response.IsSuccessStatusCode)
                {
                    var shiftWithTransport = await response.Content.ReadFromJsonAsync<ShiftWithTransport>();
                    if (shiftWithTransport != null)
                        SelectedShiftsWithTransport[day] = shiftWithTransport;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching transport data: {ex.Message}");
                var shift = Shifts.FirstOrDefault(s => s.Name == shiftName);
                if (shift != null)
                {
                    SelectedShiftsWithTransport[day] = new ShiftWithTransport
                    {
                        Date = day,
                        Shift = shift,
                        MorningTransport = null,
                        AfternoonTransport = null
                    };
                }
            }
            finally
            {
                _isLoadingTransportPerDay[day] = false;
                StateHasChanged();
            }

            await SaveScheduleToStorage();
        }

        private async Task ExportToIcs()
        {
            try
            {
                _errorMessage = string.Empty;
                _successMessage = string.Empty;
                StateHasChanged();

                var request = new { Year = SelectedDate.Year, Month = SelectedDate.Month };
                var response = await HttpClient.PostAsJsonAsync("api/shift/export_ics", request);

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var base64 = Convert.ToBase64String(bytes);
                    var fileUrl = $"data:text/calendar;base64,{base64}";
                    NavigationManager.NavigateTo(fileUrl, true);
                    _successMessage = "ICS file exported successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Failed to export ICS file: {response.StatusCode}. {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error exporting ICS file: {ex.Message}";
            }

            StateHasChanged();
        }

        private async Task ExportToPdf()
        {
            try
            {
                _errorMessage = string.Empty;
                _successMessage = string.Empty;
                StateHasChanged();

                var request = new { Year = SelectedDate.Year, Month = SelectedDate.Month };
                var response = await HttpClient.PostAsJsonAsync("api/shift/export_pdf", request);

                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var base64 = Convert.ToBase64String(bytes);
                    await JSRuntime.InvokeVoidAsync("downloadFile", $"Schedule {SelectedDate.Year}-{SelectedDate.Month:D2}.pdf", "application/pdf", base64);
                    _successMessage = "PDF file exported successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Failed to export PDF file: {response.StatusCode}. {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error exporting PDF file: {ex.Message}";
            }

            StateHasChanged();
        }

        private Task SyncToGoogleCalendar() => StartCalendarSync("google");
        private Task SyncToNextcloud() => StartCalendarSync("nextcloud");

        private async Task StartCalendarSync(string provider)
        {
            try
            {
                _errorMessage = string.Empty;
                _successMessage = string.Empty;
                _syncProvider = provider;
                _isSyncing = true;
                StateHasChanged();

                var response = await HttpClient.GetAsync($"api/shift/calendars?provider={provider}");
                if (response.IsSuccessStatusCode)
                {
                    _availableCalendars = await response.Content.ReadFromJsonAsync<List<CalendarInfo>>() ?? new();

                    if (_availableCalendars.Count == 1)
                    {
                        await PerformCalendarSync(_availableCalendars[0].Id);
                    }
                    else if (_availableCalendars.Count > 1)
                    {
                        _showCalendarSelector = true;
                    }
                    else
                    {
                        _errorMessage = "No writable calendars found.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Failed to retrieve calendars: {response.StatusCode}. {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error accessing calendar: {ex.Message}";
            }
            finally
            {
                _isSyncing = false;
                StateHasChanged();
            }
        }

        private async Task SelectCalendarAndSync(string calendarId)
        {
            _showCalendarSelector = false;
            await PerformCalendarSync(calendarId);
        }

        private async Task PerformCalendarSync(string calendarId)
        {
            try
            {
                _isSyncing = true;
                StateHasChanged();

                var request = new
                {
                    Provider = _syncProvider,
                    CalendarId = calendarId,
                    Year = SelectedDate.Year,
                    Month = SelectedDate.Month
                };

                var response = await HttpClient.PostAsJsonAsync("api/shift/sync_to_calendar", request);

                if (response.IsSuccessStatusCode)
                {
                    _successMessage = "Shifts synced successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Failed to sync: {response.StatusCode}. {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error syncing: {ex.Message}";
            }
            finally
            {
                _isSyncing = false;
                StateHasChanged();
            }
        }

        private void CloseCalendarSelector()
        {
            _showCalendarSelector = false;
            _availableCalendars.Clear();
            StateHasChanged();
        }

        private async Task SaveScheduleToStorage()
        {
            try
            {
                var request = new
                {
                    Year = SelectedDate.Year,
                    Month = SelectedDate.Month,
                    Schedule = SelectedSchedule
                };

                await HttpClient.PostAsJsonAsync("api/shift/save_schedule", request);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task LoadScheduleFromStorage()
        {
            try
            {
                var response = await HttpClient.GetAsync($"api/shift/load_schedule/{SelectedDate.Year}/{SelectedDate.Month}");

                if (response.IsSuccessStatusCode)
                {
                    SelectedSchedule = await response.Content.ReadFromJsonAsync<Dictionary<DateTime, string>>() ?? new();
                    await ReloadTransportDataForSchedule();
                }
                else
                {
                    SelectedSchedule = new Dictionary<DateTime, string>();
                    SelectedShiftsWithTransport = new Dictionary<DateTime, ShiftWithTransport>();
                }
            }
            catch (Exception)
            {
                SelectedSchedule = new Dictionary<DateTime, string>();
                SelectedShiftsWithTransport = new Dictionary<DateTime, ShiftWithTransport>();
            }
        }

        private async Task ReloadTransportDataForSchedule()
        {
            SelectedShiftsWithTransport.Clear();
            _isLoadingTransportPerDay.Clear();

            foreach (var kvp in SelectedSchedule)
                _isLoadingTransportPerDay[kvp.Key] = true;

            StateHasChanged();

            var transportTasks = SelectedSchedule.Select(async kvp =>
            {
                var day = kvp.Key;
                var shiftName = kvp.Value;

                try
                {
                    var request = new { ShiftName = shiftName, Date = day };
                    var response = await HttpClient.PostAsJsonAsync("api/shift/shift_transport", request);

                    if (response.IsSuccessStatusCode)
                    {
                        var shiftWithTransport = await response.Content.ReadFromJsonAsync<ShiftWithTransport>();
                        if (shiftWithTransport != null)
                            SelectedShiftsWithTransport[day] = shiftWithTransport;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reloading transport data for {day}: {ex.Message}");
                    var shift = Shifts.FirstOrDefault(s => s.Name == shiftName);
                    if (shift != null)
                    {
                        SelectedShiftsWithTransport[day] = new ShiftWithTransport
                        {
                            Date = day,
                            Shift = shift,
                            MorningTransport = null,
                            AfternoonTransport = null
                        };
                    }
                }
                finally
                {
                    _isLoadingTransportPerDay[day] = false;
                    await InvokeAsync(StateHasChanged);
                }
            }).ToArray();

            await Task.WhenAll(transportTasks);
        }

        private async Task ResetSchedule()
        {
            SelectedSchedule.Clear();
            SelectedShiftsWithTransport.Clear();

            try
            {
                await HttpClient.DeleteAsync($"api/shift/delete_schedule/{SelectedDate.Year}/{SelectedDate.Month}");
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private string GetTransportSummary(DateTime date)
        {
            if (!SelectedShiftsWithTransport.TryGetValue(date, out var shiftWithTransport))
                return string.Empty;

            var summaries = new List<string>();

            var morningTransport = shiftWithTransport.GetMorningTransportSummary();
            if (!string.IsNullOrEmpty(morningTransport))
                summaries.Add(morningTransport);

            var afternoonTransport = shiftWithTransport.GetAfternoonTransportSummary();
            if (!string.IsNullOrEmpty(afternoonTransport))
                summaries.Add(afternoonTransport);

            if (summaries.Count == 0) return string.Empty;

            return "🚂 " + string.Join(" | ", summaries);
        }

        private string GetShiftTimes(DateTime date)
        {
            if (!SelectedSchedule.TryGetValue(date, out var shiftName))
                return string.Empty;

            var shift = Shifts.FirstOrDefault(s => s.Name == shiftName);
            if (shift == null) return string.Empty;

            var times = new List<string>();

            if (!string.IsNullOrEmpty(shift.MorningTime))
                times.Add($"🌅 {shift.MorningTime}");

            if (!string.IsNullOrEmpty(shift.AfternoonTime))
                times.Add($"🌆 {shift.AfternoonTime}");

            return string.Join(" | ", times);
        }

        private void ShowConfiguration()
        {
            _showConfigDialog = true;
            StateHasChanged();
        }

        private void HideConfiguration()
        {
            _showConfigDialog = false;
            StateHasChanged();
        }

        private async Task OnConfigurationChanged()
        {
            Shifts = await HttpClient.GetFromJsonAsync<List<Shift>>("api/shift/shifts") ?? new();
            StateHasChanged();
        }

        private bool IsLoadingTransportForDay(DateTime day) =>
            _isLoadingTransportPerDay.GetValueOrDefault(day, false);
    }
}
```

- [ ] **Step 2: Update `Client/Pages/Index.razor`**

Make the following targeted changes to `Index.razor`:

**2a. Add Nextcloud button** — in the `action-buttons` div, after the Google Calendar button, add:

```html
<button class="btn export-btn" @onclick="SyncToNextcloud" disabled="@(_isLoadingInitial || _isSyncing)"><span class="oi oi-cloud-upload" aria-hidden="true"></span> Sync to Nextcloud</button>
```

**2b. Update the calendar selector modal title** — change:

```html
<h5 class="modal-title">Select Google Calendar</h5>
```

To:

```html
<h5 class="modal-title">Select @(_syncProvider == "nextcloud" ? "Nextcloud" : "Google") Calendar</h5>
```

**2d. Remove the `@code` block at the bottom of `Index.razor`** — the `IsLoadingTransportForDay` helper method was moved into `Index.razor.cs`. Delete this block from `Index.razor`:

```razor
@code {
    // Helper method to check if a specific day is loading transport data
    private bool IsLoadingTransportForDay(DateTime day)
    {
        return _isLoadingTransportPerDay.GetValueOrDefault(day, false);
    }
}
```

**2e. Update the syncing loading overlay text** — change:

```html
<p>Syncing with google calendar...</p>
```

To:

```html
<p>Syncing with @(_syncProvider == "nextcloud" ? "Nextcloud" : "Google Calendar")...</p>
```

- [ ] **Step 3: Build**

```bash
cd /home/schaad/Documents/ShiftScheduler && dotnet build
```

Expected: Build succeeds.

- [ ] **Step 4: Run all tests**

```bash
cd /home/schaad/Documents/ShiftScheduler && dotnet test ShiftScheduler.Services.Tests/ -v minimal
```

Expected: All tests pass.

- [ ] **Step 5: Commit**

```bash
git add Client/Pages/Index.razor.cs Client/Pages/Index.razor
git commit -m "feat: add Nextcloud sync button and update UI for provider-agnostic calendar flow"
```

---

## Verification Checklist

After all tasks are complete, verify end-to-end:

- [ ] `dotnet build` succeeds with no warnings on `GoogleCalendar` references
- [ ] `dotnet test ShiftScheduler.Services.Tests/` — all tests pass
- [ ] Both "Sync to Google Calendar" and "Sync to Nextcloud" buttons appear in the UI
- [ ] Modal title changes based on which button was pressed
- [ ] `GET /api/shift/calendars?provider=google` and `?provider=nextcloud` both return `CalendarInfo[]`
- [ ] `POST /api/shift/sync_to_calendar` with `Provider: "google"` still works as before
