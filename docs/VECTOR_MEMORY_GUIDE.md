# Vector Memory Implementation Guide

This guide explains how to set up and use ALAN's vector memory system for semantic search and intelligent memory retrieval.

## Overview

ALAN now supports **semantic memory search** using Azure AI Search with vector embeddings. This enables:

- **Semantic Understanding**: Find memories by meaning, not just keywords
- **Task Deduplication**: Automatically detect and avoid repeating completed tasks
- **Context-Aware Retrieval**: Get memories most relevant to current goals
- **Performance Tracking**: Monitor search quality with precision metrics

## Prerequisites

1. **Azure AI Search** - For vector storage and search
2. **Azure OpenAI** - For embedding generation (text-embedding-ada-002 or similar)
3. **Azure Storage** - For traditional blob-based memory (fallback)

## Configuration

### Environment Variables

Set these environment variables to enable vector memory:

```bash
# Required for vector memory
export AZURE_AI_SEARCH_ENDPOINT="https://<your-search>.search.windows.net"
export AZURE_OPENAI_ENDPOINT="https://<your-openai>.openai.azure.com"
export AZURE_OPENAI_EMBEDDING_DEPLOYMENT="text-embedding-ada-002"

# Optional: Use API keys instead of managed identity
export AZURE_OPENAI_API_KEY="<your-key>"

# Traditional storage (still used for short-term memory)
export AZURE_STORAGE_CONNECTION_STRING="<your-connection-string>"
```

### Create Azure Resources

#### 1. Azure AI Search

```bash
az search service create \
  --name <search-service-name> \
  --resource-group <resource-group> \
  --sku standard \
  --location eastus
```

#### 2. Azure OpenAI (if not already created)

```bash
az cognitiveservices account create \
  --name <openai-name> \
  --resource-group <resource-group> \
  --kind OpenAI \
  --sku s0 \
  --location eastus
```

#### 3. Deploy Embedding Model

```bash
az cognitiveservices account deployment create \
  --name <openai-name> \
  --resource-group <resource-group> \
  --deployment-name text-embedding-ada-002 \
  --model-name text-embedding-ada-002 \
  --model-version "2" \
  --model-format OpenAI \
  --sku-capacity 1 \
  --sku-name "Standard"
```

## Usage

### Agent Loop Integration

The vector memory system is automatically used by the agent loop when configured:

```csharp
// Agent automatically uses vector search when available
await agent.RunAsync(cancellationToken);

// Memory loading uses semantic search for current directive
// - Finds memories relevant to the task
// - Checks for similar completed tasks
// - Evaluates search quality
```

### Memory Agent API

Use the `MemoryAgent` for advanced memory operations:

```csharp
// Search for relevant memories
var memories = await memoryAgent.SearchRelevantMemoriesAsync(
    taskDescription: "Implement user authentication",
    maxResults: 10
);

// Check if task was already completed
var similarTask = await memoryAgent.FindSimilarCompletedTaskAsync(
    taskDescription: "Add login endpoint"
);

if (similarTask != null && similarTask.Score > 0.85)
{
    Console.WriteLine($"Similar task found: {similarTask.Memory.Summary}");
}

// Consolidate memories with AI
var learning = await memoryAgent.ConsolidateMemoriesWithAIAsync(memories);

// Evaluate search quality
var evaluation = await memoryAgent.EvaluateSearchQualityAsync(
    query: "authentication",
    results: searchResults
);
```

### ChatAPI Endpoints

Access memory search via REST API:

#### Search Memories
```bash
curl "http://localhost:5001/api/memory/search?query=authentication&maxResults=5&minScore=0.7"
```

Response:
```json
{
  "query": "authentication",
  "method": "vector",
  "count": 5,
  "results": [
    {
      "id": "mem-123",
      "type": "Success",
      "summary": "JWT authentication implemented",
      "timestamp": "2025-01-15T10:30:00Z",
      "importance": 0.9,
      "tags": ["authentication", "jwt", "security"],
      "score": 0.95
    }
  ]
}
```

#### Get Memory Stats
```bash
curl "http://localhost:5001/api/memory/stats"
```

Response:
```json
{
  "longTermMemoryCount": 1523,
  "vectorMemoryCount": 1523,
  "vectorSearchEnabled": true
}
```

#### Get Specific Memory
```bash
curl "http://localhost:5001/api/memory/{id}"
```

### Direct Service Usage

```csharp
// Store memory with automatic embeddings
var memory = new MemoryEntry
{
    Type = MemoryType.Learning,
    Content = "JWT tokens should expire after 15 minutes for security",
    Summary = "JWT expiration best practice",
    Importance = 0.8,
    Tags = ["security", "jwt", "best-practice"]
};

await vectorMemory.StoreMemoryAsync(memory);

// Semantic search
var results = await vectorMemory.SearchMemoriesAsync(
    query: "How long should tokens last?",
    maxResults: 5,
    minScore: 0.7
);

// Advanced filtering
var filters = new MemorySearchFilters
{
    Types = [MemoryType.Learning, MemoryType.Success],
    Tags = ["security"],
    FromDate = DateTime.UtcNow.AddDays(-30),
    MinImportance = 0.6
};

var filtered = await vectorMemory.SearchMemoriesAsync(
    query: "security practices",
    maxResults: 10,
    minScore: 0.75,
    filters: filters
);
```

