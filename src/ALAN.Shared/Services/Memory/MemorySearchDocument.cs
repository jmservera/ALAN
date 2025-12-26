using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using System.Text.Json.Serialization;

namespace ALAN.Shared.Services.Memory;

/// <summary>
/// Document model for Azure AI Search index.
/// Represents a memory entry with vector embeddings for semantic search.
/// </summary>
public class MemorySearchDocument
{
    [SimpleField(IsKey = true, IsFilterable = true)]
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true, IsSortable = true)]
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [SearchableField(IsFilterable = true)]
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [SimpleField(IsFilterable = true, IsSortable = true)]
    [JsonPropertyName("timestamp")]
    public DateTimeOffset Timestamp { get; set; }

    [SimpleField(IsFilterable = true, IsSortable = true)]
    [JsonPropertyName("importance")]
    public double Importance { get; set; }

    [SearchableField(IsFilterable = true, IsFacetable = true)]
    [JsonPropertyName("tags")]
    public string[] Tags { get; set; } = [];

    [SimpleField(IsFilterable = true)]
    [JsonPropertyName("accessCount")]
    public int AccessCount { get; set; }

    [SimpleField(IsFilterable = true, IsSortable = true)]
    [JsonPropertyName("lastAccessed")]
    public DateTimeOffset LastAccessed { get; set; }

    [JsonPropertyName("metadata")]
    public string Metadata { get; set; } = "{}";

    /// <summary>
    /// Vector embedding of the memory content for semantic search.
    /// Configured with 1536 dimensions for text-embedding-ada-002 or similar models.
    /// </summary>
    [VectorSearchField(VectorSearchDimensions = 1536, VectorSearchProfileName = "memory-vector-profile")]
    [JsonPropertyName("contentVector")]
    public ReadOnlyMemory<float> ContentVector { get; set; }
}
