using ALAN.Shared.Services.Resilience;
using Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Polly;
using Xunit;

namespace ALAN.Shared.Tests.Services.Resilience;

/// <summary>
/// Tests for ResiliencePolicy retry pipelines.
/// Uses FakeTimeProvider to eliminate actual delays and ensure fast test execution.
/// Helper methods encapsulate common patterns for executing pipelines with time advancement.
/// </summary>
public class ResiliencePolicyTests
{
    private readonly Mock<ILogger> _mockLogger;
    private readonly FakeTimeProvider _fakeTimeProvider;

    public ResiliencePolicyTests()
    {
        _mockLogger = new Mock<ILogger>();
        _fakeTimeProvider = new FakeTimeProvider();
    }

    /// <summary>
    /// Core logic for executing with fake time advancement.
    /// </summary>
    private async Task AdvanceTimeUntilCompleteAsync(Task executeTask, int maxAttempts, Func<int> getAttemptCount)
    {
        while (!executeTask.IsCompleted && getAttemptCount() < maxAttempts)
        {
            await Task.Delay(10);
            _fakeTimeProvider.Advance(TimeSpan.FromSeconds(10));
        }
    }

    /// <summary>
    /// Executes a pipeline operation with fake time provider, automatically advancing time to trigger retries.
    /// </summary>
    private async Task<T> ExecuteWithFakeTimeAsync<T>(
        ResiliencePipeline<T> pipeline,
        Func<CancellationToken, ValueTask<T>> operation,
        int maxAttempts,
        Func<int> getAttemptCount)
    {
        var executeTask = Task.Run(async () => await pipeline.ExecuteAsync(operation));
        await AdvanceTimeUntilCompleteAsync(executeTask, maxAttempts, getAttemptCount);
        return await executeTask;
    }

    /// <summary>
    /// Executes a non-generic pipeline operation with fake time provider.
    /// </summary>
    private async Task ExecuteWithFakeTimeAsync(
        ResiliencePipeline pipeline,
        Func<CancellationToken, ValueTask> operation,
        int maxAttempts,
        Func<int> getAttemptCount)
    {
        var executeTask = Task.Run(async () => await pipeline.ExecuteAsync(operation));
        await AdvanceTimeUntilCompleteAsync(executeTask, maxAttempts, getAttemptCount);
        await executeTask;
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_RetriesOnThrottling()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, _fakeTimeProvider);
        int attemptCount = 0;

