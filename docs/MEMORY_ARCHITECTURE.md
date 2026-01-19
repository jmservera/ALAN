# Memory Architecture

ALAN includes a sophisticated memory system that enables self-improvement through learning and consolidation. The system now features **vector-based semantic search** using Azure AI Search for intelligent memory retrieval.

## Overview

The memory architecture consists of four main components:

1. **Short-term Memory** - Working memory for current context and tasks
2. **Long-term Memory** - Persistent storage for historical context, logs, and learnings
3. **Vector Memory** - Semantic search using Azure AI Search with embeddings (NEW)
4. **Memory Consolidation** - Batch process that analyzes memories and extracts learnings
5. **Memory Agent** - Specialized agent for memory management and task deduplication (NEW)

## Memory Types

### Short-term Memory (Working Memory)
- Stores temporary data with optional expiration
- Used for current session state and immediate context
- Implemented in-memory (can be replaced with Redis for production)
- Automatically expires old entries

### Long-term Memory
- Stores permanent memories of observations, decisions, actions, errors, and learnings
- Each memory has:
  - Type (Observation, Learning, CodeChange, Decision, Reflection, Error, Success)
  - Content and summary
  - Metadata tags
  - Importance score
  - Access tracking
- Supports searching and filtering
- Can be backed by Azure Blob Storage or Azure AI Search

### Vector Memory (Azure AI Search)
**New feature for semantic memory retrieval:**
- Stores memory embeddings using Azure OpenAI text-embedding models
- Enables semantic search - finds memories by meaning, not just keywords
- Automatic task deduplication - detects similar completed tasks
- Search quality evaluation with precision metrics
- 1536-dimensional vector embeddings (text-embedding-ada-002 compatible)
- HNSW algorithm for fast approximate nearest neighbor search
- Configurable similarity thresholds and result filtering

**Benefits:**
- **Semantic Understanding**: Finds relevant memories even when wording differs
- **Task Avoidance**: Prevents repeating already-completed work
- **Context-Aware**: Retrieves memories most relevant to current goals
- **Performance Tracking**: Monitors and logs search quality metrics

### Memory Entry Structure

```csharp
public class MemoryEntry
{
    public string Id { get; set; }
    public DateTime Timestamp { get; set; }
    public MemoryType Type { get; set; }
    public string Content { get; set; }
    public string Summary { get; set; }
    public Dictionary<string, string> Metadata { get; set; }
    public int AccessCount { get; set; }
    public DateTime LastAccessed { get; set; }
    public double Importance { get; set; }  // 0.0 to 1.0
    public List<string> Tags { get; set; }
}
```

## Batch Learning Process

The batch learning process runs periodically to:
1. Extract learnings from recent memories
2. Consolidate similar memories into higher-level insights
3. Clean up outdated or low-importance memories
4. Store consolidated learnings for future reference

### Configuration

Batch learning is triggered when:
- A configurable number of iterations have passed (default: 100)
- OR a time threshold is exceeded (default: 4 hours)

To configure batch learning intervals, modify the service initialization in your code.

### Batch Learning Output

Consolidated learnings include:
- Topic summary
- Key insights extracted from multiple memories
- Confidence score
- References to source memories

## Usage Examples

### Storing a Memory (Traditional)

```csharp
var memory = new MemoryEntry
{
    Type = MemoryType.Learning,
    Content = "Discovered that async operations improve responsiveness",
    Summary = "Async programming benefit",
    Importance = 0.8,
    Tags = new List<string> { "async", "performance", "learning" }
};

await longTermMemory.StoreMemoryAsync(memory);
```

### Storing a Memory with Vector Search

```csharp
// Store memory with automatic embedding generation
var memory = new MemoryEntry
{
    Type = MemoryType.Success,
    Content = "Successfully implemented JWT authentication with refresh tokens",
    Summary = "JWT auth implementation",
    Importance = 0.9,
    Tags = ["authentication", "jwt", "security"]
};

await vectorMemory.StoreMemoryAsync(memory); // Automatically generates embeddings
```

### Semantic Memory Search

