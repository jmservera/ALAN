using ALAN.Agent.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace ALAN.Agent.Tests.Services;

public class UsageTrackerTests
{
    private readonly Mock<ILogger<UsageTracker>> _mockLogger;

    public UsageTrackerTests()
    {
        _mockLogger = new Mock<ILogger<UsageTracker>>();
    }

    [Fact]
    public void UsageTracker_Constructor_SetsDefaultLimits()
    {
        // Arrange & Act
        var tracker = new UsageTracker(_mockLogger.Object);
        var stats = tracker.GetTodayStats();

        // Assert
        Assert.Equal(4000, stats.MaxLoops);
        Assert.Equal(8_000_000, stats.MaxTokens);
        Assert.Equal(0, stats.LoopCount);
        Assert.Equal(0, stats.EstimatedTokens);
    }

    [Fact]
    public void UsageTracker_Constructor_AcceptsCustomLimits()
    {
        // Arrange & Act
        var tracker = new UsageTracker(_mockLogger.Object, maxLoopsPerDay: 1000, maxTokensPerDay: 2_000_000);
        var stats = tracker.GetTodayStats();

        // Assert
        Assert.Equal(1000, stats.MaxLoops);
        Assert.Equal(2_000_000, stats.MaxTokens);
    }

    [Fact]
    public void CanExecuteLoop_WithinLimits_ReturnsTrue()
    {
        // Arrange
        var tracker = new UsageTracker(_mockLogger.Object, maxLoopsPerDay: 10, maxTokensPerDay: 10000);

        // Act
        var canExecute = tracker.CanExecuteLoop(out var reason);

        // Assert
        Assert.True(canExecute);
        Assert.Null(reason);
    }

    [Fact]
    public void CanExecuteLoop_AfterReachingLoopLimit_ReturnsFalse()
    {
        // Arrange
        var tracker = new UsageTracker(_mockLogger.Object, maxLoopsPerDay: 2, maxTokensPerDay: 10000);
        tracker.RecordLoop(1000);
        tracker.RecordLoop(1000);

        // Act
        var canExecute = tracker.CanExecuteLoop(out var reason);

        // Assert
        Assert.False(canExecute);
        Assert.NotNull(reason);
        Assert.Contains("loop limit", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CanExecuteLoop_AfterReachingTokenLimit_ReturnsFalse()
    {
        // Arrange
        var tracker = new UsageTracker(_mockLogger.Object, maxLoopsPerDay: 100, maxTokensPerDay: 5000);
        tracker.RecordLoop(3000);
        tracker.RecordLoop(3000);

        // Act
        var canExecute = tracker.CanExecuteLoop(out var reason);

        // Assert
        Assert.False(canExecute);
        Assert.NotNull(reason);
        Assert.Contains("token limit", reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RecordLoop_IncrementsLoopCount()
    {
        // Arrange
        var tracker = new UsageTracker(_mockLogger.Object);

        // Act
        tracker.RecordLoop(2000);
        tracker.RecordLoop(2000);
        var stats = tracker.GetTodayStats();

        // Assert
        Assert.Equal(2, stats.LoopCount);
    }

    [Fact]
    public void RecordLoop_AccumulatesTokens()
    {
        // Arrange
        var tracker = new UsageTracker(_mockLogger.Object);

        // Act
        tracker.RecordLoop(1500);
        tracker.RecordLoop(2500);
        var stats = tracker.GetTodayStats();

        // Assert
        Assert.Equal(4000, stats.EstimatedTokens);
    }

    [Fact]
    public void GetTodayStats_CalculatesPercentages()
    {
        // Arrange
        var tracker = new UsageTracker(_mockLogger.Object, maxLoopsPerDay: 100, maxTokensPerDay: 10000);
        tracker.RecordLoop(2500);
        tracker.RecordLoop(2500);

        // Act
        var stats = tracker.GetTodayStats();

        // Assert
        Assert.Equal(2, stats.LoopPercentage);
        Assert.Equal(50, stats.TokenPercentage);
    }

    [Fact]
    public void GetTodayStats_CalculatesEstimatedCost()
    {
        // Arrange
        var tracker = new UsageTracker(_mockLogger.Object);
        tracker.RecordLoop(1_000_000);

        // Act
        var stats = tracker.GetTodayStats();

        // Assert
        Assert.Equal(0.15, stats.EstimatedCost, precision: 4);
    }

    [Fact]
    public void ResetToday_ClearsUsageStats()
    {
        // Arrange
        var tracker = new UsageTracker(_mockLogger.Object);
        tracker.RecordLoop(2000);
        tracker.RecordLoop(2000);

        // Act
        tracker.ResetToday();
        var stats = tracker.GetTodayStats();

        // Assert
        Assert.Equal(0, stats.LoopCount);
        Assert.Equal(0, stats.EstimatedTokens);
    }

    [Fact]
    public void UsageStats_ToString_ReturnsFormattedString()
    {
        // Arrange
        var stats = new UsageStats
        {
            Date = new DateTime(2024, 1, 1),
            LoopCount = 50,
            MaxLoops = 100,
            EstimatedTokens = 100_000,
            MaxTokens = 1_000_000,
            EstimatedCost = 0.015,
            LoopPercentage = 50.0,
            TokenPercentage = 10.0
        };

        // Act
        var result = stats.ToString();

        // Assert
        Assert.Contains("2024-01-01", result);
        Assert.Contains("50/100", result);
        Assert.Contains("50.0%", result);
        Assert.Contains("10.0%", result);
        Assert.Contains("$0.0150", result);
    }
}
