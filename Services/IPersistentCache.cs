namespace ShiftScheduler.Services
{
    public interface IPersistentCache
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class;
        Task RemoveAsync(string key);
        Task ClearExpiredAsync();
    }
}