```csharp
// Find memories by semantic similarity
var results = await vectorMemory.SearchMemoriesAsync(
    query: "How do I secure user login?",  // Natural language query
    maxResults: 10,
    minScore: 0.7  // Similarity threshold
);

// Filter by type and importance
var filters = new MemorySearchFilters
{
    Types = [MemoryType.Learning, MemoryType.Success],
    MinImportance = 0.6,
    FromDate = DateTime.UtcNow.AddDays(-30)
};

var filteredResults = await vectorMemory.SearchMemoriesAsync(
    query: "authentication patterns",
    maxResults: 5,
    minScore: 0.8,
    filters: filters
);

foreach (var result in filteredResults)
{
    Console.WriteLine($"[Score: {result.Score:F3}] {result.Memory.Summary}");
    Console.WriteLine($"  Type: {result.Memory.Type}, Importance: {result.Memory.Importance}");
}
```

### Finding Similar Completed Tasks

```csharp
// Check if a task has already been completed
var taskDescription = "Add user registration endpoint";
var similarTask = await memoryAgent.FindSimilarCompletedTaskAsync(taskDescription);

if (similarTask != null && similarTask.Score > 0.85)
{
    Console.WriteLine($"Similar task already completed: {similarTask.Memory.Summary}");
    Console.WriteLine($"Result: {similarTask.Memory.Content}");
    // Reuse or reference the previous work
}
```

### Using the Memory Agent

```csharp
// Search for relevant memories for a specific task
var relevantMemories = await memoryAgent.SearchRelevantMemoriesAsync(
    taskDescription: "Implement password reset flow",
    maxResults: 10
);

// Consolidate memories with AI
var consolidatedLearning = await memoryAgent.ConsolidateMemoriesWithAIAsync(
    memories: recentMemories
);

// Evaluate search quality
var evaluation = await memoryAgent.EvaluateSearchQualityAsync(
    query: "authentication",
    results: searchResults
);

Console.WriteLine($"Precision: {evaluation.Precision:P2}");
Console.WriteLine($"Average Score: {evaluation.AverageScore:F3}");
```

### Searching Memories (Traditional)

```csharp
// Search for memories containing specific keywords
var memories = await longTermMemory.SearchMemoriesAsync("async", maxResults: 10);

// Get memories by type
var learnings = await longTermMemory.GetMemoriesByTypeAsync(MemoryType.Learning);

// Get recent memories
var recent = await longTermMemory.GetRecentMemoriesAsync(count: 50);
```

### Using Short-term Memory

```csharp
// Store temporary data
await shortTermMemory.SetAsync("current_task", taskInfo, TimeSpan.FromHours(1));

// Retrieve data
var task = await shortTermMemory.GetAsync<TaskInfo>("current_task");

// Check existence
bool exists = await shortTermMemory.ExistsAsync("current_task");
```

## Production Deployment

### Azure AI Search Integration (Recommended)

To enable vector-based semantic search in production:

1. **Create an Azure AI Search resource**
   ```bash
   az search service create \
     --name <search-service-name> \
     --resource-group <resource-group> \
     --sku standard \
     --location eastus
   ```

2. **Configure environment variables**
   ```bash
   # Required for vector memory
   export AZURE_AI_SEARCH_ENDPOINT="https://<search-service-name>.search.windows.net"
   export AZURE_OPENAI_ENDPOINT="https://<openai-resource>.openai.azure.com"
   export AZURE_OPENAI_EMBEDDING_DEPLOYMENT="text-embedding-ada-002"
   ```

3. **Initialize the search index**
   The index is automatically created on first use with:
   - Vector search profile using HNSW algorithm
   - 1536-dimensional embeddings (text-embedding-ada-002)
   - Full-text search on content and summary fields
   - Filterable fields for type, tags, importance, and dates

4. **Migrate existing memories**
   ```csharp
   // Batch migrate from blob storage to vector search
   var migratedCount = await memoryAgent.BatchMigrateMemoriesAsync(batchSize: 100);
   Console.WriteLine($"Migrated {migratedCount} memories to vector search");
   ```

### Traditional Blob Storage

