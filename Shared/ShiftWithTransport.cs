namespace ShiftScheduler.Shared
{
    public record ShiftWithTransport
    {
        public DateTime Date { get; init; }
        public Shift Shift { get; init; } = new();
        public TransportConnection? MorningTransport { get; init; }
        public TransportConnection? AfternoonTransport { get; init; }
        
        public string GetMorningTransportSummary()
        {
            return MorningTransport != null ? FormatTransportSummary(MorningTransport) : string.Empty;
        }
        
        public string GetAfternoonTransportSummary()
        {
            return AfternoonTransport != null ? FormatTransportSummary(AfternoonTransport) : string.Empty;
        }
        
        private string FormatTransportSummary(TransportConnection connection)
        {
            if (string.IsNullOrEmpty(connection.DepartureTime)) return string.Empty;
            
            var departure = DateTime.TryParse(connection.DepartureTime, out var dep) ? dep.ToString("HH:mm") : connection.DepartureTime;
            var arrival = DateTime.TryParse(connection.ArrivalTime, out var arr) ? arr.ToString("HH:mm") : connection.ArrivalTime;
            return $"{departure}→{arrival}";
        }
    }
}