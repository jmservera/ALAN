# Azure Storage Long-Term Memory

## Overview

ALAN now supports Azure Blob Storage as a backend for long-term memory, providing persistent, scalable storage for agent memories.

## Configuration

### Environment Variable

Set the Azure Storage connection string:

```bash
export AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;AccountName=<account>;AccountKey=<key>;EndpointSuffix=core.windows.net"
```

### Application Settings

Or configure in `appsettings.json`:

```json
{
  "AzureStorage": {
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=<account>;AccountKey=<key>;EndpointSuffix=core.windows.net"
  }
}
```

## Behavior

- **With Azure Storage**: When a connection string is provided, ALAN uses `AzureBlobLongTermMemoryService` to store memories as JSON blobs in Azure Storage
- **Without Azure Storage**: Falls back to `InMemoryLongTermMemoryService` for development and testing

## Storage Structure

Memories are stored with the following structure:

```
agent-memories/
  ├── 2024/
  │   ├── 12/
  │   │   ├── 08/
  │   │   │   ├── {memory-id-1}.json
  │   │   │   ├── {memory-id-2}.json
  │   │   │   └── ...
```

Each memory blob includes metadata for efficient searching:
- `type`: Memory type (Observation, Learning, etc.)
- `importance`: Importance score (0.0-1.0)
- `timestamp`: ISO 8601 timestamp
- `summary`: Truncated summary
- `tag0-tag4`: Up to 5 tags

## Features

### Automatic Container Creation
The service automatically creates the `agent-memories` container if it doesn't exist.

### Date-Based Organization
Memories are organized by date (YYYY/MM/DD) for efficient retrieval and management.

### Metadata Search
Blob metadata enables quick filtering by type, importance, and tags without downloading content.

### Search Capabilities
- Search by content keywords
- Filter by memory type
- Filter by tags
- Sort by timestamp or importance

## Usage Example

```csharp
// Store a memory
var memory = new MemoryEntry
{
    Type = MemoryType.Learning,
    Content = "Discovered async/await improves responsiveness",
    Summary = "Async programming benefit",
    Importance = 0.8,
    Tags = new List<string> { "async", "performance", "learning" }
};

await longTermMemory.StoreMemoryAsync(memory);

// Search memories
var asyncMemories = await longTermMemory.SearchMemoriesAsync("async", maxResults: 10);

// Get memories by type
var learnings = await longTermMemory.GetMemoriesByTypeAsync(MemoryType.Learning, maxResults: 50);
```

## Performance Considerations

### Access Tracking
Due to Azure Blob Storage limitations, memory access counts are not persisted to avoid re-uploading blobs. Access is logged but not stored.

### Search Performance
- Metadata-based filtering is fast
- Full content search requires downloading blobs
- For large-scale deployments, consider Azure Cognitive Search for semantic search

## Migration from In-Memory

To migrate from in-memory to Azure Storage:

1. Set the `AZURE_STORAGE_CONNECTION_STRING` environment variable
2. Restart the agent
3. Previous in-memory data will be lost (as expected)
4. New memories will be persisted to Azure Storage

## Cost Considerations

Azure Blob Storage costs include:
- Storage capacity (very low for JSON blobs)
- Operations (puts, gets, lists)
- Data transfer (egress)

For typical agent workloads:
- ~100 memories/day = ~10KB/day
- Monthly storage cost: < $0.01
- Operations cost: Minimal for moderate usage

## Security

### Connection String Security
- Never commit connection strings to source control
- Use Azure Key Vault for production deployments
- Rotate storage account keys regularly

### Access Control
- Use Azure RBAC to control access to the storage account
- Consider using Managed Identity instead of connection strings
- Enable Azure Storage encryption at rest (enabled by default)

## Troubleshooting

### Container Not Created
Ensure the connection string has permissions to create containers (`Storage Blob Data Contributor` role).

### Slow Search Performance
For better search performance:
- Enable blob indexing in Azure Storage
- Consider using Azure Cognitive Search for semantic search
- Implement caching for frequently accessed memories

### Memory Not Found
The service searches back 365 days. Older memories may not be retrieved by ID lookups.

## Future Enhancements

- Async batch operations for better performance
- Integration with Azure Cognitive Search for semantic search
- Support for blob lifecycle management policies
- Compression for large memories
- Versioning for memory updates
