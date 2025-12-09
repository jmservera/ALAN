using ALAN.Shared.Models;

namespace ALAN.Shared.Services.Memory;

/// <summary>
/// Interface for long-term memory storage and retrieval.
/// </summary>
public interface ILongTermMemoryService
{
    Task<string> StoreMemoryAsync(MemoryEntry memory, CancellationToken cancellationToken = default);
    Task<MemoryEntry?> GetMemoryAsync(string id, CancellationToken cancellationToken = default);
    Task<List<MemoryEntry>> SearchMemoriesAsync(string query, int maxResults = 10, CancellationToken cancellationToken = default);
    Task<List<MemoryEntry>> GetRecentMemoriesAsync(int count = 100, CancellationToken cancellationToken = default);
    Task<bool> DeleteMemoryAsync(string id, CancellationToken cancellationToken = default);
    Task<int> GetMemoryCountAsync(CancellationToken cancellationToken = default);
    Task<List<MemoryEntry>> GetMemoriesByTypeAsync(MemoryType type, int maxResults = 50, CancellationToken cancellationToken = default);
    Task UpdateMemoryAccessAsync(string id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for short-term (working) memory.
/// </summary>
public interface IShortTermMemoryService
{
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string key, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<List<string>> GetKeysAsync(string pattern = "*", CancellationToken cancellationToken = default);
}

/// <summary>
/// Interface for memory consolidation and learning extraction.
/// </summary>
public interface IMemoryConsolidationService
{
    Task<ConsolidatedLearning> ConsolidateMemoriesAsync(List<MemoryEntry> memories, CancellationToken cancellationToken = default);
    Task<List<ConsolidatedLearning>> ExtractLearningsAsync(DateTime since, CancellationToken cancellationToken = default);
    Task<bool> StoreLearningAsync(ConsolidatedLearning learning, CancellationToken cancellationToken = default);
    Task<List<MemoryEntry>> IdentifyOutdatedMemoriesAsync(CancellationToken cancellationToken = default);
    Task<int> CleanupOutdatedMemoriesAsync(CancellationToken cancellationToken = default);
}
