# Dual Memory Collections Architecture

## Overview

ALAN now supports separate vector database collections for short-term and long-term memories, enabling distinct retrieval strategies for each type:

- **Short-term memories**: Immediate, recent context (all recent items)
- **Long-term memories**: Consolidated, important learnings (semantic search for relevance)

## Architecture Changes

### Collection Separation

| Collection    | Purpose                          | Retrieval Method      | Retention               |
|---------------|----------------------------------|-----------------------|-------------------------|
| `short-term`  | Recent thoughts and actions      | Time-ordered (all)    | 8 hours (TTL in blob)   |
| `long-term`   | Consolidated learnings           | Semantic search       | Permanent               |

### Memory Lifecycle

```
1. Thought/Action Created
   ↓
2. StateManager stores in blob (8h TTL) + vector DB (short-term collection)
   ↓
3. After 6 hours, MemoryConsolidationService evaluates importance
   ↓
4. Important items (≥0.5) promoted to long-term blob + vector DB (long-term collection)
   ↓
5. Agent queries both:
   - Short-term: GetAllRecentMemoriesAsync() - immediate context
   - Long-term: SearchMemoriesAsync() - relevant learnings
```

## Implementation Details

### Interface Changes

**IVectorMemoryService** now includes `collection` parameter with default values:

```csharp
Task<string> StoreMemoryAsync(MemoryEntry memory, string collection = "long-term", CancellationToken cancellationToken = default);

Task<List<MemorySearchResult>> SearchMemoriesAsync(
    string query,
    int maxResults = 10,
    double minScore = 0.7,
    MemorySearchFilters? filters = null,
    string collection = "long-term",
    CancellationToken cancellationToken = default);

Task<MemoryEntry?> GetMemoryAsync(string id, string collection = "long-term", CancellationToken cancellationToken = default);

Task<bool> DeleteMemoryAsync(string id, string collection = "long-term", CancellationToken cancellationToken = default);

// New method for retrieving all recent memories without semantic search
Task<List<MemoryEntry>> GetAllRecentMemoriesAsync(
    int maxResults = 50,
    string collection = "short-term",
    CancellationToken cancellationToken = default);
```

### Service Implementations

#### QdrantMemoryService

- **InitializeAsync**: Creates both `short-term` and `long-term` collections
- **StoreMemoryAsync**: Stores in specified collection (default: `long-term`)
- **SearchMemoriesAsync**: Searches specified collection (default: `long-term`)
- **GetAllRecentMemoriesAsync**: Retrieves all recent memories from specified collection using Scroll API

#### AzureAISearchMemoryService

- **InitializeAsync**: Creates both `short-term` and `long-term` indexes
- **StoreMemoryAsync**: Stores in specified index (default: `long-term`)
- **SearchMemoriesAsync**: Searches specified index (default: `long-term`)
- **GetAllRecentMemoriesAsync**: Retrieves all recent memories ordered by timestamp

### StateManager Changes

Now explicitly stores memories in `short-term` collection:

```csharp
await _memoryAgent.MigrateMemoryToVectorSearchAsync(memory, default, "short-term");
```

### MemoryConsolidationService Changes

Stores consolidated memories in `long-term` collection:

```csharp
await _memoryAgent.MigrateMemoryToVectorSearchAsync(memory, cancellationToken, "long-term");
```

### MemoryAgent Enhancements

#### New Methods

```csharp
// Get all recent short-term memories (immediate context)
Task<List<MemoryEntry>> GetShortTermContextAsync(int maxResults = 50, CancellationToken cancellationToken = default);

// Search long-term consolidated memories (relevant context)
Task<List<MemorySearchResult>> SearchLongTermContextAsync(string taskDescription, int maxResults = 20, CancellationToken cancellationToken = default);

// Combine both for comprehensive agent context
Task<string> BuildCombinedMemoryContextAsync(string taskDescription, int maxShortTerm = 30, int maxLongTerm = 15, CancellationToken cancellationToken = default);
```

#### BuildCombinedMemoryContextAsync Output Format

