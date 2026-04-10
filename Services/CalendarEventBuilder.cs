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
