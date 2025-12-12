using ALAN.Shared.Models;
using ALAN.Shared.Services.Resilience;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;

namespace ALAN.Shared.Services.Memory;

/// <summary>
/// Azure Blob Storage implementation of long-term memory service.
/// Stores memories as JSON blobs in Azure Storage with metadata for searching.
/// </summary>
public class AzureBlobLongTermMemoryService : ILongTermMemoryService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<AzureBlobLongTermMemoryService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private const string ContainerName = "agent-memories";
    private bool _isInitialized = false;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public AzureBlobLongTermMemoryService(
        string connectionString,
        ILogger<AzureBlobLongTermMemoryService> logger)
    {
        _logger = logger;
        _resiliencePipeline = ResiliencePolicy.CreateStorageRetryPipeline(logger);

        try
        {
            var blobServiceClient = new BlobServiceClient(connectionString);
            _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
            _logger.LogInformation("Azure Blob Long-Term Memory Service created with container: {ContainerName}", ContainerName);
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
            await _resiliencePipeline.ExecuteAsync(async ct =>
                await _containerClient.CreateIfNotExistsAsync(cancellationToken: ct),
                cancellationToken);
            _isInitialized = true;
            _logger.LogInformation("Azure Blob container '{ContainerName}' is ready", ContainerName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure Blob container. Memory operations will be skipped.");
            return false;
        }
    }

    public async Task<string> StoreMemoryAsync(MemoryEntry memory, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            _logger.LogWarning("Azure Blob Storage not initialized. Skipping memory storage for {Id}", memory.Id);
            return memory.Id;
        }

        var blobName = $"{memory.Timestamp:yyyy/MM/dd}/{memory.Id}.json";
        var blobClient = _containerClient.GetBlobClient(blobName);

        var json = JsonSerializer.Serialize(memory, new JsonSerializerOptions { WriteIndented = true });
        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));

        // Set metadata for searching
        var metadata = new Dictionary<string, string>
        {
            ["type"] = memory.Type.ToString(),
            ["importance"] = memory.Importance.ToString("F2"),
            ["timestamp"] = memory.Timestamp.ToString("o"),
            ["summary"] = TruncateForMetadata(memory.Summary, 100)
        };

        // Add tags as metadata (Azure Blob metadata keys must be valid identifiers)
        for (int i = 0; i < Math.Min(memory.Tags.Count, 5); i++)
        {
            metadata[$"tag{i}"] = TruncateForMetadata(memory.Tags[i], 50);
        }

        try
        {
            // Upload blob with overwrite
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
            };
            await _resiliencePipeline.ExecuteAsync(async ct =>
                await blobClient.UploadAsync(stream, uploadOptions, ct),
                cancellationToken);

            // Set metadata after upload
            await _resiliencePipeline.ExecuteAsync(async ct =>
                await blobClient.SetMetadataAsync(metadata, cancellationToken: ct),
                cancellationToken);

            _logger.LogDebug("Stored memory {Id} of type {Type} to blob {BlobName}", memory.Id, memory.Type, blobName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload memory {Id} to Azure Blob Storage", memory.Id);
        }

        return memory.Id;
    }

    public async Task<MemoryEntry?> GetMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            _logger.LogWarning("Azure Blob Storage not initialized. Cannot retrieve memory {Id}", id);
            return null;
        }

        try
        {
            // Note: Without an external index, we need to search for the blob
            // For better performance in production, consider:
            // 1. Maintaining an Azure Table Storage index of ID -> blob path
            // 2. Using a consistent naming scheme that includes the memory ID
            // 3. Adding a metadata tag for memory ID and using Azure Blob Index tags

            // For now, we search recent dates first (most likely location)
            var now = DateTime.UtcNow;

            // Search recent 30 days first (90% of lookups likely here)
            for (int daysBack = 0; daysBack < 30; daysBack++)
            {
                var date = now.AddDays(-daysBack);
                var blobName = $"{date:yyyy/MM/dd}/{id}.json";
                var blobClient = _containerClient.GetBlobClient(blobName);

                var exists = await _resiliencePipeline.ExecuteAsync(async ct =>
                    await blobClient.ExistsAsync(ct),
                    cancellationToken);
                
                if (exists)
                {
                    var response = await _resiliencePipeline.ExecuteAsync(async ct =>
                        await blobClient.DownloadContentAsync(ct),
                        cancellationToken);
                    var json = response.Value.Content.ToString();
                    var memory = JsonSerializer.Deserialize<MemoryEntry>(json, JsonOptions);

                    if (memory != null)
                    {
                        await UpdateMemoryAccessAsync(id, cancellationToken);
                    }

                    return memory;
                }
            }

            // If not found in recent 30 days, log warning
            _logger.LogWarning("Memory {Id} not found in recent 30 days (consider extending search if needed)", id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving memory {Id} from Azure Blob Storage", id);
            return null;
        }
    }

    public async Task<List<MemoryEntry>> SearchMemoriesAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            _logger.LogWarning("Azure Blob Storage not initialized. Returning empty search results");
            return new List<MemoryEntry>();
        }

        try
        {
            var results = new List<MemoryEntry>();
            var queryLower = query.ToLowerInvariant();

            // Search recent blobs (last 90 days) using date-based iteration
            var now = DateTime.UtcNow;
            var cutoffDate = now.AddDays(-90);

            // Iterate through days from most recent to cutoff
            for (var date = now.Date; date >= cutoffDate.Date && results.Count < maxResults; date = date.AddDays(-1))
            {
                var prefix = $"{date:yyyy/MM/dd}/";

                await foreach (var blobItem in _containerClient.GetBlobsAsync(
                    prefix: prefix,
                    traits: BlobTraits.Metadata,
                    cancellationToken: cancellationToken))
                {
                    if (results.Count >= maxResults) break;

                    // Check if metadata matches query
                    bool matches = false;
                    if (blobItem.Metadata != null)
                    {
                        matches = blobItem.Metadata.Values.Any(v => v.ToLowerInvariant().Contains(queryLower));
                    }

                    if (matches || blobItem.Name.Contains(queryLower, StringComparison.OrdinalIgnoreCase))
                    {
                        var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                        try
                        {
                            var response = await blobClient.DownloadContentAsync(cancellationToken);
                            var json = response.Value.Content.ToString();
                            var memory = JsonSerializer.Deserialize<MemoryEntry>(json, JsonOptions);

                            if (memory != null &&
                                (memory.Content.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                                 memory.Summary.Contains(queryLower, StringComparison.OrdinalIgnoreCase) ||
                                 memory.Tags.Any(t => t.Contains(queryLower, StringComparison.OrdinalIgnoreCase))))
                            {
                                results.Add(memory);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize memory from blob {BlobName}", blobItem.Name);
                        }
                    }
                }
            }

            _logger.LogDebug("Search for '{Query}' returned {Count} results", query, results.Count);
            return results.OrderByDescending(m => m.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching memories in Azure Blob Storage");
            return new List<MemoryEntry>();
        }
    }

    public async Task<List<MemoryEntry>> GetRecentMemoriesAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            return new List<MemoryEntry>();
        }

        try
        {
            var results = new List<MemoryEntry>();

            // Use date-based prefix to only fetch recent blobs (last 30 days)
            var now = DateTime.UtcNow;
            for (int daysBack = 0; daysBack < 30 && results.Count < count; daysBack++)
            {
                var date = now.AddDays(-daysBack);
                var prefix = $"{date:yyyy/MM/dd}/";

                await foreach (var blobItem in _containerClient.GetBlobsAsync(
                    prefix: prefix,
                    traits: BlobTraits.Metadata,
                    cancellationToken: cancellationToken))
                {
                    if (results.Count >= count) break;

                    var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                    try
                    {
                        var response = await blobClient.DownloadContentAsync(cancellationToken);
                        var json = response.Value.Content.ToString();
                        var memory = JsonSerializer.Deserialize<MemoryEntry>(json, JsonOptions);

                        if (memory != null)
                        {
                            results.Add(memory);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to deserialize memory from blob {BlobName}", blobItem.Name);
                    }
                }
            }

            return results.OrderByDescending(m => m.Timestamp).Take(count).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recent memories");
            return new List<MemoryEntry>();
        }
    }

    public async Task<bool> DeleteMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            return false;
        }

        try
        {
            // Search through recent dates to find and delete the blob (limit to 30 days)
            var now = DateTime.UtcNow;
            for (int daysBack = 0; daysBack < 30; daysBack++)
            {
                var date = now.AddDays(-daysBack);
                var blobName = $"{date:yyyy/MM/dd}/{id}.json";
                var blobClient = _containerClient.GetBlobClient(blobName);

                var exists = await _resiliencePipeline.ExecuteAsync(async ct =>
                    await blobClient.ExistsAsync(ct),
                    cancellationToken);
                
                if (exists)
                {
                    await _resiliencePipeline.ExecuteAsync(async ct =>
                        await blobClient.DeleteAsync(cancellationToken: ct),
                        cancellationToken);
                    _logger.LogInformation("Deleted memory {Id}", id);
                    return true;
                }
            }

            _logger.LogWarning("Memory {Id} not found for deletion in recent 30 days", id);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete memory {Id}", id);
            return false;
        }
    }

    public async Task<int> GetMemoryCountAsync(CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            return 0;
        }

        try
        {
            // Note: This counts memories from the last 90 days only for performance
            // To count all memories across all time, remove the date filtering
            int count = 0;
            var now = DateTime.UtcNow;
            var cutoffDate = now.AddDays(-90);

            for (var date = now.Date; date >= cutoffDate.Date; date = date.AddDays(-1))
            {
                var prefix = $"{date:yyyy/MM/dd}/";
                await foreach (var _ in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
                {
                    count++;
                }
            }

            _logger.LogDebug("Memory count (last 90 days): {Count}", count);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory count");
            return 0;
        }
    }

    public async Task<List<MemoryEntry>> GetMemoriesByTypeAsync(MemoryType type, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            return new List<MemoryEntry>();
        }

        try
        {
            var results = new List<MemoryEntry>();
            var typeString = type.ToString();

            // Use date-based filtering for performance (last 90 days)
            var now = DateTime.UtcNow;
            var cutoffDate = now.AddDays(-90);

            for (var date = now.Date; date >= cutoffDate.Date && results.Count < maxResults; date = date.AddDays(-1))
            {
                var prefix = $"{date:yyyy/MM/dd}/";

                await foreach (var blobItem in _containerClient.GetBlobsAsync(
                    prefix: prefix,
                    traits: BlobTraits.Metadata,
                    cancellationToken: cancellationToken))
                {
                    if (results.Count >= maxResults) break;

                    if (blobItem.Metadata != null &&
                        blobItem.Metadata.TryGetValue("type", out var metadataType) &&
                        metadataType == typeString)
                    {
                        var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                        try
                        {
                            var response = await blobClient.DownloadContentAsync(cancellationToken);
                            var json = response.Value.Content.ToString();
                            var memory = JsonSerializer.Deserialize<MemoryEntry>(json, JsonOptions);

                            if (memory != null && memory.Type == type)
                            {
                                results.Add(memory);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to deserialize memory from blob {BlobName}", blobItem.Name);
                        }
                    }
                }
            }

            return results.OrderByDescending(m => m.Timestamp).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memories by type {Type}", type);
            return new List<MemoryEntry>();
        }
    }

    public async Task UpdateMemoryAccessAsync(string id, CancellationToken cancellationToken = default)
    {
        if (!await EnsureInitializedAsync(cancellationToken))
        {
            return;
        }

        // Note: Azure Blob Storage doesn't support updating content without re-uploading
        // For access tracking, we would need to either:
        // 1. Re-upload the blob with updated content
        // 2. Use a separate index/database for access tracking
        // 3. Use blob metadata (has limitations)

        // For now, we'll log the access but not persist it to avoid re-uploading
        _logger.LogTrace("Memory {Id} accessed (not persisted to storage)", id);
        await Task.CompletedTask;
    }

    private static string TruncateForMetadata(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        // Encode value to adhere to HTTP header rules per HeaderNameValueEncode logic:
        // - Characters < 32 (except tab/9) are URL-encoded as %NN
        // - Tab (9) is not URL-encoded
        // - ASCII 127 is encoded as %7F
        // - All other characters are not encoded
        var sb = new System.Text.StringBuilder();
        foreach (char ch in value)
        {
            if (ch == 9)
            {
                // Tab character - not encoded
                sb.Append(ch);
            }
            else if (ch < 32)
            {
                // Control characters - URL encode
                sb.AppendFormat("%{0:X2}", (int)ch);
            }
            else if (ch == 127)
            {
                // DEL character - URL encode
                sb.Append("%7F");
            }
            else if (ch <= 255)
            {
                // Standard ASCII - not encoded
                sb.Append(ch);
            }
            else
            {
                // Non-ASCII characters - URL encode
                sb.AppendFormat("%{0:X2}", (int)ch);
            }
        }

        var sanitized = sb.ToString().Trim();
        return sanitized.Length <= maxLength ? sanitized : sanitized[..maxLength].TrimEnd();
    }
}
