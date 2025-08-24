using ShiftScheduler.Shared;

namespace ShiftScheduler.Services
{
    public class CachedTransportService(ITransportApiService transportService, TransportConfiguration config, IPersistentCache cache) : ITransportService
    {
        public async Task<TransportConnection?> GetConnectionAsync(DateTime shiftStartTime)
        {
            var searchDate = shiftStartTime.ToString("yyyy-MM-dd");
            var searchTime = shiftStartTime.AddMinutes(config.MaxLateArrivalMinutes).ToString("HH:mm");

            // Generate cache key based on request parameters
            var cacheKey = GenerateCacheKey(searchDate, searchTime);
            
            // Try to get from cache first
            var cachedConnection = await cache.GetAsync<TransportConnection>(cacheKey);
            if (cachedConnection != null)
            {
                return cachedConnection;
            }

            // Not in cache, call the underlying transport service
            var connection = await transportService.GetConnectionAsync(shiftStartTime);

            // Cache the connection result if valid
            if (connection != null)
            {
                await cache.SetAsync(cacheKey, connection, TimeSpan.FromDays(config.CacheDurationDays));
            }

            return connection;
        }

        private string GenerateCacheKey(string searchDate, string searchTime)
        {
            return $"transport_{config.StartStation}_{config.EndStation}_{searchDate}_{searchTime}";
        }
    }
}