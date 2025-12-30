using ALAN.Shared.Models;
using ALAN.Shared.Services.Resilience;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;

namespace ALAN.Shared.Services.Memory;

/// <summary>
/// Azure AI Search implementation of vector memory service.
/// Provides semantic search capabilities using vector embeddings.
/// </summary>
public class AzureAISearchMemoryService : IVectorMemoryService
{
    private readonly SearchIndexClient _indexClient;
    private readonly AzureOpenAIClient _openAIClient;
    private readonly string _embeddingDeployment;
    private readonly ILogger<AzureAISearchMemoryService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public AzureAISearchMemoryService(
        string searchEndpoint,
        string openAIEndpoint,
        string embeddingDeployment,
        ILogger<AzureAISearchMemoryService> logger,
        AzureKeyCredential? searchCredential = null,
        AzureKeyCredential? openAICredential = null)
    {
        _logger = logger;
        _embeddingDeployment = embeddingDeployment;
        _resiliencePipeline = ResiliencePolicy.CreateStorageRetryPipeline(logger);

        // Initialize Search client
        _indexClient = searchCredential != null
            ? new SearchIndexClient(new Uri(searchEndpoint), searchCredential)
            : new SearchIndexClient(new Uri(searchEndpoint), new DefaultAzureCredential());

        // Initialize OpenAI client for embeddings
        _openAIClient = openAICredential != null
            ? new AzureOpenAIClient(new Uri(openAIEndpoint), openAICredential)
            : new AzureOpenAIClient(new Uri(openAIEndpoint), new DefaultAzureCredential());

        _logger.LogInformation("AzureAISearchMemoryService initialized");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // Initialize both long-term and short-term indexes
        await EnsureIndexExistsAsync("long-term", cancellationToken);
        await EnsureIndexExistsAsync("short-term", cancellationToken);
    }

