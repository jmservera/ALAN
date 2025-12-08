using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ALAN.Core.Memory;

/// <summary>
/// In-memory implementation for short-term memory storage
/// </summary>
public class ShortTermMemoryService : IMemoryService
{
    private readonly ConcurrentDictionary<string, MemoryEntry> _memoryStore = new();
    private readonly int _maxEntries;

    public ShortTermMemoryService(int maxEntries = 1000)
    {
        _maxEntries = maxEntries;
    }

    public Task StoreAsync(MemoryEntry entry, CancellationToken cancellationToken = default)
    {
        // Enforce capacity limit by removing oldest entries
        if (_memoryStore.Count >= _maxEntries)
        {
            var oldestKey = _memoryStore
                .OrderBy(x => x.Value.Timestamp)
                .First()
                .Key;
            _memoryStore.TryRemove(oldestKey, out _);
        }

        _memoryStore[entry.Id] = entry;
        return Task.CompletedTask;
    }

    public Task<IEnumerable<MemoryEntry>> RetrieveAsync(MemoryType type, int limit = 100, CancellationToken cancellationToken = default)
    {
        var entries = _memoryStore.Values
            .Where(e => e.Type == type)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit);

        return Task.FromResult(entries);
    }

    public Task<IEnumerable<MemoryEntry>> SearchAsync(string query, MemoryType? type = null, int limit = 10, CancellationToken cancellationToken = default)
    {
        var entries = _memoryStore.Values
            .Where(e => !type.HasValue || e.Type == type.Value)
            .Where(e => e.Content.Contains(query, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Timestamp)
            .Take(limit);

        return Task.FromResult(entries);
    }

    public Task ClearAsync(MemoryType type, CancellationToken cancellationToken = default)
    {
        var keysToRemove = _memoryStore.Values
            .Where(e => e.Type == type)
            .Select(e => e.Id)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _memoryStore.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }
}
