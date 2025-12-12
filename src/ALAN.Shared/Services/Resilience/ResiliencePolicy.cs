using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Azure;

namespace ALAN.Shared.Services.Resilience;

/// <summary>
/// Provides resilience policies for Azure service operations.
/// Implements retry with exponential backoff and circuit breaker patterns.
/// </summary>
public static class ResiliencePolicy
{
    /// <summary>
    /// Creates a retry pipeline for Azure Storage operations.
    /// Handles transient failures, throttling, and timeouts.
    /// </summary>
    public static ResiliencePipeline<TResult> CreateStorageRetryPipeline<TResult>(ILogger logger)
    {
        return new ResiliencePipelineBuilder<TResult>()
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .HandleInner<RequestFailedException>(ex => 
                        ex.Status == 429 ||  // Too Many Requests (throttling)
                        ex.Status == 503 ||  // Service Unavailable
                        ex.Status == 504 ||  // Gateway Timeout
                        ex.Status == 408)    // Request Timeout
                    .HandleInner<TimeoutException>()
                    .HandleInner<OperationCanceledException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Azure Storage operation failed. Attempt {AttemptNumber} of {MaxAttempts}. Waiting {Delay}ms before retry. Error: {Error}",
                        args.AttemptNumber,
                        3,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a retry pipeline for Azure Storage operations without return type.
    /// Handles transient failures, throttling, and timeouts.
    /// </summary>
    public static ResiliencePipeline CreateStorageRetryPipeline(ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .HandleInner<RequestFailedException>(ex => 
                        ex.Status == 429 ||  // Too Many Requests (throttling)
                        ex.Status == 503 ||  // Service Unavailable
                        ex.Status == 504 ||  // Gateway Timeout
                        ex.Status == 408)    // Request Timeout
                    .HandleInner<TimeoutException>()
                    .HandleInner<OperationCanceledException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Azure Storage operation failed. Attempt {AttemptNumber} of {MaxAttempts}. Waiting {Delay}ms before retry. Error: {Error}",
                        args.AttemptNumber,
                        3,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a retry pipeline for Azure OpenAI operations.
    /// Handles rate limiting and transient failures.
    /// </summary>
    public static ResiliencePipeline<TResult> CreateOpenAIRetryPipeline<TResult>(ILogger logger)
    {
        return new ResiliencePipelineBuilder<TResult>()
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .HandleInner<RequestFailedException>(ex => 
                        ex.Status == 429 ||  // Rate limit exceeded
                        ex.Status == 503 ||  // Service temporarily unavailable
                        ex.Status == 504 ||  // Gateway timeout
                        ex.Status == 500)    // Internal server error (may be transient)
                    .HandleInner<TimeoutException>()
                    .HandleInner<OperationCanceledException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Azure OpenAI operation failed. Attempt {AttemptNumber} of {MaxAttempts}. Waiting {Delay}ms before retry. Error: {Error}",
                        args.AttemptNumber,
                        5,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Creates a retry pipeline for Azure OpenAI operations without return type.
    /// Handles rate limiting and transient failures.
    /// </summary>
    public static ResiliencePipeline CreateOpenAIRetryPipeline(ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(2),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .HandleInner<RequestFailedException>(ex => 
                        ex.Status == 429 ||  // Rate limit exceeded
                        ex.Status == 503 ||  // Service temporarily unavailable
                        ex.Status == 504 ||  // Gateway timeout
                        ex.Status == 500)    // Internal server error (may be transient)
                    .HandleInner<TimeoutException>()
                    .HandleInner<OperationCanceledException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Azure OpenAI operation failed. Attempt {AttemptNumber} of {MaxAttempts}. Waiting {Delay}ms before retry. Error: {Error}",
                        args.AttemptNumber,
                        5,
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