For simpler deployments without semantic search:

### Redis Cache Integration

For distributed short-term memory, replace `InMemoryShortTermMemoryService` with Redis:

1. Create an Azure Cache for Redis instance
2. Install the StackExchange.Redis NuGet package
3. Implement the `IShortTermMemoryService` interface using Redis commands
4. Configure connection strings and failover policies

## Architecture Benefits

1. **Scalability** - In-memory services can be swapped for Azure services without code changes
2. **Learning** - Batch process extracts patterns and insights automatically
3. **Maintenance** - Old memories are automatically cleaned up
4. **Searchability** - Memories can be queried by content, type, or tags
5. **Importance Weighting** - Critical memories are retained longer
6. **Knowledge Continuity** - Agent maintains context across iterations, building incrementally on past learnings
7. **Additive Knowledge** - Memories are never overwritten, only added, ensuring accumulated knowledge is preserved
8. **Semantic Understanding** - Vector search finds relevant memories by meaning, not just keywords (NEW)
9. **Task Deduplication** - Automatically detects and prevents repeating completed work (NEW)
10. **Performance Monitoring** - Search quality metrics provide visibility into memory system effectiveness (NEW)

## Integration with Agent Loop

The autonomous agent automatically:
- **Loads recent memories at startup** - Uses vector search when available for semantic relevance
- **Checks for similar tasks** - Prevents duplication of already-completed work
- **Includes memory context in prompts** - Each iteration includes accumulated knowledge from previous iterations
- **Refreshes memories periodically** - Updates memory context every 10 iterations or hourly to stay current
- Stores observations before each thinking cycle
- Records reasoning and decisions in long-term memory (and vector search if enabled)
- Logs successful actions and errors with automatic embedding generation
- Pauses for batch learning at configured intervals
- Uses memory context to inform future decisions (memory is always additive, never overwritten)
- **Evaluates search quality** - Monitors precision and relevance of memory retrieval

### Memory Context in Prompts

Each agent iteration receives:
- **With Vector Search (when available):**
  - Top memories semantically relevant to current directive
  - Automatically detects and prioritizes similar completed tasks
  - Search quality metrics logged for monitoring
  - Precision-weighted results (high-confidence matches prioritized)
  
- **Traditional Mode (fallback):**
  - Top 20 most relevant memories (weighted by importance and recency)
  - Memories grouped by type (Learning, Success, Reflection, Decision)
  - Summary and importance score for each memory
  - Full content for high-importance memories (≥0.8)

This ensures the agent:
- Builds on previous knowledge incrementally
- Doesn't repeat failed approaches or completed tasks
- Leverages successful patterns from semantically similar situations
- Maintains continuity across iterations

## Future Enhancements

- ~~Semantic embeddings for similarity search~~ ✅ **Implemented with Azure AI Search**
- ~~Task deduplication to prevent repeated work~~ ✅ **Implemented in MemoryAgent**
- Memory importance decay over time
- Cross-agent memory sharing
- Knowledge graph construction
- Automated memory categorization using AI
- Multi-modal embeddings (text + code)
- Federated search across multiple memory stores

### Multi-Agent Architecture Considerations

The current design is prepared for future multi-agent scenarios:

1. **Interface-based Memory Services** - `ILongTermMemoryService` and `IShortTermMemoryService` allow different agents to use shared or isolated memory stores
2. **Memory Tagging** - Tags enable filtering memories by agent, task type, or domain
3. **Tool Isolation** - MCP integration pattern supports agent-specific tools (e.g., Playwright for web agents, Python sandbox for code agents)
4. **Additive Knowledge** - Memory append-only design ensures agents can safely share knowledge without conflicts

Future multi-agent capabilities could include:
- **Specialized Agents**: Web navigation agent (with Playwright), code execution agent (with Python sandbox), research agent (with enhanced search)
- **Agent Coordination**: Shared long-term memory with agent-specific short-term memory
- **Knowledge Transfer**: Learnings from one agent available to others through consolidated memories
- **Task Delegation**: Primary agent delegating specialized tasks to domain-specific agents