    private async Task EnsureIndexExistsAsync(string indexName, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Initializing Azure AI Search index: {IndexName}", indexName);

            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                // Check if index exists
                try
                {
                    await _indexClient.GetIndexAsync(indexName, ct);
                    _logger.LogInformation("Index {IndexName} already exists", indexName);
                    return;
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    _logger.LogInformation("Index {IndexName} does not exist, creating...", indexName);
                }

                // Create the index with vector search configuration
                var index = new SearchIndex(indexName)
                {
                    Fields =
                    {
                        new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                        new SearchableField("content") { IsFilterable = true, IsSortable = true },
                        new SearchableField("summary") { IsFilterable = true },
                        new SimpleField("type", SearchFieldDataType.String) { IsFilterable = true },
                        new SimpleField("timestamp", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                        new SimpleField("importance", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
                        new SearchField("tags", SearchFieldDataType.Collection(SearchFieldDataType.String)) { IsFilterable = true, IsFacetable = true, IsSearchable = true },
                        new SimpleField("accessCount", SearchFieldDataType.Int32) { IsFilterable = true },
                        new SimpleField("lastAccessed", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                        new SimpleField("metadata", SearchFieldDataType.String),
                        new VectorSearchField("contentVector", 1536, "memory-vector-profile")
                    },
                    VectorSearch = new VectorSearch
                    {
                        Profiles =
                        {
                            new VectorSearchProfile("memory-vector-profile", "memory-hnsw-config")
                        },
                        Algorithms =
                        {
                            new HnswAlgorithmConfiguration("memory-hnsw-config")
                        }
                    }
                };

                await _indexClient.CreateIndexAsync(index, ct);
                _logger.LogInformation("Index {IndexName} created successfully", indexName);
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Azure AI Search index {IndexName}", indexName);
            throw;
        }
    }

    public async Task<string> StoreMemoryAsync(MemoryEntry memory, string collection = "long-term", CancellationToken cancellationToken = default)
    {
        try
        {
            // Ensure index exists
            await EnsureIndexExistsAsync(collection, cancellationToken);

            // Generate embedding for the memory content
            var embedding = await GenerateEmbeddingAsync(memory.Content, cancellationToken);

            var document = new MemorySearchDocument
            {
                Id = memory.Id,
                Content = memory.Content,
                Summary = memory.Summary,
                Type = memory.Type.ToString(),
                Timestamp = memory.Timestamp,
                Importance = memory.Importance,
                Tags = [.. memory.Tags],
                AccessCount = memory.AccessCount,
                LastAccessed = memory.LastAccessed,
                Metadata = JsonSerializer.Serialize(memory.Metadata),
                ContentVector = embedding
            };

            var searchClient = _indexClient.GetSearchClient(collection);
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var batch = IndexDocumentsBatch.Upload([document]);
                await searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);
            }, cancellationToken);

            _logger.LogDebug("Stored memory {Id} in vector search index {Collection}", memory.Id, collection);
            return memory.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store memory {Id} in vector search", memory.Id);
            throw;
        }
    }

    public async Task<List<MemorySearchResult>> SearchMemoriesAsync(
        string query,
        int maxResults = 10,
        double minScore = 0.7,
        MemorySearchFilters? filters = null,
        string collection = "long-term",
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Generate embedding for the query
            var queryEmbedding = await GenerateEmbeddingAsync(query, cancellationToken);

            var searchOptions = new SearchOptions
            {
                Size = maxResults,
                Select = { "id", "content", "summary", "type", "timestamp", "importance", "tags", "accessCount", "lastAccessed", "metadata" },
                VectorSearch = new VectorSearchOptions
                {
                    Queries = { new VectorizedQuery(queryEmbedding.ToArray()) { KNearestNeighborsCount = maxResults, Fields = { "contentVector" } } }
                }
            };

            // Apply filters
            if (filters != null)
            {
                var filterParts = new List<string>();

                if (filters.Types?.Count > 0)
                {
                    var typeFilter = string.Join(" or ", filters.Types.Select(t => $"type eq '{t}'"));
                    filterParts.Add($"({typeFilter})");
                }

                if (filters.Tags?.Count > 0)
                {
                    var tagFilter = string.Join(" or ", filters.Tags.Select(t => $"tags/any(tag: tag eq '{t}')"));
                    filterParts.Add($"({tagFilter})");
                }

                if (filters.FromDate.HasValue)
                {
                    filterParts.Add($"timestamp ge {filters.FromDate.Value:O}");
                }

                if (filters.ToDate.HasValue)
                {
                    filterParts.Add($"timestamp le {filters.ToDate.Value:O}");
                }

                if (filters.MinImportance.HasValue)
                {
                    filterParts.Add($"importance ge {filters.MinImportance.Value}");
                }

                if (filterParts.Count > 0)
                {
                    searchOptions.Filter = string.Join(" and ", filterParts);
                }
            }

            var results = new List<MemorySearchResult>();
            var searchClient = _indexClient.GetSearchClient(collection);

            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var response = await searchClient.SearchAsync<MemorySearchDocument>("*", searchOptions, ct);

                await foreach (var result in response.Value.GetResultsAsync())
                {
                    var score = result.Score ?? 0.0;
                    if (score >= minScore)
                    {
                        var memory = ConvertToMemoryEntry(result.Document);
                        results.Add(new MemorySearchResult
                        {
                            Memory = memory,
                            Score = score
                        });
                    }
                }
            }, cancellationToken);

            _logger.LogInformation("Found {Count} memories for query with min score {MinScore}", results.Count, minScore);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search memories for query: {Query}", query);
            throw;
        }
    }

    public async Task<List<MemorySearchResult>> FindSimilarMemoriesAsync(
        MemoryEntry memory,
        int maxResults = 5,
        double minScore = 0.8,
        CancellationToken cancellationToken = default)
    {
        // Use the memory's content to find similar memories
        var results = await SearchMemoriesAsync(memory.Content, maxResults + 1, minScore, cancellationToken: cancellationToken);

        // Filter out the memory itself
        return results.Where(r => r.Memory.Id != memory.Id).Take(maxResults).ToList();
    }

    public async Task<MemoryEntry?> GetMemoryAsync(string id, string collection = "long-term", CancellationToken cancellationToken = default)
    {
        try
        {
            var searchClient = _indexClient.GetSearchClient(collection);
            return await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var response = await searchClient.GetDocumentAsync<MemorySearchDocument>(id, cancellationToken: ct);
                return ConvertToMemoryEntry(response.Value);
            }, cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory {Id} from collection {Collection}", id, collection);
            throw;
        }
    }

    public async Task<bool> DeleteMemoryAsync(string id, string collection = "long-term", CancellationToken cancellationToken = default)
    {
        try
        {
            var searchClient = _indexClient.GetSearchClient(collection);
            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var batch = IndexDocumentsBatch.Delete("id", [id]);
                await searchClient.IndexDocumentsAsync(batch, cancellationToken: ct);
            }, cancellationToken);

            _logger.LogInformation("Deleted memory {Id} from vector search index {Collection}", id, collection);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete memory {Id} from collection {Collection}", id, collection);
            return false;
        }
    }

    public async Task<List<MemoryEntry>> GetAllRecentMemoriesAsync(
        int maxResults = 50,
        string collection = "short-term",
        CancellationToken cancellationToken = default)
    {
        try
        {
            var searchClient = _indexClient.GetSearchClient(collection);
            var searchOptions = new SearchOptions
            {
                Size = maxResults,
                OrderBy = { "timestamp desc" },
                Select = { "id", "content", "summary", "type", "timestamp", "importance", "tags", "accessCount", "lastAccessed", "metadata" }
            };

            var results = new List<MemoryEntry>();

            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var response = await searchClient.SearchAsync<MemorySearchDocument>("*", searchOptions, ct);

                await foreach (var result in response.Value.GetResultsAsync())
                {
                    var memory = ConvertToMemoryEntry(result.Document);
                    if (memory != null)
                    {
                        results.Add(memory);
                    }
                }
            }, cancellationToken);

            _logger.LogDebug("Retrieved {Count} recent memories from collection {Collection}",
                results.Count, collection);

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve recent memories from collection {Collection}", collection);
            return new List<MemoryEntry>();
        }
    }

    public async Task UpdateMemoryAccessAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            // Try both collections (short-term first, then long-term)
            var memory = await GetMemoryAsync(id, "short-term", cancellationToken)
                ?? await GetMemoryAsync(id, "long-term", cancellationToken);

            if (memory != null)
            {
                memory.AccessCount++;
                memory.LastAccessed = DateTime.UtcNow;
                // Determine which collection to update based on tags
                var collection = memory.Tags.Contains("short-term") ? "short-term" : "long-term";
                await StoreMemoryAsync(memory, collection, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update memory access for {Id}", id);
        }
    }

    public async Task<long> GetMemoryCountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Count memories in both collections
            long shortTermCount = 0;
            long longTermCount = 0;

            var shortTermClient = _indexClient.GetSearchClient("short-term");
            var longTermClient = _indexClient.GetSearchClient("long-term");

            await _resiliencePipeline.ExecuteAsync(async ct =>
            {
                var shortTermResponse = await shortTermClient.SearchAsync<MemorySearchDocument>("*", new SearchOptions { Size = 0, IncludeTotalCount = true }, ct);
                shortTermCount = shortTermResponse.Value.TotalCount ?? 0;

                var longTermResponse = await longTermClient.SearchAsync<MemorySearchDocument>("*", new SearchOptions { Size = 0, IncludeTotalCount = true }, ct);
                longTermCount = longTermResponse.Value.TotalCount ?? 0;
            }, cancellationToken);

            return shortTermCount + longTermCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory count");
            return 0;
        }
    }

    private async Task<ReadOnlyMemory<float>> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Attempted to generate embedding for null or empty text");
            // Return a zero vector as fallback
            return new float[1536];
        }

        var embeddingClient = _openAIClient.GetEmbeddingClient(_embeddingDeployment);

        var response = await _resiliencePipeline.ExecuteAsync(async ct =>
            await embeddingClient.GenerateEmbeddingAsync(text, cancellationToken: ct),
            cancellationToken);

        return response.Value.ToFloats();
    }

    private static MemoryEntry ConvertToMemoryEntry(MemorySearchDocument document)
    {
        // Safely parse memory type with fallback
        if (!Enum.TryParse<MemoryType>(document.Type, out var memoryType))
        {
            memoryType = MemoryType.Observation; // Default fallback
        }

        return new MemoryEntry
        {
            Id = document.Id,
            Content = document.Content,
            Summary = document.Summary,
            Type = memoryType,
            Timestamp = document.Timestamp.UtcDateTime,
            Importance = document.Importance,
            Tags = [.. document.Tags],
            AccessCount = document.AccessCount,
            LastAccessed = document.LastAccessed.UtcDateTime,
            Metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(document.Metadata) ?? []
        };
    }
}
