using ShiftScheduler.Shared;

namespace ShiftScheduler.Services;

public static class TransportConnectionCalculator
{
    public static TransportConnection? FindBestConnection(
        IReadOnlyList<TransportConnection> connections, 
        DateTime shiftStartTime, 
        int safetyBufferMinutes, 
        int maxEarlyArrivalMinutes, 
        int maxLateArrivalMinutes)
    {
        var latestArrivalTime = shiftStartTime.AddMinutes(-safetyBufferMinutes);
        var earliestAcceptableTime = shiftStartTime.AddMinutes(-maxEarlyArrivalMinutes);
        var latestAcceptableTime = shiftStartTime.AddMinutes(maxLateArrivalMinutes);

        var validConnections = new List<TransportConnection>();
        var lateValidConnections = new List<TransportConnection>();

        foreach (var connection in connections)
        {
            if (DateTime.TryParse(connection.ArrivalTime, out var arrivalTime))
            {
                if (arrivalTime <= latestArrivalTime)
                {
                    validConnections.Add(connection);
                }
                else if (arrivalTime <= latestAcceptableTime)
                {
                    lateValidConnections.Add(connection);
                }
            }
        }

        // If we have valid connections (arriving before latest arrival time)
        if (validConnections.Count > 0)
        {
            var sortedValid = validConnections
                .OrderBy(c => DateTime.Parse(c.ArrivalTime ?? "00:00"))
                .ToList();

            var bestValidConnection = sortedValid.Last();
            var bestValidArrivalTime = DateTime.Parse(bestValidConnection.ArrivalTime!);

            // Check if the best valid connection arrives too early (more than maxEarlyArrivalMinutes before shift)
            if (bestValidArrivalTime < earliestAcceptableTime && lateValidConnections.Count > 0)
            {
                // Return the earliest connection that arrives after latest arrival time but within acceptable range
                var sortedLateValid = lateValidConnections
                    .OrderBy(c => DateTime.Parse(c.ArrivalTime ?? "23:59"))
                    .ToList();
                
                return sortedLateValid.First();
            }

            return bestValidConnection;
        }

        // If no connections arrive before latest arrival time, check if any arrive within acceptable late range
        if (lateValidConnections.Count > 0)
        {
            var sortedLateValid = lateValidConnections
                .OrderBy(c => DateTime.Parse(c.ArrivalTime ?? "23:59"))
                .ToList();
            
            return sortedLateValid.First();
        }

        // No suitable connections found
        return null;
    }
}