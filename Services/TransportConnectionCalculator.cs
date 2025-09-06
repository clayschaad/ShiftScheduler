using Microsoft.Extensions.Logging;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services;

public record ConnectionPickArgument(
    DateTimeOffset ShiftStartTime,
    int SafetyBufferMinutes,
    int MaxEarlyArrivalMinutes,
    int MaxLateArrivalMinutes)
{
    public override string ToString()
    {
        return $"ShiftStartTime: {ShiftStartTime}, SafetyBufferMinutes: {SafetyBufferMinutes}, MaxEarlyArrivalMinutes{MaxEarlyArrivalMinutes}, MaxLateArrivalMinutes:{MaxLateArrivalMinutes}";
    }
}

public static class TransportConnectionCalculator
{
    public static TransportConnection? FindBestConnection(
        IReadOnlyList<TransportConnection> connections, ConnectionPickArgument args, ILogger logger)
    {
        var latestArrivalTime = args.ShiftStartTime.AddMinutes(-args.SafetyBufferMinutes);
        var earliestAcceptableTime = args.ShiftStartTime.AddMinutes(-args.MaxEarlyArrivalMinutes);
        var latestAcceptableTime = args.ShiftStartTime.AddMinutes(args.MaxLateArrivalMinutes);
        
        logger.LogDebug(args.ToString());
        logger.LogDebug($"latestArrivalTime: {latestArrivalTime}");
        logger.LogDebug($"earliestAcceptableTime: {earliestAcceptableTime}");
        logger.LogDebug($"latestAcceptableTime: {latestAcceptableTime}");

        var validConnections = new List<TransportConnection>();
        var lateValidConnections = new List<TransportConnection>();

        foreach (var connection in connections.OrderBy(c => c.ArrivalTime))
        {
            if (connection.ArrivalTime <= latestArrivalTime)
            {
                validConnections.Add(connection);
            }
            else if (connection.ArrivalTime <= latestAcceptableTime)
            {
                lateValidConnections.Add(connection);
            }
        }

        // If we have valid connections (arriving before latest arrival time)
        if (validConnections.Count > 0)
        {
            var bestValidConnection = validConnections.Last();
            if (bestValidConnection.ArrivalTime > earliestAcceptableTime)
            {
                logger.LogDebug("Found bestValidConnection {BestValidConnection}", bestValidConnection);
                return bestValidConnection;
            }
        }

        // If no connections arrive before latest arrival time, check if any arrive within acceptable late range
        if (lateValidConnections.Count > 0)
        {
            logger.LogDebug("Found lateValidConnection {LateValidConnection}", lateValidConnections.First());
            return lateValidConnections.First();
        }

        logger.LogDebug("Use last valid connection {LastValidConnection}", validConnections.LastOrDefault());
        return validConnections.LastOrDefault();
    }
}