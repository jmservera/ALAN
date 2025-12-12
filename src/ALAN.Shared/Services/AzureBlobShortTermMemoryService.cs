using ALAN.Shared.Models;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ALAN.Shared.Services.Memory;

/// <summary>
/// Azure Blob Storage implementation of short-term memory service.
/// Stores short-lived cache entries as JSON blobs in Azure Storage.
/// </summary>
public class AzureBlobShortTermMemoryService : IShortTermMemoryService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobShortTermMemoryService> _logger;
    private const string ContainerName = "agent-cache";
    private bool _isInitialized = false;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AzureBlobShortTermMemoryService(
        string connectionString,
        ILogger<AzureBlobShortTermMemoryService> logger)
    {
        _logger = logger;
        
        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            _logger.LogInformation("Azure Blob Short-Term Memory Service created with container: {ContainerName}", ContainerName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure Blob Storage client. This service will not be functional.");
            _containerClient = null!;
        }
    }

    private async Task<bool> EnsureInitializedAsync(CancellationToken cancellationToken = default)
    {
        if (_isInitialized) return true;
        if (_containerClient == null) return false;

        try
        {
            await _containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            _isInitialized = true;
            _logger.LogInformation("Azure Blob container '{ContainerName}' is ready", ContainerName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Blob container. Cache operations will be skipped.");
            return false;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            _logger.LogWarning("Azure Blob Storage not initialized. Skipping cache set for {Key}", key);
            return;
        }

        var blobName = NormalizeBlobName(key);
        var blobClient = _containerClient.GetBlobClient(blobName);

        var cacheEntry = new CacheEntry
        {
            Value = JsonSerializer.Serialize(value, JsonOptions),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiration.HasValue ? DateTime.UtcNow.Add(expiration.Value) : null
        };

        var json = JsonSerializer.Serialize(cacheEntry, JsonOptions);
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        var metadata = new Dictionary<string, string>
        {
            ["created"] = cacheEntry.CreatedAt.ToString("o")
        };

        if (cacheEntry.ExpiresAt.HasValue)
        {
            metadata["expires"] = cacheEntry.ExpiresAt.Value.ToString("o");
        }

        try
        {
            // Upload blob with overwrite
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
            };
            await blobClient.UploadAsync(stream, uploadOptions, cancellationToken);
            
            // Set metadata after upload
            await blobClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
            
            _logger.LogTrace("Set cache key {Key} in blob {BlobName} with expiration {Expiration}", key, blobName, expiration);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to set cache key {Key} in Azure Blob Storage", key);
        }
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            _logger.LogWarning("Azure Blob Storage not initialized. Cannot retrieve cache key {Key}", key);
            return default;
        }

        var blobName = NormalizeBlobName(key);
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            if (!await blobClient.ExistsAsync(cancellationToken))
            {
                return default;
            }

            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var json = response.Value.Content.ToString();
            var cacheEntry = JsonSerializer.Deserialize<CacheEntry>(json, JsonOptions);

            if (cacheEntry == null)
            {
                return default;
            }

            // Check if expired
            if (cacheEntry.ExpiresAt.HasValue && cacheEntry.ExpiresAt.Value < DateTime.UtcNow)
            {
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
                _logger.LogTrace("Cache key {Key} expired and removed", key);
                return default;
            }

            var value = JsonSerializer.Deserialize<T>(cacheEntry.Value, JsonOptions);
            return value;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to deserialize cache key {Key}", key);
            return default;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get cache key {Key} from Azure Blob Storage", key);
            return default;
        }
    }

    public async Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            _logger.LogWarning("Azure Blob Storage not initialized. Cannot delete cache key {Key}", key);
            return false;
        }

        var blobName = NormalizeBlobName(key);
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            var response = await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            if (response.Value)
            {
                _logger.LogTrace("Deleted cache key {Key}", key);
            }
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete cache key {Key} from Azure Blob Storage", key);
            return false;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            _logger.LogWarning("Azure Blob Storage not initialized. Cannot check existence of cache key {Key}", key);
            return false;
        }

        var blobName = NormalizeBlobName(key);
        var blobClient = _containerClient.GetBlobClient(blobName);

        try
        {
            var response = await blobClient.ExistsAsync(cancellationToken);
            return response.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check existence of cache key {Key} in Azure Blob Storage", key);
            return false;
        }
    }

    public async Task<List<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            _logger.LogWarning("Azure Blob Storage not initialized. Cannot list cache keys");
            return new List<string>();
        }

        var keys = new List<string>();

        try
        {
            await foreach (var blob in _containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                // Remove .json extension and denormalize blob name
                var key = DenormalizeBlobName(blob.Name);
                
                // Simple pattern matching (only supports * wildcard)
                if (pattern == "*" || MatchesPattern(key, pattern))
                {
                    keys.Add(key);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list cache keys from Azure Blob Storage");
        }

        return keys;
    }

    public async Task<List<MemoryEntry>> GetRecentMemoriesAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            _logger.LogWarning("Azure Blob Storage not initialized. Cannot retrieve recent memories");
            return [];
        }

        var keys = await GetKeysAsync("memory:*", cancellationToken);
        if (keys.Count == 0)
        {
            return [];
        }

        var memories = new List<MemoryEntry>();

        foreach (var key in keys)
        {
            try
            {
                var memory = await GetAsync<MemoryEntry>(key, cancellationToken);
                if (memory != null)
                {
                    memories.Add(memory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize memory from short-term key {Key}", key);
            }
        }

        return memories
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToList();
    }

    /// <summary>
    /// Normalize key to valid blob name (lowercase, replace invalid chars).
    /// </summary>
    private string NormalizeBlobName(string key)
    {
        // Replace invalid characters with hyphens and convert to lowercase
        var normalized = key
            .Replace(":", "-")
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace(" ", "-")
            .ToLowerInvariant();
        
        return $"{normalized}.json";
    }

    /// <summary>
    /// Reverse the normalization to get original key.
    /// </summary>
    private string DenormalizeBlobName(string blobName)
    {
        // Remove .json extension
        if (blobName.EndsWith(".json"))
        {
            blobName = blobName[..^5];
        }
        
        // Note: This is a simple implementation. For production, consider storing 
        // the original key in blob metadata for accurate reverse mapping.
        return blobName.Replace("-", ":");
    }

    /// <summary>
    /// Simple pattern matching supporting * wildcard.
    /// </summary>
    private bool MatchesPattern(string key, string pattern)
    {
        if (pattern == "*") return true;
        
        // Simple implementation: convert * to regex
        var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern).Replace("\\*", ".*") + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(key, regexPattern);
    }

    private class CacheEntry
    {
        public string Value { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
