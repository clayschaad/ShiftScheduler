using System.Text.Json;
using Microsoft.Extensions.Logging;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services
{
    public class TransportApiService(HttpClient httpClient, IConfigurationService configurationService, ILogger<TransportApiService> logger) : ITransportApiService
    {
        public async Task<TransportConnection?> GetConnectionAsync(DateTime shiftStartTime)
        {
            var config = configurationService.GetTransportConfiguration();
            
            // To allow connections that arrive after shift starts, we search from earlier time
            // and request more connections to cover the full range
            var searchDate = shiftStartTime.ToString("yyyy-MM-dd");
            var searchTime = shiftStartTime.AddMinutes(config.MaxLateArrivalMinutes).ToString("HH:mm");

            var url = $"{config.ApiBaseUrl}/connections?from={Uri.EscapeDataString(config.StartStation)}&to={Uri.EscapeDataString(config.EndStation)}&date={searchDate}&time={searchTime}&isArrivalTime=1&limit=5";
            var response = await httpClient.GetStringAsync(url);
            var apiResponse = JsonSerializer.Deserialize<TransportApiResponse>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (apiResponse?.Connections.Count > 0)
            {
                var allConnections = apiResponse.Connections.Select(MapToTransportConnection).ToList();

                foreach (var connection in allConnections)
                {
                    logger.LogDebug($"Found connection {connection}");
                }
                
                var bestConnection = TransportConnectionCalculator.FindBestConnection(
                    allConnections, new ConnectionPickArgument(shiftStartTime, config.SafetyBufferMinutes, config.MaxEarlyArrivalMinutes, config.MaxLateArrivalMinutes), logger);
                
                return bestConnection;
            }

            return null;
        }

        private TransportConnection MapToTransportConnection(TransportApiConnection? apiConnection)
        {
            if (apiConnection == null)
                return new TransportConnection();

            return new TransportConnection
            {
                DepartureTime = apiConnection.From?.Departure ?? string.Empty,
                ArrivalTime = apiConnection.To?.Arrival ?? string.Empty,
                Duration = apiConnection.Duration,
                Platform = apiConnection.From?.Platform ?? string.Empty,
            };
        }
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

        public override string ToString()
        {
            return $"{From} - {To}";
        }
    }

    public class TransportApiCheckpoint
    {
        public TransportApiStation? Station { get; set; }
        public string Departure { get; set; } = string.Empty;
        public string Arrival { get; set; } = string.Empty;
        public string Platform { get; set; } = string.Empty;

        public override string ToString()
        {
            return $"{Station?.Name}: {Departure} - {Arrival}";
        }
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
}