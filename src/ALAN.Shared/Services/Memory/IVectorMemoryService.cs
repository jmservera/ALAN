using ALAN.Shared.Models;

namespace ALAN.Shared.Services.Memory;

/// <summary>
/// Interface for vector-based memory storage and semantic search.
/// Enables finding similar memories based on semantic meaning rather than keyword matching.
/// </summary>
public interface IVectorMemoryService
{
    /// <summary>
    /// Stores a memory with vector embeddings for semantic search.
    /// </summary>
    /// <param name="memory">Memory entry to store</param>
    /// <param name="collection">Collection name (default: long-term, short-term for temporary context)</param>
    Task<string> StoreMemoryAsync(MemoryEntry memory, string collection = "long-term", CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches for memories semantically similar to the query.
    /// Uses vector embeddings to find relevant memories based on meaning.
    /// </summary>
    /// <param name="query">Natural language query describing what to search for</param>
    /// <param name="maxResults">Maximum number of results to return</param>
    /// <param name="minScore">Minimum similarity score (0.0 to 1.0)</param>
    /// <param name="filters">Optional filters (e.g., by type, tags, date range)</param>
    /// <param name="collection">Collection to search (default: long-term)</param>
    Task<List<MemorySearchResult>> SearchMemoriesAsync(
        string query, 
        int maxResults = 10, 
        double minScore = 0.7,
        MemorySearchFilters? filters = null,
        string collection = "long-term",
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds memories similar to a given memory entry.
    /// Useful for detecting duplicate tasks or similar experiences.
    /// </summary>
    Task<List<MemorySearchResult>> FindSimilarMemoriesAsync(
        MemoryEntry memory, 
        int maxResults = 5, 
        double minScore = 0.8,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific memory by ID.
    /// </summary>
    Task<MemoryEntry?> GetMemoryAsync(string id, string collection = "long-term", CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a memory by ID.
    /// </summary>
    Task<bool> DeleteMemoryAsync(string id, string collection = "long-term", CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all recent memories from a collection without semantic search.
    /// Useful for retrieving all short-term memories as immediate context.
    /// </summary>
    Task<List<MemoryEntry>> GetAllRecentMemoriesAsync(int maxResults = 50, string collection = "short-term", CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates memory access tracking.
    /// </summary>
    Task UpdateMemoryAccessAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of memories in the index.
    /// </summary>
    Task<long> GetMemoryCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the search index exists and is configured correctly.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result from a vector memory search, including the memory and its relevance score.
/// </summary>
public class MemorySearchResult
{
    public MemoryEntry Memory { get; set; } = new();
    public double Score { get; set; }
    public string? Highlights { get; set; }
}

/// <summary>
/// Filters for memory search queries.
/// </summary>
public class MemorySearchFilters
{
    public List<MemoryType>? Types { get; set; }
    public List<string>? Tags { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public double? MinImportance { get; set; }
}
