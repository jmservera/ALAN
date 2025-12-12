# Resiliency in ALAN

ALAN implements comprehensive resiliency patterns to handle transient failures, throttling, and service unavailability across all Azure service integrations.

## Overview

The system uses [Polly](https://www.pollydocs.org/) (version 8.5.0) to implement retry policies with exponential backoff for all external service calls. This ensures the agent can recover gracefully from temporary issues without manual intervention.

## Resilience Policies

### Azure Storage Operations (Blob & Queue)

**Configuration:**
- **Max Retry Attempts:** 3
- **Initial Delay:** 1 second
- **Backoff Type:** Exponential with jitter
- **Total Max Time:** ~7 seconds (1s + 2s + 4s)

**Handled Errors:**
- `429` - Too Many Requests (throttling)
- `503` - Service Unavailable
- `504` - Gateway Timeout
- `408` - Request Timeout
- `TimeoutException`
- `OperationCanceledException`

**Usage Example:**
```csharp
var pipeline = ResiliencePolicy.CreateStorageRetryPipeline(logger);

var response = await pipeline.ExecuteAsync(async ct =>
    await blobClient.UploadAsync(stream, uploadOptions, ct),
    cancellationToken);
```

### Azure OpenAI Operations

**Configuration:**
- **Max Retry Attempts:** 5
- **Initial Delay:** 2 seconds
- **Backoff Type:** Exponential with jitter
- **Total Max Time:** ~62 seconds (2s + 4s + 8s + 16s + 32s)

**Handled Errors:**
- `429` - Rate Limit Exceeded
- `503` - Service Temporarily Unavailable
- `504` - Gateway Timeout
- `500` - Internal Server Error (may be transient)
- `TimeoutException`
- `OperationCanceledException`

**Usage Example:**
```csharp
var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline(logger);

var response = await pipeline.ExecuteAsync(async ct =>
    await chatClient.CompleteChatAsync(messages, ct),
    cancellationToken);
```

## Architecture

### ResiliencePolicy Helper

Located in `src/ALAN.Shared/Services/Resilience/ResiliencePolicy.cs`, this static class provides factory methods for creating pre-configured resilience pipelines:

```csharp
public static class ResiliencePolicy
{
    // For Azure Storage Blob and Queue operations
    public static ResiliencePipeline<TResult> CreateStorageRetryPipeline<TResult>(ILogger logger)
    public static ResiliencePipeline CreateStorageRetryPipeline(ILogger logger)
    
    // For Azure OpenAI operations
    public static ResiliencePipeline<TResult> CreateOpenAIRetryPipeline<TResult>(ILogger logger)
    public static ResiliencePipeline CreateOpenAIRetryPipeline(ILogger logger)
}
```

### Integrated Services

Resiliency has been added to the following services:

1. **AzureBlobShortTermMemoryService** - Short-term memory cache operations
2. **AzureBlobLongTermMemoryService** - Long-term memory storage operations
3. **AzureStorageQueueService** - Human input queue operations

Each service initializes a resilience pipeline in its constructor and wraps all external API calls with retry logic.

### Azure OpenAI Built-in Resiliency

**Note:** Azure OpenAI SDK (`Azure.AI.OpenAI` v2.7.0) and Microsoft.Extensions.AI already include built-in retry logic and error handling. The `CreateOpenAIRetryPipeline` methods are provided for:
- **Custom scenarios** where additional retry control is needed
- **Consistency** with other service retry patterns
- **Future extensions** to other AI providers

For standard Azure OpenAI usage in ALAN, the SDK's default behavior is sufficient and no additional wrapping is required.

## Logging

All retry attempts are logged at `Warning` level with detailed information:

```
Azure Storage operation failed. Attempt 2 of 3. Waiting 2000ms before retry. Error: Service Unavailable
```

This helps with:
- Debugging transient issues
- Monitoring service health
- Identifying persistent problems that need attention

## Benefits

### Improved Reliability
- Automatic recovery from transient failures
- No manual intervention required for temporary issues
- Graceful degradation under load

### Better User Experience
- Reduced error rates
- Consistent operation even during Azure service hiccups
- Transparent handling of rate limits

### Cost Efficiency
- Prevents wasted compute from failed operations
- Optimizes token usage by avoiding incomplete operations
- Reduces support burden

## Best Practices

### When to Use
✅ **Always wrap external API calls** with resilience pipelines
✅ **Use storage pipeline** for Azure Storage operations (Blob, Queue, Table)
✅ **Use OpenAI pipeline** for Azure OpenAI and other AI service calls
✅ **Pass cancellation tokens** through the pipeline for proper cancellation

### When NOT to Use
❌ **Don't wrap local operations** (in-memory operations don't need retry)
❌ **Don't wrap database transactions** (use database-specific patterns)
❌ **Don't wrap idempotent check operations** if failure is acceptable

### Adding Resiliency to New Code

```csharp
// 1. Initialize pipeline in constructor
public class MyService
{
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly ILogger<MyService> _logger;
    
    public MyService(ILogger<MyService> logger)
    {
        _logger = logger;
        _resiliencePipeline = ResiliencePolicy.CreateStorageRetryPipeline(logger);
    }
    
    // 2. Wrap external calls
    public async Task<Data> GetDataAsync(CancellationToken cancellationToken)
    {
        return await _resiliencePipeline.ExecuteAsync(async ct =>
            await _externalClient.GetAsync(ct),
            cancellationToken);
    }
}
```

## Testing

Comprehensive unit tests verify the resilience behavior:

- **Retry on transient errors** (429, 503, 504, 408)
- **Success after retries** (operation succeeds on 2nd or 3rd attempt)
- **Failure after max retries** (all retries exhausted)
- **No retry on non-transient errors** (404, 401, etc.)
- **Logging of retry attempts**

Run tests with:
```bash
dotnet test --filter "FullyQualifiedName~ResiliencePolicyTests"
```

## Configuration

Currently, retry parameters are hardcoded based on Azure Well-Architected Framework best practices. Future enhancement could add configuration support:

```json
{
  "Resilience": {
    "Storage": {
      "MaxRetryAttempts": 3,
      "InitialDelay": "00:00:01"
    },
    "OpenAI": {
      "MaxRetryAttempts": 5,
      "InitialDelay": "00:00:02"
    }
  }
}
```

## Monitoring

Monitor resilience effectiveness by:

1. **Log Analysis** - Count retry warnings in application logs
2. **Metrics** - Track retry counts and success rates
3. **Alerts** - Alert on high retry rates (indicates service issues)

## References

- [Polly Documentation](https://www.pollydocs.org/)
- [Azure Well-Architected Framework - Reliability](https://learn.microsoft.com/azure/architecture/framework/resiliency/)
- [.NET Resilience](https://learn.microsoft.com/dotnet/core/resilience/)
- [Azure Storage Retry Guidelines](https://learn.microsoft.com/azure/storage/common/storage-retry-policy)
- [Azure OpenAI Rate Limits](https://learn.microsoft.com/azure/ai-services/openai/quotas-limits)
