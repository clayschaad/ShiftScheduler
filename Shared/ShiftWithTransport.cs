namespace ShiftScheduler.Shared.Models
{
    // View model that combines shift information with transport data for a specific date
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
        
        private static string FormatTransportSummary(TransportConnection connection)
        {
            if (string.IsNullOrEmpty(connection.DepartureTime)) return string.Empty;
            
            var departure = DateTime.TryParse(connection.DepartureTime, out var dep) ? dep.ToString("HH:mm") : connection.DepartureTime;
            var arrival = DateTime.TryParse(connection.ArrivalTime, out var arr) ? arr.ToString("HH:mm") : connection.ArrivalTime;
            
            var mainJourney = connection.Sections?.FirstOrDefault()?.Journey;
            var trainInfo = mainJourney != null ? $"{mainJourney.Category}{mainJourney.Number}" : "Train";
            
            return $"{trainInfo} {departure}→{arrival}";
        }
    }
}