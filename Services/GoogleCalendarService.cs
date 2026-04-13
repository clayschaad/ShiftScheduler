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
        {
            var eventData = CalendarEventBuilder.BuildEventContent(shift, null);
            var allDayEvent = CreateAllDayGoogleEvent(eventData, DateOnly.FromDateTime(date));
            await CalendarEventBuilder.ExecuteWithRetryAsync(async () => await service.Events.Insert(allDayEvent, calendarId).ExecuteAsync());
            return;
        }

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

    private static Event CreateAllDayGoogleEvent(CalendarEventData eventData, DateOnly date)
    {
        var dateStr = date.ToString("yyyy-MM-dd");
        var nextDateStr = date.AddDays(1).ToString("yyyy-MM-dd");
        return new Event
        {
            Summary = eventData.Summary,
            Start = new EventDateTime { Date = dateStr },
            End = new EventDateTime { Date = nextDateStr },
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
