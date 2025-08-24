using Microsoft.Extensions.Caching.Memory;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services
{
    public class CachedTransportService(ITransportApiService transportService, TransportConfiguration config, IMemoryCache cache) : ITransportService
    {
        public async Task<TransportConnection?> GetConnectionAsync(DateTime shiftStartTime)
        {
            var searchDate = shiftStartTime.ToString("yyyy-MM-dd");
            var searchTime = shiftStartTime.AddMinutes(config.MaxLateArrivalMinutes).ToString("HH:mm");

            // Generate cache key based on request parameters
            var cacheKey = GenerateCacheKey(searchDate, searchTime);
            
            // Try to get from cache first
            if (cache.TryGetValue(cacheKey, out TransportConnection? cachedConnection) && cachedConnection != null)
            {
                return cachedConnection;
            }

            // Not in cache, call the underlying transport service
            var connection = await transportService.GetConnectionAsync(shiftStartTime);

            // Cache the connection result if valid
            if (connection != null)
            {
                var cacheOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(config.CacheDurationDays)
                };
                cache.Set(cacheKey, connection, cacheOptions);
            }

            return connection;
        }

        private string GenerateCacheKey(string searchDate, string searchTime)
        {
            return $"transport_{config.StartStation}_{config.EndStation}_{searchDate}_{searchTime}";
        }
    }
}