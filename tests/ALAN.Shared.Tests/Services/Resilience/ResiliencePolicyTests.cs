using ALAN.Shared.Services.Resilience;
using Azure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Polly;
using Xunit;

namespace ALAN.Shared.Tests.Services.Resilience;

public class ResiliencePolicyTests
{
    private readonly Mock<ILogger> _mockLogger;

    public ResiliencePolicyTests()
    {
        _mockLogger = new Mock<ILogger>();
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_RetriesOnThrottling()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, fakeTimeProvider);
        int attemptCount = 0;

        // Act - Execute pipeline in background task
        var executeTask = Task.Run(async () =>
        {
            return await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new RequestFailedException(429, "Too Many Requests");
                }
                return await Task.FromResult(42);
            });
        });

        // Advance time to trigger retries without actual delays
        while (!executeTask.IsCompleted && attemptCount < 3)
        {
            await Task.Delay(10); // Small delay to let execution progress
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(10)); // Jump forward in fake time
        }

        var result = await executeTask;

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(3, attemptCount); // Initial attempt + 2 retries
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_RetriesOnServiceUnavailable()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, fakeTimeProvider);
        int attemptCount = 0;

        // Act - Execute pipeline in background task
        var executeTask = Task.Run(async () =>
        {
            return await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new RequestFailedException(503, "Service Unavailable");
                }
                return await Task.FromResult(100);
            });
        });

        // Advance time to trigger retries
        while (!executeTask.IsCompleted && attemptCount < 2)
        {
            await Task.Delay(10);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
        }

        var result = await executeTask;

        // Assert
        Assert.Equal(100, result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_FailsAfterMaxRetries()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, fakeTimeProvider);
        int attemptCount = 0;

        // Act & Assert
        var executeTask = Task.Run(async () =>
        {
            await pipeline.ExecuteAsync<int>(ct =>
            {
                attemptCount++;
                throw new RequestFailedException(503, "Service Unavailable");
            });
        });

        // Advance time to trigger all retries
        while (!executeTask.IsCompleted && attemptCount < 5)
        {
            await Task.Delay(10);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        await Assert.ThrowsAsync<RequestFailedException>(async () => await executeTask);

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
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline<string>(_mockLogger.Object, fakeTimeProvider);
        int attemptCount = 0;

        // Act - Execute pipeline in background task
        var executeTask = Task.Run(async () =>
        {
            return await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new RequestFailedException(429, "Rate limit exceeded");
                }
                return await Task.FromResult("success");
            });
        });

        // Advance time to trigger retries
        while (!executeTask.IsCompleted && attemptCount < 3)
        {
            await Task.Delay(10);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        var result = await executeTask;

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task CreateOpenAIRetryPipeline_HasMoreRetriesThanStorage()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline<int>(_mockLogger.Object, fakeTimeProvider);
        int attemptCount = 0;

        // Act & Assert
        var executeTask = Task.Run(async () =>
        {
            await pipeline.ExecuteAsync<int>(ct =>
            {
                attemptCount++;
                throw new RequestFailedException(429, "Rate limit");
            });
        });

        // Advance time to trigger all retries
        while (!executeTask.IsCompleted && attemptCount < 7)
        {
            await Task.Delay(10);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(10));
        }

        await Assert.ThrowsAsync<RequestFailedException>(async () => await executeTask);

        // OpenAI should retry more times (initial + 5 retries = 6 total)
        Assert.Equal(6, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_WithoutGeneric_Succeeds()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline(_mockLogger.Object, fakeTimeProvider);
        int attemptCount = 0;

        // Act - Execute pipeline in background task
        var executeTask = Task.Run(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new RequestFailedException(503, "Service Unavailable");
                }
                await Task.CompletedTask;
            });
        });

        // Advance time to trigger retries
        while (!executeTask.IsCompleted && attemptCount < 2)
        {
            await Task.Delay(10);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
        }

        await executeTask;

        // Assert
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateOpenAIRetryPipeline_WithoutGeneric_Succeeds()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline(_mockLogger.Object, fakeTimeProvider);
        int attemptCount = 0;

        // Act - Execute pipeline in background task
        var executeTask = Task.Run(async () =>
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new RequestFailedException(429, "Rate limit");
                }
                await Task.CompletedTask;
            });
        });

        // Advance time to trigger retries
        while (!executeTask.IsCompleted && attemptCount < 2)
        {
            await Task.Delay(10);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
        }

        await executeTask;

        // Assert
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_RetriesOnTimeout()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<string>(_mockLogger.Object, fakeTimeProvider);
        int attemptCount = 0;

        // Act - Execute pipeline in background task
        var executeTask = Task.Run(async () =>
        {
            return await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new TimeoutException("Operation timed out");
                }
                return await Task.FromResult("completed");
            });
        });

        // Advance time to trigger retries
        while (!executeTask.IsCompleted && attemptCount < 2)
        {
            await Task.Delay(10);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
        }

        var result = await executeTask;

        // Assert
        Assert.Equal("completed", result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_LogsRetryAttempts()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, fakeTimeProvider);
        int attemptCount = 0;

        // Act - Execute pipeline in background task
        var executeTask = Task.Run(async () =>
        {
            return await pipeline.ExecuteAsync(async ct =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new RequestFailedException(503, "Service Unavailable");
                }
                return await Task.FromResult(1);
            });
        });

        // Advance time to trigger retries
        while (!executeTask.IsCompleted && attemptCount < 2)
        {
            await Task.Delay(10);
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
        }

        await executeTask;

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
        var fakeTimeProvider = new FakeTimeProvider();
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object, fakeTimeProvider);
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
            fakeTimeProvider.Advance(TimeSpan.FromSeconds(5));
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
