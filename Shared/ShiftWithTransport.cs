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
            var departure = connection.DepartureTime.ToString("HH:mm");
            var arrival = connection.ArrivalTime.ToString("HH:mm");
            return $"{departure}→{arrival}";
        }
    }
}