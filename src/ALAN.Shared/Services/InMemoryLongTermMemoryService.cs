using ALAN.Shared.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ALAN.Shared.Services.Memory;

/// <summary>
/// In-memory implementation of long-term memory service.
/// Can be replaced with Azure AI Search or Cosmos DB for production.
/// </summary>
public class InMemoryLongTermMemoryService : ILongTermMemoryService
{
    private readonly ConcurrentDictionary<string, MemoryEntry> _memories = new();
    private readonly ILogger<InMemoryLongTermMemoryService> _logger;

    public InMemoryLongTermMemoryService(ILogger<InMemoryLongTermMemoryService> logger)
    {
        _logger = logger;
        _logger.LogInformation("InMemory Long-Term Memory Service initialized");
    }

    public Task<string> StoreMemoryAsync(MemoryEntry memory, CancellationToken cancellationToken = default)
    {
        _memories[memory.Id] = memory;
        _logger.LogDebug("Stored memory {Id} of type {Type}", memory.Id, memory.Type);
        return Task.FromResult(memory.Id);
    }

    public Task<MemoryEntry?> GetMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        _memories.TryGetValue(id, out var memory);
        return Task.FromResult(memory);
    }

    public Task<List<MemoryEntry>> SearchMemoriesAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default)
    {
        var queryLower = query.ToLowerInvariant();
        var results = _memories.Values
            .Where(m => m.Content.ToLowerInvariant().Contains(queryLower) ||
                       m.Summary.ToLowerInvariant().Contains(queryLower) ||
                       m.Tags.Any(t => t.ToLowerInvariant().Contains(queryLower)))
            .OrderByDescending(m => m.Timestamp)
            .Take(maxResults)
            .ToList();

        _logger.LogDebug("Search for '{Query}' returned {Count} results", query, results.Count);
        return Task.FromResult(results);
    }

    public Task<List<MemoryEntry>> GetRecentMemoriesAsync(int count = 100, CancellationToken cancellationToken = default)
    {
        var results = _memories.Values
            .OrderByDescending(m => m.Timestamp)
            .Take(count)
            .ToList();

        return Task.FromResult(results);
    }

    public Task<bool> DeleteMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        var removed = _memories.TryRemove(id, out _);
        if (removed)
        {
            _logger.LogInformation("Deleted memory {Id}", id);
        }
        return Task.FromResult(removed);
    }

    public Task<int> GetMemoryCountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_memories.Count);
    }

    public Task<List<MemoryEntry>> GetMemoriesByTypeAsync(MemoryType type, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        var results = _memories.Values
            .Where(m => m.Type == type)
            .OrderByDescending(m => m.Timestamp)
            .Take(maxResults)
            .ToList();

        return Task.FromResult(results);
    }

    public Task UpdateMemoryAccessAsync(string id, CancellationToken cancellationToken = default)
    {
        if (_memories.TryGetValue(id, out var memory))
        {
            memory.AccessCount++;
            memory.LastAccessed = DateTime.UtcNow;
            _logger.LogTrace("Updated access for memory {Id}, count: {Count}", id, memory.AccessCount);
        }
        return Task.CompletedTask;
    }
}
