namespace ShiftScheduler.Shared
{
    public class TransportConfiguration
    {
        public string StartStation { get; set; } = string.Empty;
        public string EndStation { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = string.Empty;
        public int SafetyBufferMinutes { get; set; } = 30;
        public int MinBreakMinutes { get; set; } = 60;
        public int MaxEarlyArrivalMinutes { get; set; } = 60;
        public int MaxLateArrivalMinutes { get; set; } = 15;
        public int CacheDurationDays { get; set; } = 1;
    }

    public class TransportConnection
    {
        public string DepartureTime { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string? Platform { get; set; } = string.Empty;
        public List<TransportSection> Sections { get; set; } = new();
    }

    public class TransportSection
    {
        public TransportJourney? Journey { get; set; }
        public TransportCheckpoint? Departure { get; set; }
        public TransportCheckpoint? Arrival { get; set; }
    }

    public class TransportJourney
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
    }

    public class TransportCheckpoint
    {
        public TransportStation? Station { get; set; }
        public string? Departure { get; set; } = string.Empty;
        public string? Arrival { get; set; } = string.Empty;
        public string? Platform { get; set; } = string.Empty;
    }

    public class TransportStation
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
    }
}