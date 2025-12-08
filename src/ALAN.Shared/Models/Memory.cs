namespace ALAN.Shared.Models;

/// <summary>
/// Represents a memory entry in the agent's long-term memory.
/// </summary>
public class MemoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public MemoryType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public int AccessCount { get; set; }
    public DateTime LastAccessed { get; set; } = DateTime.UtcNow;
    public double Importance { get; set; } = 0.5;
    public List<string> Tags { get; set; } = new();
    public string? EmbeddingId { get; set; }
}

public enum MemoryType
{
    Observation,
    Learning,
    CodeChange,
    Decision,
    Reflection,
    Error,
    Success
}

/// <summary>
/// Represents a consolidated learning extracted from multiple memories.
/// </summary>
public class ConsolidatedLearning
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Created { get; set; } = DateTime.UtcNow;
    public string Topic { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public List<string> SourceMemoryIds { get; set; } = new();
    public Dictionary<string, object> Insights { get; set; } = new();
    public double Confidence { get; set; } = 0.7;
}
