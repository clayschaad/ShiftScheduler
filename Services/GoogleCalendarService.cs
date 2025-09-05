using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services;

public interface IGoogleCalendarService
{
    Task<List<CalendarListEntry>> GetCalendarsAsync();
    Task SyncShiftsToCalendarAsync(string calendarId, List<ShiftWithTransport> shifts);
}

public class GoogleCalendarService : IGoogleCalendarService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IcsExportService _icsExportService;
    
    public GoogleCalendarService(IHttpContextAccessor httpContextAccessor, IcsExportService icsExportService)
    {
        _httpContextAccessor = httpContextAccessor;
        _icsExportService = icsExportService;
    }

    public async Task<List<CalendarListEntry>> GetCalendarsAsync()
    {
        var service = await CreateCalendarServiceAsync();
        var request = service.CalendarList.List();
        var response = await request.ExecuteAsync();
        
        return response.Items?.Where(c => c.AccessRole == "owner" || c.AccessRole == "writer").ToList() ?? [];
    }

    public async Task SyncShiftsToCalendarAsync(string calendarId, List<ShiftWithTransport> shifts)
    {
        var service = await CreateCalendarServiceAsync();
        
        // Get date range for deletion
        if (shifts.Count == 0) return;
        
        var minDate = shifts.Min(s => s.Date).Date;
        var maxDate = shifts.Max(s => s.Date).Date.AddDays(1);
        
        // Delete existing shift events in the date range
        await DeleteExistingShiftEventsAsync(service, calendarId, minDate, maxDate);
        
        // Create new events for each shift
        foreach (var shiftWithTransport in shifts)
        {
            await CreateShiftEventsAsync(service, calendarId, shiftWithTransport);
        }
    }

    private async Task DeleteExistingShiftEventsAsync(CalendarService service, string calendarId, DateTime startDate, DateTime endDate)
    {
        var request = service.Events.List(calendarId);
        request.TimeMinDateTimeOffset = startDate;
        request.TimeMaxDateTimeOffset = endDate;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        
        var response = await request.ExecuteAsync();
        
        if (response.Items != null)
        {
            var shiftsToDelete = response.Items.Where(e => 
                e.ExtendedProperties?.Private__?.ContainsKey("shiftSchedulerEvent") == true);
            
            foreach (var eventToDelete in shiftsToDelete)
            {
                await service.Events.Delete(calendarId, eventToDelete.Id).ExecuteAsync();
            }
        }
    }

    private async Task CreateShiftEventsAsync(CalendarService service, string calendarId, ShiftWithTransport shiftWithTransport)
    {
        var shift = shiftWithTransport.Shift;
        var date = shiftWithTransport.Date;

        // Skip shifts with no time information
        if (string.IsNullOrEmpty(shift.MorningTime) && string.IsNullOrEmpty(shift.AfternoonTime))
            return;

        // Create morning event if it exists
        if (!string.IsNullOrEmpty(shift.MorningTime))
        {
            var morningEvent = CreateEventFromShift(shift, date, shift.MorningTime, "Morning", shiftWithTransport.MorningTransport);
            await service.Events.Insert(morningEvent, calendarId).ExecuteAsync();
        }

        // Create afternoon event if it exists
        if (!string.IsNullOrEmpty(shift.AfternoonTime))
        {
            var afternoonEvent = CreateEventFromShift(shift, date, shift.AfternoonTime, "Afternoon", shiftWithTransport.AfternoonTransport);
            await service.Events.Insert(afternoonEvent, calendarId).ExecuteAsync();
        }
    }

    private static Event CreateEventFromShift(Shift shift, DateTime date, string timeRange, string period, TransportConnection? transport)
    {
        var times = timeRange.Split('-');
        var startTime = date.Add(TimeSpan.Parse(times[0]));
        var endTime = date.Add(TimeSpan.Parse(times[1]));
        
        var summary = $"{shift.Name} ({period})";
        var description = "";

        if (transport != null)
        {
            var transportInfo = FormatTransportInfo(transport);
            description = $"Transport: {transportInfo}";
        }

        return new Event
        {
            Summary = summary,
            Description = description,
            Start = new EventDateTime
            {
                DateTimeDateTimeOffset = startTime,
                TimeZone = "Europe/Zurich"
            },
            End = new EventDateTime
            {
                DateTimeDateTimeOffset = endTime,
                TimeZone = "Europe/Zurich"
            },
            ExtendedProperties = new Event.ExtendedPropertiesData
            {
                Private__ = new Dictionary<string, string>
                {
                    ["shiftSchedulerEvent"] = "true",
                    ["shiftName"] = shift.Name,
                    ["period"] = period.ToLower()
                }
            }
        };
    }

    private static string FormatTransportInfo(TransportConnection transport)
    {
        var departure = transport.DepartureTime.ToString("HH:mm");
        var arrival = transport.ArrivalTime.ToString("HH:mm");
        return $"{departure} → {arrival}";
    }

    private async Task<CalendarService> CreateCalendarServiceAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No HTTP context available");
        
        var accessToken = await httpContext.GetTokenAsync("access_token");
        if (string.IsNullOrEmpty(accessToken))
        {
            throw new UnauthorizedAccessException("No access token available");
        }

        var credential = GoogleCredential.FromAccessToken(accessToken);
        
        return new CalendarService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "ShiftScheduler"
        });
    }
}