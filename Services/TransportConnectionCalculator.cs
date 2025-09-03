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

        foreach (var connection in connections)
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
            var sortedValid = validConnections
                .OrderBy(c => c.ArrivalTime)
                .ToList();

            var bestValidConnection = sortedValid.Last();

            // Check if the best valid connection arrives too early (more than maxEarlyArrivalMinutes before shift)
            if (bestValidConnection.ArrivalTime < earliestAcceptableTime && lateValidConnections.Count > 0)
            {
                // Return the earliest connection that arrives after latest arrival time but within acceptable range
                var sortedLateValid = lateValidConnections
                    .OrderBy(c => c.ArrivalTime)
                    .ToList();
                
                return sortedLateValid.First();
            }

            return bestValidConnection;
        }

        // If no connections arrive before latest arrival time, check if any arrive within acceptable late range
        if (lateValidConnections.Count > 0)
        {
            var sortedLateValid = lateValidConnections
                .OrderBy(c => c.ArrivalTime)
                .ToList();
            
            return sortedLateValid.First();
        }

        return null;
    }
}