namespace ShiftScheduler.Shared.Models
{
    public class ShiftTransport
    {
        public string ShiftName { get; set; } = string.Empty;
        public TransportConnection? MorningTransport { get; set; }
        public TransportConnection? AfternoonTransport { get; set; }
    }

    public class TransportConfiguration
    {
        public string StartStation { get; set; } = string.Empty;
        public string EndStation { get; set; } = string.Empty;
        public string ApiBaseUrl { get; set; } = string.Empty;
        public int SafetyBufferMinutes { get; set; } = 30;
    }

    public class TransportConnection
    {
        public string DepartureTime { get; set; } = string.Empty;
        public string ArrivalTime { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
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
        public string Departure { get; set; } = string.Empty;
        public string Arrival { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
    }

    public class TransportStation
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
    }

    // API Response models for OpenData CH Transport
    public class TransportApiResponse
    {
        public List<TransportApiConnection> Connections { get; set; } = new();
    }

    public class TransportApiConnection
    {
        public TransportApiCheckpoint? From { get; set; }
        public TransportApiCheckpoint? To { get; set; }
        public string Duration { get; set; } = string.Empty;
        public List<TransportApiSection> Sections { get; set; } = new();
    }

    public class TransportApiCheckpoint
    {
        public TransportApiStation? Station { get; set; }
        public string Departure { get; set; } = string.Empty;
        public string Arrival { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;
    }

    public class TransportApiStation
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
    }

    public class TransportApiSection
    {
        public TransportApiJourney? Journey { get; set; }
        public TransportApiCheckpoint? Departure { get; set; }
        public TransportApiCheckpoint? Arrival { get; set; }
    }

    public class TransportApiJourney
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Number { get; set; } = string.Empty;
    }

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