```markdown
## Recent Context (Short-term Memory)

- [Thought] Planning: Analyze user request...
- [Action] ExecuteCode: Run validation script
  Full content for high-importance items (≥0.8)

## Relevant Past Experience (Long-term Memory)

- [Learning] Error handling patterns (relevance: 0.92)
  Full content for important items (≥0.7)
- [Success] Similar task completed (relevance: 0.85)
```

## Usage in AutonomousAgent

### Current Implementation

AutonomousAgent will need to be updated to use the new dual memory approach:

```csharp
// In ThinkAndActAsync or similar method
var memoryContext = await _memoryAgent.BuildCombinedMemoryContextAsync(
    taskDescription: currentTask,
    maxShortTerm: 30,  // Last 30 thoughts/actions
    maxLongTerm: 15,   // Top 15 relevant learnings
    cancellationToken);

// Include in prompt
var prompt = $@"
{memoryContext}

Current Task: {currentTask}

Based on recent context and relevant past experience, what should you do next?
";
```

### Memory Weight Distribution

- **Short-term**: Provides continuity and immediate context
  - What was I just thinking?
  - What actions did I just take?
  - What was the recent outcome?

- **Long-term**: Provides learned knowledge and relevant patterns
  - What have I learned about similar tasks?
  - What patterns were successful?
  - What mistakes should I avoid?

## Configuration

### Collection Names

Collections are hardcoded as constants:
- Short-term: `"short-term"`
- Long-term: `"long-term"`

### Memory Limits

Configurable via parameters:
- Short-term context: Default 50 items (configurable in `GetShortTermContextAsync`)
- Long-term context: Default 20 items (configurable in `SearchLongTermContextAsync`)

### Consolidation Settings

From MemoryConsolidationService:
- Consolidation interval: Every 6 hours
- Importance threshold: ≥0.5 for promotion to long-term
- Short-term TTL: 8 hours (blob storage)

## Testing

All 187 tests passing:
- ✅ 87 tests in ALAN.Agent.Tests
- ✅ 88 tests in ALAN.Shared.Tests
- ✅ 12 tests in ALAN.ChatApi.Tests

### Test Updates

Updated mock setups to include collection parameter:

```csharp
_mockVectorMemory
    .Setup(v => v.SearchMemoriesAsync(
        It.IsAny<string>(),
        It.IsAny<int>(),
        It.IsAny<double>(),
        It.IsAny<MemorySearchFilters?>(),
        It.IsAny<string>(),  // Collection parameter
        It.IsAny<CancellationToken>()))
    .ReturnsAsync(expectedResults);
```

## Migration Notes

### Backward Compatibility

Default parameter values ensure backward compatibility:
- Existing code without collection parameter defaults to `"long-term"`
- Old memories in single collection can coexist with new dual-collection approach

### Vector Database Setup

Both Qdrant and Azure AI Search will automatically create collections/indexes on first use via `InitializeAsync`.

### Existing Memories

Existing memories in a single collection will remain accessible but should be migrated:
1. Read from old collection
2. Determine collection based on tags or age
3. Store in appropriate new collection

## Performance Considerations

### Short-term Retrieval

- Uses time-ordered retrieval (no embedding generation)
- Fast access to recent context
- Limited to recent items (8-hour TTL)

### Long-term Retrieval

- Uses semantic search (requires embedding generation)
- Finds relevant knowledge regardless of age
- More computationally expensive but highly targeted

### Combined Context

- Two parallel queries (can be optimized)
- Total items configurable to control context size
- Formatted markdown adds ~2-5KB per memory item

## Future Enhancements

1. **Automatic context sizing**: Adjust limits based on token budget
2. **Smart filtering**: Filter short-term by relevance threshold
3. **Temporal weighting**: Decay importance over time in short-term
4. **Cross-collection deduplication**: Avoid redundant context
5. **Collection rotation**: Archive old short-term collections
6. **Memory compression**: Summarize old memories before promotion
7. **Multi-agent support**: Per-agent collections or shared knowledge base

## References

- [Memory Architecture](MEMORY_ARCHITECTURE.md)
- [Azure Storage Memory](AZURE_STORAGE_MEMORY.md)
- [Vector Memory Guide](VECTOR_MEMORY_GUIDE.md)
