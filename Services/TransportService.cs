using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services
{
    public class TransportService(HttpClient httpClient, TransportConfiguration config, IMemoryCache cache)
    {
        public async Task<TransportConnection?> GetConnectionAsync(DateTime shiftStartTime)
        {
            var latestArrivalTime = shiftStartTime.AddMinutes(-config.SafetyBufferMinutes);
            var searchDate = shiftStartTime.ToString("yyyy-MM-dd");
            
            // To allow connections that arrive after shift starts, we search from earlier time
            // and request more connections to cover the full range
            var searchTime = shiftStartTime.AddMinutes(config.MaxLateArrivalMinutes).ToString("HH:mm");

            // Generate cache key based on request parameters
            var cacheKey = GenerateCacheKey(searchDate, searchTime);
            
            // Try to get from cache first
            if (cache.TryGetValue(cacheKey, out TransportApiResponse? cachedResponse) && cachedResponse != null)
            {
                return ProcessApiResponse(cachedResponse, shiftStartTime);
            }

            // Not in cache, make API call
            var url = $"{config.ApiBaseUrl}/connections?from={Uri.EscapeDataString(config.StartStation)}&to={Uri.EscapeDataString(config.EndStation)}&date={searchDate}&time={searchTime}&isArrivalTime=1&limit=5";
            var response = await httpClient.GetStringAsync(url);
            var apiResponse = JsonSerializer.Deserialize<TransportApiResponse>(response, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            // Cache the response if valid
            if (apiResponse != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(config.CacheDurationMinutes)
                };
                cache.Set(cacheKey, apiResponse, cacheOptions);
            }

            return ProcessApiResponse(apiResponse, shiftStartTime);
        }

        private string GenerateCacheKey(string searchDate, string searchTime)
        {
            return $"transport_{config.StartStation}_{config.EndStation}_{searchDate}_{searchTime}";
        }

        private TransportConnection? ProcessApiResponse(TransportApiResponse? apiResponse, DateTime shiftStartTime)
        {
            if (apiResponse?.Connections.Count > 0)
            {
                var allConnections = apiResponse.Connections.Select(MapToTransportConnection).ToList();
                return TransportConnectionCalculator.FindBestConnection(
                    allConnections, 
                    shiftStartTime, 
                    config.SafetyBufferMinutes, 
                    config.MaxEarlyArrivalMinutes, 
                    config.MaxLateArrivalMinutes);
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
                Sections = apiConnection.Sections.Select(s => new TransportSection
                {
                    Journey = s.Journey != null ? new TransportJourney
                    {
                        Name = s.Journey.Name,
                        Category = s.Journey.Category,
                        Number = s.Journey.Number
                    } : null,
                    Departure = s.Departure != null ? new TransportCheckpoint
                    {
                        Station = s.Departure.Station != null ? new TransportStation
                        {
                            Name = s.Departure.Station.Name,
                            Id = s.Departure.Station.Id
                        } : null,
                        Departure = s.Departure.Departure,
                        Arrival = s.Departure.Arrival,
                        Platform = s.Departure.Platform
                    } : null,
                    Arrival = s.Arrival != null ? new TransportCheckpoint
                    {
                        Station = s.Arrival.Station != null ? new TransportStation
                        {
                            Name = s.Arrival.Station.Name,
                            Id = s.Arrival.Station.Id
                        } : null,
                        Departure = s.Arrival.Departure,
                        Arrival = s.Arrival.Arrival,
                        Platform = s.Arrival.Platform
                    } : null
                }).ToList()
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