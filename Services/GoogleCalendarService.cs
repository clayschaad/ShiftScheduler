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

public class GoogleCalendarService(IHttpContextAccessor httpContextAccessor, IConfigurationService configurationService)
    : IGoogleCalendarService
{
    public async Task<List<CalendarListEntry>> GetCalendarsAsync()
    {
        var service = await CreateCalendarServiceAsync();
        var request = service.CalendarList.List();
        var response = await ExecuteWithRetryAsync(async () => await request.ExecuteAsync());
        
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
        
        // Add a small delay to avoid rate limiting
        await Task.Delay(500);
        
        // Create new events for each shift with rate limiting protection
        await CreateShiftEventsWithRateLimitingAsync(service, calendarId, shifts);
    }

    private async Task DeleteExistingShiftEventsAsync(CalendarService service, string calendarId, DateTime startDate, DateTime endDate)
    {
        var request = service.Events.List(calendarId);
        request.TimeMinDateTimeOffset = startDate;
        request.TimeMaxDateTimeOffset = endDate;
        request.SingleEvents = true;
        request.OrderBy = EventsResource.ListRequest.OrderByEnum.StartTime;
        
        var response = await ExecuteWithRetryAsync(async () => await request.ExecuteAsync());
        
        if (response.Items != null)
        {
            var shiftsToDelete = response.Items.Where(e => 
                e.ExtendedProperties?.Private__?.ContainsKey("shiftSchedulerEvent") == true).ToList();
            
            // Delete events with rate limiting protection
            foreach (var eventToDelete in shiftsToDelete)
            {
                await ExecuteWithRetryAsync(async () => 
                {
                    await service.Events.Delete(calendarId, eventToDelete.Id).ExecuteAsync();
                    return true; // Return something for the generic method
                });
                
                // Small delay between deletions to avoid rate limiting
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
            
            // Add delay between shift processing to avoid rate limiting
            if (shifts.Count > 1)
                await Task.Delay(300);
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
            var morningEvent = CreateEventFromShift(shift, date, shift.MorningTime, shiftWithTransport.MorningTransport);
            await ExecuteWithRetryAsync(async () => await service.Events.Insert(morningEvent, calendarId).ExecuteAsync());
        }

        // Create afternoon event if it exists
        if (!string.IsNullOrEmpty(shift.AfternoonTime))
        {
            var afternoonEvent = CreateEventFromShift(shift, date, shift.AfternoonTime, shiftWithTransport.AfternoonTransport);
            await ExecuteWithRetryAsync(async () => await service.Events.Insert(afternoonEvent, calendarId).ExecuteAsync());
        }
    }

    private Event CreateEventFromShift(Shift shift, DateTime date, string timeRange, TransportConnection? transport)
    {
        var times = timeRange.Split('-');
        var startTime = date.Add(TimeSpan.Parse(times[0]));
        var endTime = date.Add(TimeSpan.Parse(times[1]));
        
        var summary = $"{shift.Name}";
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
                TimeZone = configurationService.GetTimeZone()
            },
            End = new EventDateTime
            {
                DateTimeDateTimeOffset = endTime,
                TimeZone = configurationService.GetTimeZone()
            },
            ExtendedProperties = new Event.ExtendedPropertiesData
            {
                Private__ = new Dictionary<string, string>
                {
                    ["shiftSchedulerEvent"] = "true",
                    ["shiftName"] = shift.Name
                }
            }
        };
    }

    private static string FormatTransportInfo(TransportConnection transport)
    {
        var departure = transport.DepartureTime.ToString("HH:mm");
        var arrival = transport.ArrivalTime.ToString("HH:mm");
        return $"{transport.Platform}: {departure} → {arrival} ({transport.Duration.TotalMinutes} Minutes)";
    }

    private async Task<CalendarService> CreateCalendarServiceAsync()
    {
        var httpContext = httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No HTTP context available");
        
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

    private static async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, int maxRetries = 3)
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
                // Rate limit exceeded (HTTP 429), wait with exponential backoff
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
            catch (Exception ex) when (ex.Message.Contains("Rate Limit") && attempt < maxRetries)
            {
                // Another form of rate limit error
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
            catch (Exception ex) when (ex.Message.Contains("Forbidden") && ex.Message.Contains("quota") && attempt < maxRetries)
            {
                // Quota exceeded error
                await Task.Delay(delay);
                delay = TimeSpan.FromMilliseconds(delay.TotalMilliseconds * 2); // Exponential backoff
            }
        }
        
        // If we get here, all retries failed, execute one more time to get the actual exception
        return await operation();
    }
}