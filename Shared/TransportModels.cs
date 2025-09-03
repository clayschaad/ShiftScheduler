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
            ArrivalTime = arrivalTime;
        }

        public string DepartureTime { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string? Platform { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{Platform}: {DepartureTime} - {ArrivalTime} ({Duration})";
        }
    }
}