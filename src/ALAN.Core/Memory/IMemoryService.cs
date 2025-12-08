using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ALAN.Core.Memory;

/// <summary>
/// Interface for memory service abstraction
/// </summary>
public interface IMemoryService
{
    /// <summary>
    /// Store a memory entry
    /// </summary>
    Task StoreAsync(MemoryEntry entry, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieve memory entries by type
    /// </summary>
    Task<IEnumerable<MemoryEntry>> RetrieveAsync(MemoryType type, int limit = 100, CancellationToken cancellationToken = default);

    /// <summary>
    /// Search memory entries by query
    /// </summary>
    Task<IEnumerable<MemoryEntry>> SearchAsync(string query, MemoryType? type = null, int limit = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Clear memory entries by type
    /// </summary>
    Task ClearAsync(MemoryType type, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a memory entry
/// </summary>
public class MemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MemoryType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Type of memory
/// </summary>
public enum MemoryType
{
    ShortTerm,
    LongTerm,
    Learning
}
