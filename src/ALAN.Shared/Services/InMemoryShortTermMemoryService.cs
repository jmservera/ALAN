using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ALAN.Shared.Services.Memory;

/// <summary>
/// In-memory implementation of short-term memory service.
/// Can be replaced with Redis Cache for production.
/// </summary>
public class InMemoryShortTermMemoryService : IShortTermMemoryService
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly ILogger<InMemoryShortTermMemoryService> _logger;

    public InMemoryShortTermMemoryService(ILogger<InMemoryShortTermMemoryService> logger)
    {
        _logger = logger;
        _logger.LogInformation("InMemory Short-Term Memory Service initialized");
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var entry = new CacheEntry
        {
            Value = JsonSerializer.Serialize(value),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : null
        };

        _cache[key] = entry;
        _logger.LogTrace("Set cache key {Key} with expiration {Expiration}", key, expiration);
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            // Check if expired
            if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
            {
                _cache.TryRemove(key, out _);
                _logger.LogTrace("Cache key {Key} expired and removed", key);
                return Task.FromResult<T?>(default);
            }

            try
            {
                var value = JsonSerializer.Deserialize<T>(entry.Value);
                return Task.FromResult(value);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Failed to deserialize cache key {Key}", key);
                return Task.FromResult<T?>(default);
            }
        }

        return Task.FromResult<T?>(default);
    }

    public Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        var removed = _cache.TryRemove(key, out _);
        if (removed)
        {
            _logger.LogTrace("Deleted cache key {Key}", key);
        }
        return Task.FromResult(removed);
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        var exists = _cache.ContainsKey(key);
        return Task.FromResult(exists);
    }

    public Task<List<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default)
    {
        // Simple pattern matching (only supports * wildcard)
        var keys = pattern == "*"
            ? _cache.Keys.ToList()
            : _cache.Keys.Where(k => MatchesPattern(k, pattern)).ToList();

        return Task.FromResult(keys);
    }

    private bool MatchesPattern(string key, string pattern)
    {
        if (pattern == "*") return true;
        
        // Simple wildcard matching
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
            .Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(key, regexPattern);
    }

    private class CacheEntry
    {
        public string Value { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
