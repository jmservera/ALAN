using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Extensions.Logging;

namespace ALAN.Core.Memory;

/// <summary>
/// Azure Blob Storage implementation for long-term memory persistence
/// </summary>
public class LongTermMemoryService : IMemoryService
{
    private readonly BlobContainerClient _containerClient;
    private readonly ILogger<LongTermMemoryService> _logger;
    private const string ContainerName = "long-term-memory";

    public LongTermMemoryService(string connectionString, ILogger<LongTermMemoryService> logger)
    {
        _logger = logger;
        var blobServiceClient = new BlobServiceClient(connectionString);
        _containerClient = blobServiceClient.GetBlobContainerClient(ContainerName);
        _containerClient.CreateIfNotExists();
    }

    public async Task StoreAsync(MemoryEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            var blobName = $"{entry.Type}/{entry.Timestamp:yyyy-MM-dd}/{entry.Id}.json";
            var blobClient = _containerClient.GetBlobClient(blobName);

            var json = JsonSerializer.Serialize(entry);
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            
            await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
            _logger.LogInformation("Stored memory entry {Id} to blob storage", entry.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store memory entry {Id}", entry.Id);
            throw;
        }
    }

    public async Task<IEnumerable<MemoryEntry>> RetrieveAsync(MemoryType type, int limit = 100, CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = new List<MemoryEntry>();
            var prefix = $"{type}/";

            await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                if (entries.Count >= limit) break;

                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken);
                var json = response.Value.Content.ToString();
                var entry = JsonSerializer.Deserialize<MemoryEntry>(json);
                
                if (entry != null)
                {
                    entries.Add(entry);
                }
            }

            return entries.OrderByDescending(e => e.Timestamp).Take(limit);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve memory entries of type {Type}", type);
            return Enumerable.Empty<MemoryEntry>();
        }
    }

    public async Task<IEnumerable<MemoryEntry>> SearchAsync(string query, MemoryType? type = null, int limit = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var entries = new List<MemoryEntry>();
            var prefix = type.HasValue ? $"{type.Value}/" : string.Empty;

            await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                var response = await blobClient.DownloadContentAsync(cancellationToken);
                var json = response.Value.Content.ToString();
                var entry = JsonSerializer.Deserialize<MemoryEntry>(json);
                
                if (entry != null && entry.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
                {
                    entries.Add(entry);
                    if (entries.Count >= limit) break;
                }
            }

            return entries.OrderByDescending(e => e.Timestamp);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search memory with query {Query}", query);
            return Enumerable.Empty<MemoryEntry>();
        }
    }

    public async Task ClearAsync(MemoryType type, CancellationToken cancellationToken = default)
    {
        try
        {
            var prefix = $"{type}/";
            await foreach (var blobItem in _containerClient.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
            }

            _logger.LogInformation("Cleared all {Type} memory entries", type);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear {Type} memory", type);
            throw;
        }
    }
}