## Migration Guide

### Migrating Existing Memories

If you have existing memories in blob storage, migrate them to vector search:

```csharp
// Batch migration
var memoryAgent = serviceProvider.GetRequiredService<MemoryAgent>();
var migratedCount = await memoryAgent.BatchMigrateMemoriesAsync(batchSize: 100);
Console.WriteLine($"Migrated {migratedCount} memories to vector search");

// Single memory migration
var memory = await longTermMemory.GetMemoryAsync(memoryId);
if (memory != null)
{
    await memoryAgent.MigrateMemoryToVectorSearchAsync(memory);
}
```

### Hybrid Approach

You can use both blob storage and vector search simultaneously:

- **Blob Storage**: Fast writes, simple queries, lower cost
- **Vector Search**: Semantic search, task deduplication, better relevance

The system will automatically:
- Use vector search when available for reads
- Fall back to blob storage if vector search is not configured
- Continue to work with existing blob-based memories

## Performance Tuning

### Embedding Model Selection

- **text-embedding-ada-002**: Good balance of quality and cost (default)
- **text-embedding-3-small**: Lower cost, slightly lower quality
- **text-embedding-3-large**: Higher quality, higher cost

### Search Parameters

```csharp
// High precision (fewer, more relevant results)
var results = await vectorMemory.SearchMemoriesAsync(
    query: query,
    maxResults: 5,
    minScore: 0.85  // Higher threshold
);

// High recall (more results, some less relevant)
var results = await vectorMemory.SearchMemoriesAsync(
    query: query,
    maxResults: 20,
    minScore: 0.65  // Lower threshold
);
```

### Index Configuration

The search index is automatically created with:
- **HNSW algorithm**: Fast approximate nearest neighbor search
- **1536 dimensions**: Compatible with text-embedding-ada-002
- **Vector profile**: Optimized for similarity search

## Monitoring and Metrics

### Search Quality Evaluation

The system automatically logs search quality metrics:

```
Vector search quality: 80.00% precision, avg score 0.852
```

Metrics tracked:
- **Precision**: % of results above relevance threshold (0.8)
- **Average Score**: Mean similarity score
- **Min/Max Score**: Score range
- **Result Count**: Number of matches found

### Performance Logging

Enable detailed logging to monitor:
- Embedding generation time
- Search execution time
- Memory migration progress
- Task deduplication hits

```csharp
// In appsettings.json
{
  "Logging": {
    "LogLevel": {
      "ALAN.Shared.Services.Memory": "Information",
      "ALAN.Agent.Services.MemoryAgent": "Information"
    }
  }
}
```

## Troubleshooting

### Vector Search Not Working

1. **Check configuration**:
   ```bash
   echo $AZURE_AI_SEARCH_ENDPOINT
   echo $AZURE_OPENAI_ENDPOINT
   echo $AZURE_OPENAI_EMBEDDING_DEPLOYMENT
   ```

2. **Verify resources exist**:
   ```bash
   az search service show --name <search-service> --resource-group <rg>
   az cognitiveservices account show --name <openai-name> --resource-group <rg>
   ```

3. **Check logs**:
   Look for messages like:
   - "Azure AI Search configured, enabling vector memory"
   - "MemoryAgent is available - vector search enabled"

### Low Search Quality

1. **Increase embedding deployment capacity** (more throughput)
2. **Adjust minScore threshold** (balance precision/recall)
3. **Add more relevant memories** (improve training data)
4. **Use filters** to narrow search scope

### Migration Issues

1. **Batch size too large**: Reduce from 100 to 50 or 25
2. **Rate limiting**: Add delays between batches
3. **Memory too large**: Check content length (max ~8K tokens)

## Cost Optimization

### Embedding Generation Costs

- **Billable**: Per 1,000 tokens processed
- **Optimize**: Embed only summary for less important memories
- **Cache**: Store embeddings to avoid regeneration

### Search Costs

- **Billable**: Per search query and storage
- **Optimize**: Use filters to reduce search scope
- **Cache**: Cache common query results

### Hybrid Strategy

Use vector search for:
- Critical memories (importance > 0.7)
- Recent memories (last 30 days)
- Frequently accessed memories

Use blob storage for:
- Low importance memories
- Historical archives
- Bulk storage

## Best Practices

1. **Set appropriate importance scores**: Affects which memories are migrated
2. **Use descriptive summaries**: Improves search relevance
3. **Tag consistently**: Enables better filtering
4. **Monitor search quality**: Adjust thresholds based on metrics
5. **Regular cleanup**: Remove outdated low-importance memories
6. **Batch operations**: Use batch migration for efficiency
7. **Test queries**: Validate search quality with representative queries

## Next Steps

- Review [MEMORY_ARCHITECTURE.md](MEMORY_ARCHITECTURE.md) for system design
- Check [QUICKSTART.md](../QUICKSTART.md) for local development setup
- See [AZURE_DEPLOYMENT.md](AZURE_DEPLOYMENT.md) for production deployment
