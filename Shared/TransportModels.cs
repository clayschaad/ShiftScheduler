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
        public TransportConnection()
        {
        }

        public TransportConnection(string arrivalTime)
        {
            ArrivalTime = DateTimeOffset.Parse(arrivalTime);
        }

        public DateTimeOffset DepartureTime { get; set; }
        public DateTimeOffset ArrivalTime { get; set; }
        public TimeSpan Duration { get; set; }
        public string? Platform { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{Platform}: {DepartureTime} - {ArrivalTime} ({Duration})";
        }
    }
}