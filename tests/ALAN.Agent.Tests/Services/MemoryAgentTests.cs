using ALAN.Agent.Services;
using ALAN.Shared.Models;
using ALAN.Shared.Services;
using ALAN.Shared.Services.Memory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace ALAN.Agent.Tests.Services;

public class MemoryAgentTests
{
    private readonly Mock<AIAgent> _mockAIAgent;
    private readonly Mock<IVectorMemoryService> _mockVectorMemory;
    private readonly Mock<ILongTermMemoryService> _mockLongTermMemory;
    private readonly Mock<ILogger<MemoryAgent>> _mockLogger;
    private readonly Mock<IPromptService> _mockPromptService;

    public MemoryAgentTests()
    {
        _mockAIAgent = new Mock<AIAgent>();
        _mockVectorMemory = new Mock<IVectorMemoryService>();
        _mockLongTermMemory = new Mock<ILongTermMemoryService>();
        _mockLogger = new Mock<ILogger<MemoryAgent>>();
        _mockPromptService = new Mock<IPromptService>();
    }

    [Fact]
    public async Task SearchRelevantMemoriesAsync_ShouldReturnResults()
    {
        // Arrange
        var agent = CreateMemoryAgent();
        var taskDescription = "Implement authentication";
        var expectedResults = new List<MemorySearchResult>
        {
            new MemorySearchResult
            {
                Memory = new MemoryEntry
                {
                    Id = "mem-1",
                    Type = MemoryType.Learning,
                    Summary = "JWT authentication implementation",
                    Importance = 0.9
                },
                Score = 0.95
            }
        };

        _mockVectorMemory
            .Setup(v => v.SearchMemoriesAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<MemorySearchFilters?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResults);

        // Act
        var results = await agent.SearchRelevantMemoriesAsync(taskDescription);

        // Assert
        Assert.Single(results);
        Assert.Equal("mem-1", results[0].Memory.Id);
        Assert.Equal(0.95, results[0].Score);
        _mockVectorMemory.Verify(v => v.SearchMemoriesAsync(
            taskDescription,
            10,
            0.7,
            null,
            "long-term",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task FindSimilarCompletedTaskAsync_ShouldReturnMatchWhenFound()
    {
        // Arrange
        var agent = CreateMemoryAgent();
        var taskDescription = "Add user registration";
        var expectedMatch = new MemorySearchResult
        {
            Memory = new MemoryEntry
            {
                Id = "task-1",
                Type = MemoryType.Success,
                Summary = "User registration completed",
                Importance = 0.8
            },
            Score = 0.90
        };

        _mockVectorMemory
            .Setup(v => v.SearchMemoriesAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<MemorySearchFilters?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([expectedMatch]);

        // Act
        var result = await agent.FindSimilarCompletedTaskAsync(taskDescription);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("task-1", result.Memory.Id);
        Assert.Equal(0.90, result.Score);
    }

    [Fact]
    public async Task FindSimilarCompletedTaskAsync_ShouldReturnNullWhenNoMatch()
    {
        // Arrange
        var agent = CreateMemoryAgent();
        var taskDescription = "Some unique task";

        _mockVectorMemory
            .Setup(v => v.SearchMemoriesAsync(
                It.IsAny<string>(),
                It.IsAny<int>(),
                It.IsAny<double>(),
                It.IsAny<MemorySearchFilters?>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await agent.FindSimilarCompletedTaskAsync(taskDescription);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task EvaluateSearchQualityAsync_ShouldCalculateMetrics()
    {
        // Arrange
        var agent = CreateMemoryAgent();
        var query = "test query";
        var results = new List<MemorySearchResult>
        {
            new() { Memory = new MemoryEntry(), Score = 0.95 },
            new() { Memory = new MemoryEntry(), Score = 0.85 },
            new() { Memory = new MemoryEntry(), Score = 0.75 }
        };

        // Act
        var evaluation = await agent.EvaluateSearchQualityAsync(query, results);

        // Assert
        Assert.Equal(query, evaluation.Query);
        Assert.Equal(3, evaluation.ResultCount);
        Assert.Equal(0.85, evaluation.AverageScore, precision: 2);
        Assert.Equal(0.75, evaluation.MinScore);
        Assert.Equal(0.95, evaluation.MaxScore);
        // Precision: 2 results with score > 0.8 out of 3 total = 0.666...
        Assert.Equal(0.666, evaluation.Precision, precision: 2);
    }

    [Fact]
    public async Task MigrateMemoryToVectorSearchAsync_ShouldStoreMemory()
    {
        // Arrange
        var agent = CreateMemoryAgent();
        var memory = new MemoryEntry
        {
            Id = "test-memory",
            Type = MemoryType.Learning,
            Content = "Test content"
        };

        _mockVectorMemory
            .Setup(v => v.StoreMemoryAsync(It.IsAny<MemoryEntry>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("test-memory");

        // Act
        var result = await agent.MigrateMemoryToVectorSearchAsync(memory);

        // Assert
        Assert.True(result);
        _mockVectorMemory.Verify(v => v.StoreMemoryAsync(
            It.Is<MemoryEntry>(m => m.Id == "test-memory"),
            "long-term",
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task BatchMigrateMemoriesAsync_ShouldMigrateMultipleMemories()
    {
        // Arrange
        var agent = CreateMemoryAgent();
        var memories = new List<MemoryEntry>
        {
            new() { Id = "mem-1", Content = "Content 1" },
            new() { Id = "mem-2", Content = "Content 2" },
            new() { Id = "mem-3", Content = "Content 3" }
        };

        _mockLongTermMemory
            .Setup(l => l.GetRecentMemoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);

        _mockVectorMemory
            .Setup(v => v.StoreMemoryAsync(It.IsAny<MemoryEntry>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((MemoryEntry m, string c, CancellationToken ct) => m.Id);

        // Act
        var count = await agent.BatchMigrateMemoriesAsync(batchSize: 100);

        // Assert
        Assert.Equal(3, count);
        _mockVectorMemory.Verify(v => v.StoreMemoryAsync(
            It.IsAny<MemoryEntry>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public void MemorySearchEvaluation_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var evaluation = new MemorySearchEvaluation
        {
            Query = "test query",
            ResultCount = 5,
            AverageScore = 0.85,
            MinScore = 0.70,
            MaxScore = 0.95,
            Precision = 0.8,
            Suggestions = ["Increase threshold", "Add more filters"]
        };

        // Assert
        Assert.Equal("test query", evaluation.Query);
        Assert.Equal(5, evaluation.ResultCount);
        Assert.Equal(0.85, evaluation.AverageScore);
        Assert.Equal(0.70, evaluation.MinScore);
        Assert.Equal(0.95, evaluation.MaxScore);
        Assert.Equal(0.8, evaluation.Precision);
        Assert.Equal(2, evaluation.Suggestions.Count);
    }

    private MemoryAgent CreateMemoryAgent()
    {
        return new MemoryAgent(
            _mockAIAgent.Object,
            _mockVectorMemory.Object,
            _mockLongTermMemory.Object,
            _mockLogger.Object,
            _mockPromptService.Object);
    }
}
