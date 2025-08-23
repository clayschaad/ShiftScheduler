using ShiftScheduler.Shared;

namespace ShiftScheduler.Services;

public static class TransportConnectionCalculator
{
    public static TransportConnection? FindBestConnection(IReadOnlyList<TransportConnection> connections, DateTime latestArrivalTime)
    {
        var validConnections = new List<TransportConnection>();
            
        foreach (var connection in connections)
        {
            if (DateTime.TryParse(connection.ArrivalTime, out var arrivalTime))
            {
                if (arrivalTime <= latestArrivalTime)
                {
                    validConnections.Add(connection);
                }
            }
        }

        if (validConnections.Count > 0)
        {
            var sortedValid = validConnections
                .OrderBy(c => DateTime.Parse(c.ArrivalTime ?? "00:00"))
                .ToList();

            return sortedValid.Last();
        }

        // If no valid connections, return the earliest available
        return connections
            .OrderBy(c => DateTime.Parse(c.ArrivalTime ?? "23:59"))
            .FirstOrDefault();
    }
}