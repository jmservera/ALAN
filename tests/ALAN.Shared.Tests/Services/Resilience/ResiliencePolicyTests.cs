using ALAN.Shared.Services.Resilience;
using Azure;
using Microsoft.Extensions.Logging;
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
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object);
        int attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new RequestFailedException(429, "Too Many Requests");
            }
            return await Task.FromResult(42);
        });

        // Assert
        Assert.Equal(42, result);
        Assert.Equal(3, attemptCount); // Initial attempt + 2 retries
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_RetriesOnServiceUnavailable()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object);
        int attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new RequestFailedException(503, "Service Unavailable");
            }
            return await Task.FromResult(100);
        });

        // Assert
        Assert.Equal(100, result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_FailsAfterMaxRetries()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object);
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<RequestFailedException>(async () =>
        {
            await pipeline.ExecuteAsync<int>(async ct =>
            {
                attemptCount++;
                throw new RequestFailedException(503, "Service Unavailable");
            });
        });

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
            await pipeline.ExecuteAsync<int>(async ct =>
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
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline<string>(_mockLogger.Object);
        int attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 3)
            {
                throw new RequestFailedException(429, "Rate limit exceeded");
            }
            return await Task.FromResult("success");
        });

        // Assert
        Assert.Equal("success", result);
        Assert.Equal(3, attemptCount);
    }

    [Fact]
    public async Task CreateOpenAIRetryPipeline_HasMoreRetriesThanStorage()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline<int>(_mockLogger.Object);
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAsync<RequestFailedException>(async () =>
        {
            await pipeline.ExecuteAsync<int>(async ct =>
            {
                attemptCount++;
                throw new RequestFailedException(429, "Rate limit");
            });
        });

        // OpenAI should retry more times (initial + 5 retries = 6 total)
        Assert.Equal(6, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_WithoutGeneric_Succeeds()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline(_mockLogger.Object);
        int attemptCount = 0;

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new RequestFailedException(503, "Service Unavailable");
            }
            await Task.CompletedTask;
        });

        // Assert
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateOpenAIRetryPipeline_WithoutGeneric_Succeeds()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateOpenAIRetryPipeline(_mockLogger.Object);
        int attemptCount = 0;

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new RequestFailedException(429, "Rate limit");
            }
            await Task.CompletedTask;
        });

        // Assert
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_RetriesOnTimeout()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<string>(_mockLogger.Object);
        int attemptCount = 0;

        // Act
        var result = await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new TimeoutException("Operation timed out");
            }
            return await Task.FromResult("completed");
        });

        // Assert
        Assert.Equal("completed", result);
        Assert.Equal(2, attemptCount);
    }

    [Fact]
    public async Task CreateStorageRetryPipeline_LogsRetryAttempts()
    {
        // Arrange
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object);
        int attemptCount = 0;

        // Act
        await pipeline.ExecuteAsync(async ct =>
        {
            attemptCount++;
            if (attemptCount < 2)
            {
                throw new RequestFailedException(503, "Service Unavailable");
            }
            return await Task.FromResult(1);
        });

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
        var pipeline = ResiliencePolicy.CreateStorageRetryPipeline<int>(_mockLogger.Object);
        var cts = new CancellationTokenSource();
        int attemptCount = 0;

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
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
