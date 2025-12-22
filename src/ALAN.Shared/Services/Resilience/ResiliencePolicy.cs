using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Azure;

namespace ALAN.Shared.Services.Resilience;

/// <summary>
/// Provides resilience policies for Azure service operations.
/// Implements retry with exponential backoff.
/// </summary>
public static class ResiliencePolicy
{
    /// <summary>
    /// Creates a retry pipeline for Azure Storage operations.
    /// Handles transient failures, throttling, and timeouts.
    /// </summary>
    /// <param name="logger">Logger instance for retry warnings.</param>
    /// <param name="timeProvider">Optional time provider for testing (default: system time).</param>
    public static ResiliencePipeline<TResult> CreateStorageRetryPipeline<TResult>(ILogger logger, TimeProvider? timeProvider = null)
    {
        return CreateRetryPipeline<TResult>(
            logger,
            maxRetryAttempts: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            serviceType: "Azure Storage",
            shouldHandleStatus: status => status == 429 || status == 503 || status == 504 || status == 408,
            timeProvider: timeProvider);
    }

    /// <summary>
    /// Creates a retry pipeline for Azure Storage operations without return type.
    /// Handles transient failures, throttling, and timeouts.
    /// </summary>
    /// <param name="logger">Logger instance for retry warnings.</param>
    /// <param name="timeProvider">Optional time provider for testing (default: system time).</param>
    public static ResiliencePipeline CreateStorageRetryPipeline(ILogger logger, TimeProvider? timeProvider = null)
    {
        return CreateRetryPipeline(
            logger,
            maxRetryAttempts: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            serviceType: "Azure Storage",
            shouldHandleStatus: status => status == 429 || status == 503 || status == 504 || status == 408,
            timeProvider: timeProvider);
    }

    /// <summary>
    /// Creates a retry pipeline for Azure OpenAI operations.
    /// Handles rate limiting and transient failures.
    /// </summary>
    /// <param name="logger">Logger instance for retry warnings.</param>
    /// <param name="timeProvider">Optional time provider for testing (default: system time).</param>
    public static ResiliencePipeline<TResult> CreateOpenAIRetryPipeline<TResult>(ILogger logger, TimeProvider? timeProvider = null)
    {
        return CreateRetryPipeline<TResult>(
            logger,
            maxRetryAttempts: 5,
            initialDelay: TimeSpan.FromSeconds(2),
            serviceType: "Azure OpenAI",
            shouldHandleStatus: status => status == 429 || status == 503 || status == 504 || status == 500,
            timeProvider: timeProvider);
    }

    /// <summary>
    /// Creates a retry pipeline for Azure OpenAI operations without return type.
    /// Handles rate limiting and transient failures.
    /// </summary>
    /// <param name="logger">Logger instance for retry warnings.</param>
    /// <param name="timeProvider">Optional time provider for testing (default: system time).</param>
    public static ResiliencePipeline CreateOpenAIRetryPipeline(ILogger logger, TimeProvider? timeProvider = null)
    {
        return CreateRetryPipeline(
            logger,
            maxRetryAttempts: 5,
            initialDelay: TimeSpan.FromSeconds(2),
            serviceType: "Azure OpenAI",
            shouldHandleStatus: status => status == 429 || status == 503 || status == 504 || status == 500,
            timeProvider: timeProvider);
    }

    /// <summary>
    /// Helper method to create a retry pipeline with specified configuration.
    /// </summary>
    private static ResiliencePipeline<TResult> CreateRetryPipeline<TResult>(
        ILogger logger,
        int maxRetryAttempts,
        TimeSpan initialDelay,
        string serviceType,
        Func<int, bool> shouldHandleStatus,
        TimeProvider? timeProvider = null)
    {
        var builder = new ResiliencePipelineBuilder<TResult>();
        if (timeProvider != null)
        {
            builder.TimeProvider = timeProvider;
        }
        return builder
            .AddRetry(new RetryStrategyOptions<TResult>
            {
                MaxRetryAttempts = maxRetryAttempts,
                Delay = initialDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<TResult>()
                    .HandleInner<RequestFailedException>(ex => shouldHandleStatus(ex.Status))
                    .HandleInner<TimeoutException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "{ServiceType} operation failed. Attempt {AttemptNumber} of {MaxAttempts}. Waiting {Delay}ms before retry. Error: {Error}",
                        serviceType,
                        args.AttemptNumber,
                        maxRetryAttempts + 1, // Total attempts = retries + initial attempt
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    /// <summary>
    /// Helper method to create a retry pipeline without return type.
    /// </summary>
    private static ResiliencePipeline CreateRetryPipeline(
        ILogger logger,
        int maxRetryAttempts,
        TimeSpan initialDelay,
        string serviceType,
        Func<int, bool> shouldHandleStatus,
        TimeProvider? timeProvider = null)
    {
        var builder = new ResiliencePipelineBuilder();
        if (timeProvider != null)
        {
            builder.TimeProvider = timeProvider;
        }
        return builder
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = maxRetryAttempts,
                Delay = initialDelay,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder()
                    .HandleInner<RequestFailedException>(ex => shouldHandleStatus(ex.Status))
                    .HandleInner<TimeoutException>(),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "{ServiceType} operation failed. Attempt {AttemptNumber} of {MaxAttempts}. Waiting {Delay}ms before retry. Error: {Error}",
                        serviceType,
                        args.AttemptNumber,
                        maxRetryAttempts + 1, // Total attempts = retries + initial attempt
                        args.RetryDelay.TotalMilliseconds,
                        args.Outcome.Exception?.Message ?? "Unknown");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
