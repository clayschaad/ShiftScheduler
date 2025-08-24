using System.Text.Json;

namespace ShiftScheduler.Services
{
    public class FilePersistentCache : IPersistentCache
    {
        private readonly string _cacheDirectory;
        private readonly string _indexFilePath;
        private readonly object _lockObject = new object();

        public FilePersistentCache(string cacheDirectory = "cache")
        {
            _cacheDirectory = Path.Combine(Directory.GetCurrentDirectory(), cacheDirectory);
            _indexFilePath = Path.Combine(_cacheDirectory, "cache_index.json");
            EnsureCacheDirectoryExists();
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                var cacheFilePath = GetCacheFilePath(key);
                if (!File.Exists(cacheFilePath))
                    return null;

                var cacheEntryJson = await File.ReadAllTextAsync(cacheFilePath);
                var cacheEntry = JsonSerializer.Deserialize<CacheEntry>(cacheEntryJson);

                if (cacheEntry == null || cacheEntry.ExpiresAt < DateTime.UtcNow)
                {
                    // Cache entry expired, remove it
                    await RemoveAsync(key);
                    return null;
                }

                return JsonSerializer.Deserialize<T>(cacheEntry.Value);
            }
            catch
            {
                // If there's any error reading the cache, return null
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration) where T : class
        {
            try
            {
                var cacheEntry = new CacheEntry
                {
                    Key = key,
                    Value = JsonSerializer.Serialize(value),
                    ExpiresAt = DateTime.UtcNow.Add(expiration),
                    CreatedAt = DateTime.UtcNow
                };

                var cacheFilePath = GetCacheFilePath(key);
                var cacheEntryJson = JsonSerializer.Serialize(cacheEntry);
                
                lock (_lockObject)
                {
                    EnsureCacheDirectoryExists();
                    File.WriteAllText(cacheFilePath, cacheEntryJson);
                }

                await UpdateIndexAsync(key, cacheEntry.ExpiresAt);
            }
            catch
            {
                // Fail silently if cache write fails
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                var cacheFilePath = GetCacheFilePath(key);
                if (File.Exists(cacheFilePath))
                {
                    File.Delete(cacheFilePath);
                }
                await RemoveFromIndexAsync(key);
            }
            catch
            {
                // Fail silently if removal fails
            }
        }

        public async Task ClearExpiredAsync()
        {
            try
            {
                var index = await LoadIndexAsync();
                var expiredKeys = index.Where(kvp => kvp.Value < DateTime.UtcNow).Select(kvp => kvp.Key).ToList();

                foreach (var key in expiredKeys)
                {
                    await RemoveAsync(key);
                }
            }
            catch
            {
                // Fail silently if cleanup fails
            }
        }

        private string GetCacheFilePath(string key)
        {
            var safeKey = key.Replace(':', '_').Replace('/', '_').Replace('\\', '_');
            return Path.Combine(_cacheDirectory, $"{safeKey}.json");
        }

        private void EnsureCacheDirectoryExists()
        {
            if (!Directory.Exists(_cacheDirectory))
            {
                Directory.CreateDirectory(_cacheDirectory);
            }
        }

        private async Task UpdateIndexAsync(string key, DateTime expiresAt)
        {
            try
            {
                var index = await LoadIndexAsync();
                index[key] = expiresAt;
                
                var indexJson = JsonSerializer.Serialize(index);
                await File.WriteAllTextAsync(_indexFilePath, indexJson);
            }
            catch
            {
                // Fail silently if index update fails
            }
        }

        private async Task RemoveFromIndexAsync(string key)
        {
            try
            {
                var index = await LoadIndexAsync();
                index.Remove(key);
                
                var indexJson = JsonSerializer.Serialize(index);
                await File.WriteAllTextAsync(_indexFilePath, indexJson);
            }
            catch
            {
                // Fail silently if index update fails
            }
        }

        private async Task<Dictionary<string, DateTime>> LoadIndexAsync()
        {
            try
            {
                if (!File.Exists(_indexFilePath))
                    return new Dictionary<string, DateTime>();

                var indexJson = await File.ReadAllTextAsync(_indexFilePath);
                return JsonSerializer.Deserialize<Dictionary<string, DateTime>>(indexJson) ?? new Dictionary<string, DateTime>();
            }
            catch
            {
                return new Dictionary<string, DateTime>();
            }
        }

        private class CacheEntry
        {
            public string Key { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
            public DateTime ExpiresAt { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}