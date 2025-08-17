using ShiftScheduler.Shared.Models;
using System.Text.Json;

namespace ShiftScheduler.Services
{
    public class TransportService
    {
        private readonly HttpClient _httpClient;
        private readonly TransportConfiguration _config;

        public TransportService(HttpClient httpClient, TransportConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<TransportConnection?> GetConnectionAsync(DateTime arrivalTime, string? customEndStation = null)
        {
            try
            {
                var endStation = customEndStation ?? _config.EndStation;
                var arrivalTimeStr = arrivalTime.ToString("HH:mm");
                
                // Calculate departure time (assume 30 minutes before arrival for safety)
                var searchTime = arrivalTime.AddMinutes(-30);
                var searchTimeStr = searchTime.ToString("yyyy-MM-dd");
                var searchHourStr = searchTime.ToString("HH:mm");

                var url = $"{_config.ApiBaseUrl}/connections?from={Uri.EscapeDataString(_config.StartStation)}&to={Uri.EscapeDataString(endStation)}&date={searchTimeStr}&time={searchHourStr}&limit=3";

                var response = await _httpClient.GetStringAsync(url);
                var apiResponse = JsonSerializer.Deserialize<TransportApiResponse>(response, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (apiResponse?.Connections?.Count > 0)
                {
                    // Find the best connection that arrives before the desired time
                    var bestConnection = FindBestConnection(apiResponse.Connections, arrivalTime);
                    return MapToTransportConnection(bestConnection);
                }

                return null;
            }
            catch (Exception ex)
            {
                // Log the exception and return a mock connection for demonstration
                Console.WriteLine($"Error fetching transport connection: {ex.Message}");
                return CreateMockConnection(arrivalTime);
            }
        }

        private TransportConnection CreateMockConnection(DateTime arrivalTime)
        {
            // Create a realistic mock connection for demonstration
            var departureTime = arrivalTime.AddMinutes(-45); // 45 minutes journey time
            
            return new TransportConnection
            {
                DepartureTime = departureTime.ToString("HH:mm"),
                ArrivalTime = arrivalTime.ToString("HH:mm"),
                Duration = "00:45:00",
                Platform = "3",
                Sections = new List<TransportSection>
                {
                    new TransportSection
                    {
                        Journey = new TransportJourney
                        {
                            Name = "S1",
                            Category = "S",
                            Number = "1"
                        },
                        Departure = new TransportCheckpoint
                        {
                            Station = new TransportStation
                            {
                                Name = _config.StartStation,
                                Id = "start"
                            },
                            Departure = departureTime.ToString("HH:mm"),
                            Platform = "3"
                        },
                        Arrival = new TransportCheckpoint
                        {
                            Station = new TransportStation
                            {
                                Name = _config.EndStation,
                                Id = "end"
                            },
                            Arrival = arrivalTime.ToString("HH:mm"),
                            Platform = "1"
                        }
                    }
                }
            };
        }

        private TransportApiConnection? FindBestConnection(List<TransportApiConnection> connections, DateTime targetArrival)
        {
            foreach (var connection in connections)
            {
                if (DateTime.TryParse(connection.To?.Arrival, out var arrivalTime))
                {
                    // Find a connection that arrives before or at the target time
                    if (arrivalTime.TimeOfDay <= targetArrival.TimeOfDay)
                    {
                        return connection;
                    }
                }
            }

            // If no connection arrives before target, return the first one
            return connections.FirstOrDefault();
        }

        private TransportConnection MapToTransportConnection(TransportApiConnection? apiConnection)
        {
            if (apiConnection == null)
                return new TransportConnection();

            return new TransportConnection
            {
                DepartureTime = apiConnection.From?.Departure ?? string.Empty,
                ArrivalTime = apiConnection.To?.Arrival ?? string.Empty,
                Duration = apiConnection.Duration ?? string.Empty,
                Platform = apiConnection.From?.Platform ?? string.Empty,
                Sections = apiConnection.Sections?.Select(s => new TransportSection
                {
                    Journey = s.Journey != null ? new TransportJourney
                    {
                        Name = s.Journey.Name ?? string.Empty,
                        Category = s.Journey.Category ?? string.Empty,
                        Number = s.Journey.Number ?? string.Empty
                    } : null,
                    Departure = s.Departure != null ? new TransportCheckpoint
                    {
                        Station = s.Departure.Station != null ? new TransportStation
                        {
                            Name = s.Departure.Station.Name ?? string.Empty,
                            Id = s.Departure.Station.Id ?? string.Empty
                        } : null,
                        Departure = s.Departure.Departure ?? string.Empty,
                        Arrival = s.Departure.Arrival ?? string.Empty,
                        Platform = s.Departure.Platform ?? string.Empty
                    } : null,
                    Arrival = s.Arrival != null ? new TransportCheckpoint
                    {
                        Station = s.Arrival.Station != null ? new TransportStation
                        {
                            Name = s.Arrival.Station.Name ?? string.Empty,
                            Id = s.Arrival.Station.Id ?? string.Empty
                        } : null,
                        Departure = s.Arrival.Departure ?? string.Empty,
                        Arrival = s.Arrival.Arrival ?? string.Empty,
                        Platform = s.Arrival.Platform ?? string.Empty
                    } : null
                }).ToList() ?? new List<TransportSection>()
            };
        }

        public string FormatConnectionSummary(TransportConnection? connection)
        {
            if (connection == null || string.IsNullOrEmpty(connection.DepartureTime))
                return "No transport info";

            var departure = DateTime.TryParse(connection.DepartureTime, out var dep) ? dep.ToString("HH:mm") : connection.DepartureTime;
            var arrival = DateTime.TryParse(connection.ArrivalTime, out var arr) ? arr.ToString("HH:mm") : connection.ArrivalTime;
            
            var mainJourney = connection.Sections?.FirstOrDefault()?.Journey;
            var trainInfo = mainJourney != null ? $"{mainJourney.Category} {mainJourney.Number}" : "Train";

            return $"{trainInfo}: {departure} → {arrival}";
        }
    }
}