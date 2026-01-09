using ALAN.Agent.Services;
using ALAN.Shared.Models;
using ALAN.Shared.Services;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using System.IO;

namespace ALAN.Agent.Tests.Services;

public class MemoryConsolidationServiceTests
{
    private static PromptService CreatePromptService()
    {
        var promptsDir = Path.Combine(Path.GetTempPath(), $"alan-tests-prompts-{Guid.NewGuid()}");
        Directory.CreateDirectory(promptsDir);
        
        // Create the memory-consolidation template file that's actually used by the service
        var templatePath = Path.Combine(promptsDir, "memory-consolidation.hbs");
        File.WriteAllText(templatePath, @"You are analyzing {{memoryCount}} memory entries to extract key learnings and patterns.

Memories to analyze:
{{memoriesJson}}

Please analyze these memories and provide:
1. A topic that these memories relate to
2. A summary of the key learning or pattern
3. Specific insights in JSON format
4. A confidence score (0.0 to 1.0)

Respond with ONLY a JSON object in this format:
{
  ""topic"": ""the main topic"",
  ""summary"": ""concise summary of the learning"",
  ""insights"": {
    ""pattern"": ""description of any pattern found"",
    ""actionable"": ""actionable insight if any"",
    ""related_concepts"": [""concept1"", ""concept2""]
  },
  ""confidence"": 0.8
}");
        
        return new PromptService(Mock.Of<ILogger<PromptService>>(), promptsDir);
    }
    [Fact]
    public async Task ExtractLearningsAsync_UsesShortTermMemoryForRecentMemories()
    {
        // Arrange
        var mockLongTerm = new Mock<ILongTermMemoryService>(MockBehavior.Strict);
        var mockShortTerm = new Mock<IShortTermMemoryService>();
        mockLongTerm
            .Setup(m => m.GetRecentMemoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var logger = Mock.Of<ILogger<MemoryConsolidationService>>();
        var stateManager = new StateManager(mockShortTerm.Object, mockLongTerm.Object, Mock.Of<ILogger<StateManager>>());

        var memories = new List<MemoryEntry>
        {
            new() { Type = MemoryType.Observation, Timestamp = DateTime.UtcNow.AddMinutes(-10) },
            new() { Type = MemoryType.Observation, Timestamp = DateTime.UtcNow.AddMinutes(-5) }
        };

        mockLongTerm
            .Setup(m => m.GetRecentMemoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);

        var promptService = CreatePromptService();
        var svc = new MemoryConsolidationService(mockLongTerm.Object, mockShortTerm.Object, stateManager, agent: null!, logger, promptService);

        // Act
        var learnings = await svc.ExtractLearningsAsync(DateTime.UtcNow.AddHours(-1));

        // Assert
        Assert.Empty(learnings); // fewer than 3 memories in any group -> no consolidation
        mockShortTerm.Verify(m => m.GetRecentMemoriesAsync(500, It.IsAny<CancellationToken>()), Times.Never);
        mockLongTerm.Verify(m => m.GetRecentMemoriesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsolidateShortTermMemoryAsync_StoresConsolidatedMemoriesInShortTerm()
    {
        // Arrange
        var mockLongTerm = new Mock<ILongTermMemoryService>();
        mockLongTerm
            .Setup(m => m.StoreMemoryAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        var mockShortTerm = new Mock<IShortTermMemoryService>();
        var logger = Mock.Of<ILogger<MemoryConsolidationService>>();
        var stateManager = new StateManager(mockShortTerm.Object, mockLongTerm.Object, Mock.Of<ILogger<StateManager>>());

        var thought = new AgentThought
        {
            Id = Guid.NewGuid().ToString(),
            Type = ThoughtType.Reflection,
            Content = "A reflective thought",
            Timestamp = DateTime.UtcNow
        };
        stateManager.AddThought(thought);

        var promptService = CreatePromptService();
        var svc = new MemoryConsolidationService(mockLongTerm.Object, mockShortTerm.Object, stateManager, agent: null!, logger, promptService);

        // Act
        await svc.ConsolidateShortTermMemoryAsync();

        // Assert
        mockLongTerm.Verify(m => m.StoreMemoryAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        mockShortTerm.Verify(
            m => m.SetAsync(
                It.Is<string>(k => k.StartsWith("memory:")),
                It.IsAny<MemoryEntry>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ConsolidateShortTermMemoryAsync_UsesPersistedThoughtsAndActionsWhenAvailable()
    {
        // Arrange
        var mockLongTerm = new Mock<ILongTermMemoryService>();
        mockLongTerm
            .Setup(m => m.StoreMemoryAsync(It.IsAny<MemoryEntry>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Guid.NewGuid().ToString());

        var mockShortTerm = new Mock<IShortTermMemoryService>();
        var logger = Mock.Of<ILogger<MemoryConsolidationService>>();
        var stateManager = new StateManager(mockShortTerm.Object, mockLongTerm.Object, Mock.Of<ILogger<StateManager>>());

        var thought = new AgentThought
        {
            Id = "t1",
            Type = ThoughtType.Reflection,
            Content = "Persisted reflective thought",
            Timestamp = DateTime.UtcNow
        };

        mockShortTerm
            .Setup(m => m.GetKeysAsync("thought:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "thought:t1" });
        mockShortTerm
            .Setup(m => m.GetAsync<AgentThought>("thought:t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(thought);

        mockShortTerm
            .Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var promptService = CreatePromptService();
        var svc = new MemoryConsolidationService(mockLongTerm.Object, mockShortTerm.Object, stateManager, agent: null!, logger, promptService);

        // Act
        await svc.ConsolidateShortTermMemoryAsync();

        // Assert
        mockShortTerm.Verify(m => m.GetKeysAsync("thought:*", It.IsAny<CancellationToken>()), Times.Once);
        mockShortTerm.Verify(m => m.GetAsync<AgentThought>("thought:t1", It.IsAny<CancellationToken>()), Times.Once);
        mockShortTerm.Verify(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void ParseLearningResponse_IsCaseInsensitive()
    {
        // Arrange
        var mockLongTerm = new Mock<ILongTermMemoryService>();
        var mockShortTerm = new Mock<IShortTermMemoryService>();
        var logger = Mock.Of<ILogger<MemoryConsolidationService>>();
        var stateManager = new StateManager(mockShortTerm.Object, mockLongTerm.Object, Mock.Of<ILogger<StateManager>>());
        var promptService = CreatePromptService();
        var svc = new MemoryConsolidationService(mockLongTerm.Object, mockShortTerm.Object, stateManager, agent: null!, logger, promptService);

        var jsonResponse = @"{ ""TOPIC"": ""TestTopic"", ""SUMMARY"": ""Some summary"", ""INSIGHTS"": { ""pattern"": ""X"" }, ""CONFIDENCE"": 0.9 }";
        var sourceMemories = new List<MemoryEntry> { new() { Id = "m1" } };

        var method = typeof(MemoryConsolidationService)
            .GetMethod("ParseLearningResponse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(method);

        // Act
        var learning = (ConsolidatedLearning?)method!.Invoke(svc, new object[] { jsonResponse, sourceMemories });

        // Assert
        Assert.NotNull(learning);
        Assert.Equal("TestTopic", learning!.Topic);
        Assert.Equal("Some summary", learning.Summary);
        Assert.Equal(0.9, learning.Confidence, 3);
        Assert.Contains("m1", learning.SourceMemoryIds);
    }
}
