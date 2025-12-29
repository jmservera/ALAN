using ALAN.Shared.Models;
using ALAN.Shared.Services.Resilience;
using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Polly;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Text.Json;

namespace ALAN.Shared.Services.Memory;

/// <summary>
/// Qdrant-based vector memory service for local development.
/// Provides semantic search using Qdrant vector database.
/// For production, use AzureAISearchMemoryService.
/// </summary>
public class QdrantMemoryService : IVectorMemoryService
{
    private readonly QdrantClient _qdrantClient;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly string _embeddingDeployment;
    private readonly ILogger<QdrantMemoryService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;
    private const string CollectionName = "alan-memories";
    private const ulong VectorSize = 1536; // text-embedding-ada-002 dimension

    public QdrantMemoryService(
        string qdrantEndpoint,
        string openAIEndpoint,
        string embeddingDeployment,
        ILogger<QdrantMemoryService> logger,
        string? apiKey = null)
    {
        _logger = logger;
        _embeddingDeployment = embeddingDeployment;
        _resiliencePipeline = ResiliencePolicy.CreateOpenAIRetryPipeline(logger);

        if (string.IsNullOrEmpty(qdrantEndpoint))
        {
            throw new ArgumentNullException(nameof(qdrantEndpoint), "Qdrant endpoint is required");
        }
        if (string.IsNullOrEmpty(openAIEndpoint))
        {
            throw new ArgumentNullException(nameof(openAIEndpoint), "Azure OpenAI endpoint is required");
        }
        if (string.IsNullOrEmpty(embeddingDeployment))
        {
            throw new ArgumentNullException(nameof(embeddingDeployment), "Embedding deployment is required");
        }

        // Initialize Qdrant client - parse URL to get host, port, and scheme
        if (!qdrantEndpoint.StartsWith("http://") && !qdrantEndpoint.StartsWith("https://"))
        {
            qdrantEndpoint = $"http://{qdrantEndpoint}";
        }

        var uri = new Uri(qdrantEndpoint);
        var host = uri.Host;
        var port = uri.Port;
        var https = uri.Scheme == "https";

        _qdrantClient = new QdrantClient(host, port, https, apiKey: null);
        _logger.LogInformation("Qdrant client initialized: {Host}:{Port} (HTTPS: {Https})", host, port, https);

        // Initialize OpenAI client for embeddings
        if (!string.IsNullOrEmpty(apiKey))
        {
            _openAIClient = new AzureOpenAIClient(new Uri(openAIEndpoint), new Azure.AzureKeyCredential(apiKey));
        }
        else
        {
            _openAIClient = new AzureOpenAIClient(new Uri(openAIEndpoint), new DefaultAzureCredential());
        }

        _logger.LogInformation("QdrantMemoryService initialized with endpoint: {Endpoint}", qdrantEndpoint);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Check if collection exists
            var collections = await _qdrantClient.ListCollectionsAsync(cancellationToken);
            if (collections.Any(c => c == CollectionName))
            {
                _logger.LogInformation("Qdrant collection '{Collection}' already exists", CollectionName);
                return;
            }

            // Create collection with vector configuration
            await _qdrantClient.CreateCollectionAsync(
                CollectionName,
                new VectorParams
                {
                    Size = VectorSize,
                    Distance = Distance.Cosine
                },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created Qdrant collection '{Collection}' with vector size {Size}",
                CollectionName, VectorSize);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Qdrant collection");
            throw;
        }
    }

    public async Task<string> StoreMemoryAsync(MemoryEntry memory, CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for the memory content
            var embedding = await GenerateEmbeddingAsync(memory.Content, cancellationToken);

            // Create payload for Qdrant point
            var payload = new Dictionary<string, Value>
            {
                ["id"] = memory.Id,
                ["type"] = memory.Type.ToString(),
                ["content"] = memory.Content,
                ["summary"] = memory.Summary,
                ["timestamp"] = memory.Timestamp.ToString("O"),
                ["importance"] = memory.Importance,
                ["accessCount"] = (long)memory.AccessCount,
                ["lastAccessed"] = memory.LastAccessed.ToString("O"),
                ["metadata"] = JsonSerializer.Serialize(memory.Metadata),
                ["tags"] = JsonSerializer.Serialize(memory.Tags)
            };

            // Create point for Qdrant
            var point = new PointStruct
            {
                Id = new PointId { Uuid = memory.Id },
                Vectors = embedding.ToArray()
            };

            // Add payload to point
            foreach (var kvp in payload)
            {
                point.Payload.Add(kvp.Key, kvp.Value);
            }

            // Upsert point to Qdrant
            await _qdrantClient.UpsertAsync(
                CollectionName,
                new[] { point },
                cancellationToken: cancellationToken);

            _logger.LogInformation("Stored memory {Id} in Qdrant", memory.Id);
            return memory.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store memory {Id} in Qdrant", memory.Id);
            throw;
        }
    }

    public async Task<List<MemorySearchResult>> SearchMemoriesAsync(
        string query,
        int maxResults = 10,
        double minScore = 0.7,
        MemorySearchFilters? filters = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for query
            var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);

            // Search in Qdrant (filters not implemented yet for simplicity)
            var searchResults = await _qdrantClient.SearchAsync(
                CollectionName,
                queryEmbedding.ToArray(),
                limit: (ulong)maxResults,
                scoreThreshold: (float)minScore,
                cancellationToken: cancellationToken);

            var results = searchResults
                .Select(r => new MemorySearchResult
                {
                    Memory = ConvertToMemoryEntry(r.Payload),
                    Score = r.Score
                })
                .ToList();

            _logger.LogInformation("Found {Count} memories for query (min score: {MinScore})",
                results.Count, minScore);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search memories in Qdrant");
            return new List<MemorySearchResult>();
        }
    }

    public async Task<List<MemorySearchResult>> FindSimilarMemoriesAsync(
        MemoryEntry memory,
        int maxResults = 5,
        double minScore = 0.8,
        CancellationToken cancellationToken = default)
    {
        return await SearchMemoriesAsync(
            memory.Content,
            maxResults,
            minScore,
            cancellationToken: cancellationToken);
    }

    public async Task<MemoryEntry?> GetMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var pointId = new PointId { Uuid = id };
            var points = await _qdrantClient.RetrieveAsync(
                CollectionName,
                new[] { pointId },
                withPayload: true,
                cancellationToken: cancellationToken);

            if (points.Count == 0)
            {
                return null;
            }

            return ConvertToMemoryEntry(points[0].Payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory {Id} from Qdrant", id);
            return null;
        }
    }

    public async Task<bool> DeleteMemoryAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            // Use DeletePayloadKeysAsync as a workaround, or just mark as deleted in payload
            // For now, we'll update the point to mark it as deleted
            var memory = await GetMemoryAsync(id, cancellationToken);
            if (memory == null)
            {
                return false;
            }

            // Store it with a "deleted" tag to filter it out later
            memory.Tags.Add("__deleted__");
            await StoreMemoryAsync(memory, cancellationToken);

            _logger.LogInformation("Marked memory {Id} as deleted in Qdrant", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete memory {Id} from Qdrant", id);
            return false;
        }
    }

    public async Task UpdateMemoryAccessAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var memory = await GetMemoryAsync(id, cancellationToken);
            if (memory != null)
            {
                memory.AccessCount++;
                memory.LastAccessed = DateTime.UtcNow;
                await StoreMemoryAsync(memory, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update memory access for {Id}", id);
        }
    }

    public async Task<long> GetMemoryCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var info = await _qdrantClient.GetCollectionInfoAsync(CollectionName, cancellationToken);
            return (long)(info?.PointsCount ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory count from Qdrant");
            return 0;
        }
    }

    private async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Attempted to generate embedding for null or empty text");
            return new float[(int)VectorSize];
        }

        var embeddingClient = _openAIClient.GetEmbeddingClient(_embeddingDeployment);

        var response = await _resiliencePipeline.ExecuteAsync(async ct =>
            await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct),
            cancellationToken);

        return response.Value.ToFloats();
    }

    private static MemoryEntry ConvertToMemoryEntry(IDictionary<string, Value> payload)
    {
        return new MemoryEntry
        {
            Id = payload["id"].StringValue,
            Type = Enum.Parse<MemoryType>(payload["type"].StringValue),
            Content = payload["content"].StringValue,
            Summary = payload["summary"].StringValue,
            Timestamp = DateTime.Parse(payload["timestamp"].StringValue),
            Importance = payload["importance"].DoubleValue,
            Tags = JsonSerializer.Deserialize<List<string>>(payload["tags"].StringValue) ?? new List<string>(),
            AccessCount = (int)payload["accessCount"].IntegerValue,
            LastAccessed = DateTime.Parse(payload["lastAccessed"].StringValue),
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(payload["metadata"].StringValue)
                ?? new Dictionary<string, string>()
        };
    }
}