        // Act
        var result = await ExecuteWithFakeTimeAsync(
            pipeline,
            ct =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new RequestFailedException(429, "Too Many Requests");
                }
                return ValueTask.FromResult(42);
            },
            maxAttempts: 3,
            getAttemptCount: () => attemptCount);

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(3, attemptCount); // Initial attempt + 2 retries
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_RetriesOnServiceUnavailable()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, _fakeTimeProvider);
        int attemptCount = 0;

        // Act
        var result = await ExecuteWithFakeTimeAsync(
            pipeline,
            ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new RequestFailedException(503, "Service Unavailable");
                }
                return ValueTask.FromResult(100);
            },
            maxAttempts: 2,
            getAttemptCount: () => attemptCount);

        // Assert
        Assert.Equal(100, result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_FailsAfterMaxRetries()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, _fakeTimeProvider);
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<RequestFailedException>(async () => await ExecuteWithFakeTimeAsync(
            pipeline,
            ct =>
            {
                attemptCount++;
                throw new RequestFailedException(503, "Service Unavailable");
            },
            maxAttempts: 5,
            getAttemptCount: () => attemptCount));

        // Should try: initial + 3 retries = 4 total attempts
        Assert.Equal(4, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_DoesNotRetryOnNonTransientErrors()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object);
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<RequestFailedException>(async () =>
        {
            await pipeline.ExecuteAsync<int>(ct =>
            {
                attemptCount++;
                throw new RequestFailedException(404, "Not Found");
            });
        });

        // Should only try once (404 is not a transient error)
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task CreateOpenAIRetryPipeline_RetriesOnRateLimit()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline<string>(_mockLogger.Object, _fakeTimeProvider);
        int attemptCount = 0;

        // Act
        var result = await ExecuteWithFakeTimeAsync(
            pipeline,
            ct =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new RequestFailedException(429, "Rate limit exceeded");
                }
                return ValueTask.FromResult("success");
            },
            maxAttempts: 3,
            getAttemptCount: () => attemptCount);

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task CreateOpenAIRetryPipeline_HasMoreRetriesThanStorage()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline<int>(_mockLogger.Object, _fakeTimeProvider);
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<RequestFailedException>( async()=> await ExecuteWithFakeTimeAsync(
            pipeline,
            ct =>
            {
                attemptCount++;
                throw new RequestFailedException(429, "Rate limit");
            },
            maxAttempts: 7,
            getAttemptCount: () => attemptCount));

        // OpenAI should retry more times (initial + 5 retries = 6 total)
        Assert.Equal(6, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_WithoutGeneric_Succeeds()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline(_mockLogger.Object, _fakeTimeProvider);
        int attemptCount = 0;

        // Act
        await ExecuteWithFakeTimeAsync(
            pipeline,
            ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new RequestFailedException(503, "Service Unavailable");
                }
                return ValueTask.CompletedTask;
            },
            maxAttempts: 2,
            getAttemptCount: () => attemptCount);

        // Assert
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateOpenAIRetryPipeline_WithoutGeneric_Succeeds()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline(_mockLogger.Object, _fakeTimeProvider);
        int attemptCount = 0;

        // Act
        await ExecuteWithFakeTimeAsync(
            pipeline,
            ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new RequestFailedException(429, "Rate limit");
                }
                return ValueTask.CompletedTask;
            },
            maxAttempts: 2,
            getAttemptCount: () => attemptCount);

        // Assert
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_RetriesOnTimeout()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<string>(_mockLogger.Object, _fakeTimeProvider);
        int attemptCount = 0;

        // Act
        var result = await ExecuteWithFakeTimeAsync(
            pipeline,
            ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new TimeoutException("Operation timed out");
                }
                return ValueTask.FromResult("completed");
            },
            maxAttempts: 2,
            getAttemptCount: () => attemptCount);

        // Assert
        Assert.Equal("completed", result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_LogsRetryAttempts()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, _fakeTimeProvider);
        int attemptCount = 0;

        // Act
        await ExecuteWithFakeTimeAsync(
            pipeline,
            ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new RequestFailedException(503, "Service Unavailable");
                }
                return ValueTask.FromResult(1);
            },
            maxAttempts: 2,
            getAttemptCount: () => attemptCount);

        // Assert - verify logger was called
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Azure Storage operation failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_PropagatesCancellation()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object);
        var cts = new CancellationTokenSource();
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                cts.Cancel(); // Cancel after first attempt
                ct.ThrowIfCancellationRequested();
                return await Task.FromResult(1);
            }, cts.Token);
        });

        // Should only attempt once, no retries on cancellation
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_StopsRetryingOnCancellation()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, _fakeTimeProvider);
        var cts = new CancellationTokenSource();
        int attemptCount = 0;

        // Act & Assert
        var executeTask = Task.Run(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                if (attemptCount == 1)
                {
                    // First attempt fails with retryable error
                    throw new RequestFailedException(503, "Service Unavailable");
                }
                // Cancel before second retry
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                return await Task.FromResult(1);
            }, cts.Token);
        });

        // Advance time to trigger retry
        while (!executeTask.IsCompleted && attemptCount < 2)
        {
            await Task.Delay(10);
            _fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await executeTask);

        // Should attempt twice max (initial + one retry before cancellation)
        Assert.True(attemptCount <= 2, $"Expected <= 2 attempts but got {attemptCount}");
    }

    [Fact]
    public async Task CreateOpenAIRetryPipeline_PropagatesCancellation()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline<string>(_mockLogger.Object);
        var cts = new CancellationTokenSource();
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                cts.Cancel(); // Cancel after first attempt
                ct.ThrowIfCancellationRequested();
                return await Task.FromResult("result");
            }, cts.Token);
        });

        // Should only attempt once, no retries on cancellation
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_NonGeneric_PropagatesCancellation()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline(_mockLogger.Object);
        var cts = new CancellationTokenSource();
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                await Task.CompletedTask;
            }, cts.Token);
        });

        // Should only attempt once, no retries on cancellation
        Assert.Equal(1, attemptCount);
    }

    [Fact]
    public async Task CreateOpenAIRetryPipeline_NonGeneric_PropagatesCancellation()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline(_mockLogger.Object);
        var cts = new CancellationTokenSource();
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                cts.Cancel();
                ct.ThrowIfCancellationRequested();
                await Task.CompletedTask;
            }, cts.Token);
        });

        // Should only attempt once, no retries on cancellation
        Assert.Equal(1, attemptCount);
    }
}
