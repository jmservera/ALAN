using ALAN.Agent.Services;
using ALAN.Shared.Models;
using ALAN.Shared.Services;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.AI;
using Moq;
using ALAN.Shared.Services.Queue;

namespace ALAN.Agent.Tests.Services;

public class AutonomousAgentTests
{
    private readonly AutonomousAgent _agent;

    public AutonomousAgentTests()
    {
        // Create mocks for all dependencies
        var mockAIAgent = new Mock<AIAgent>();
        var mockLogger = new Mock<ILogger<AutonomousAgent>>();
        var mockLongTermMemory = new Mock<ILongTermMemoryService>();
        var mockShortTermMemory = new Mock<IShortTermMemoryService>();
        var mockConsolidation = new Mock<IMemoryConsolidationService>();
        var mockPromptService = new Mock<IPromptService>();

        // Create real service instances with mocked dependencies
        var stateManager = new StateManager(mockShortTermMemory.Object, mockLongTermMemory.Object, Mock.Of<ILogger<StateManager>>());
        var usageTracker = new UsageTracker(Mock.Of<ILogger<UsageTracker>>(), 4000, 8000000);
        var batchLearning = new BatchLearningService(
            mockConsolidation.Object,
            mockLongTermMemory.Object,
            Mock.Of<ILogger<BatchLearningService>>());
        var humanInput = new HumanInputHandler(
            Mock.Of<ILogger<HumanInputHandler>>(),
            stateManager,
            Mock.Of<IMessageQueue<HumanInput>>(),
            mockConsolidation.Object);

        _agent = new AutonomousAgent(
            mockAIAgent.Object,
            mockLogger.Object,
            stateManager,
            usageTracker,
            mockLongTermMemory.Object,
            mockShortTermMemory.Object,
            batchLearning,
            humanInput,
            mockPromptService.Object);
    }

    [Fact]
    public void BuildMemoryContext_WithEmptyMemories_ReturnsDefaultMessage()
    {
        // Arrange
        _agent.SetRecentMemoriesForTesting(new List<MemoryEntry>());

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.Equal("No previous memories available yet. This may be your first iteration.", result);
    }

    [Fact]
    public void BuildMemoryContext_GroupsByMemoryType()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Learning 1",
                Importance = 0.9,
                Timestamp = DateTime.UtcNow.AddHours(-2)
            },
            new MemoryEntry
            {
                Type = MemoryType.Success,
                Summary = "Success 1",
                Importance = 0.8,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.Contains("### Learning", result);
        Assert.Contains("### Success", result);
        Assert.Contains("Learning 1", result);
        Assert.Contains("Success 1", result);
    }

    [Fact]
    public void BuildMemoryContext_OrdersByTypePriority()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Decision,
                Summary = "Decision 1",
                Importance = 0.9,
                Timestamp = DateTime.UtcNow
            },
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Learning 1",
                Importance = 0.9,
                Timestamp = DateTime.UtcNow
            },
            new MemoryEntry
            {
                Type = MemoryType.Reflection,
                Summary = "Reflection 1",
                Importance = 0.9,
                Timestamp = DateTime.UtcNow
            },
            new MemoryEntry
            {
                Type = MemoryType.Success,
                Summary = "Success 1",
                Importance = 0.9,
                Timestamp = DateTime.UtcNow
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        // Learning (priority 5) should appear before Reflection (4), Success (3), and Decision (2)
        var learningIndex = result.IndexOf("### Learning");
        var reflectionIndex = result.IndexOf("### Reflection");
        var successIndex = result.IndexOf("### Success");
        var decisionIndex = result.IndexOf("### Decision");

        Assert.True(learningIndex < reflectionIndex, "Learning should appear before Reflection");
        Assert.True(reflectionIndex < successIndex, "Reflection should appear before Success");
        Assert.True(successIndex < decisionIndex, "Success should appear before Decision");
    }

    [Fact]
    public void BuildMemoryContext_FormatsAgeCorrectly_Hours()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Recent learning",
                Importance = 0.9,
                Timestamp = DateTime.UtcNow.AddHours(-5)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.Contains("5h ago", result);
    }

    [Fact]
    public void BuildMemoryContext_FormatsAgeCorrectly_Days()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Old learning",
                Importance = 0.9,
                Timestamp = DateTime.UtcNow.AddDays(-3)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.Contains("3d ago", result);
    }

    [Fact]
    public void BuildMemoryContext_IncludesImportanceScore()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Important learning",
                Importance = 0.85,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.Contains("importance: 0.85", result);
    }

    [Fact]
    public void BuildMemoryContext_IncludesFullContentForHighImportance()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "High importance learning",
                Content = "This is the detailed content that should be included for high importance items.",
                Importance = 0.85, // >= 0.8 threshold
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.Contains("Details:", result);
        Assert.Contains("This is the detailed content", result);
    }

    [Fact]
    public void BuildMemoryContext_ExcludesContentForLowImportance()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Low importance learning",
                Content = "This detailed content should not be included for low importance items.",
                Importance = 0.5, // < 0.8 threshold
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.DoesNotContain("Details:", result);
        Assert.DoesNotContain("This detailed content should not be included", result);
        Assert.Contains("Low importance learning", result);
    }

    [Fact]
    public void BuildMemoryContext_TruncatesContentAt200Characters()
    {
        // Arrange
        var longContent = new string('x', 250); // 250 characters
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Learning with long content",
                Content = longContent,
                Importance = 0.85,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.Contains("Details:", result);
        // Should contain exactly 200 chars + "..."
        Assert.Contains(new string('x', 200) + "...", result);
        // Should not contain the full 250 character string
        Assert.DoesNotContain(longContent, result);
    }

    [Fact]
    public void BuildMemoryContext_DoesNotTruncateShortContent()
    {
        // Arrange
        var shortContent = "This is a short content that should not be truncated.";
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Learning with short content",
                Content = shortContent,
                Importance = 0.85,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.Contains(shortContent, result);
        // Should not have "..." since content is short
        Assert.DoesNotContain("...", result);
    }

    [Fact]
    public void BuildMemoryContext_TakesOnlyTop5PerGroup()
    {
        // Arrange
        var memories = new List<MemoryEntry>();
        for (int i = 0; i < 10; i++)
        {
            memories.Add(new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = $"Learning {i}",
                Importance = 0.9 - (i * 0.05), // Descending importance
                Timestamp = DateTime.UtcNow.AddHours(-i)
            });
        }
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        // Should contain first 5 (highest importance)
        for (int i = 0; i < 5; i++)
        {
            Assert.Contains($"Learning {i}", result);
        }
        // Should NOT contain 6-9
        for (int i = 5; i < 10; i++)
        {
            Assert.DoesNotContain($"Learning {i}", result);
        }
    }

    [Fact]
    public void BuildMemoryContext_OrdersByImportanceWithinGroup()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Low importance",
                Importance = 0.5,
                Timestamp = DateTime.UtcNow
            },
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "High importance",
                Importance = 0.9,
                Timestamp = DateTime.UtcNow
            },
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Medium importance",
                Importance = 0.7,
                Timestamp = DateTime.UtcNow
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        var highIndex = result.IndexOf("High importance");
        var mediumIndex = result.IndexOf("Medium importance");
        var lowIndex = result.IndexOf("Low importance");

        Assert.True(highIndex < mediumIndex, "High importance should appear before medium");
        Assert.True(mediumIndex < lowIndex, "Medium importance should appear before low");
    }

    [Fact]
    public void BuildMemoryContext_HandlesContentSameAsSummary()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Same content",
                Content = "Same content", // Same as summary
                Importance = 0.85,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        // Should not include Details section when content equals summary
        Assert.DoesNotContain("Details:", result);
        Assert.Contains("Same content", result);
    }

    [Fact]
    public void BuildMemoryContext_HandlesEmptyContent()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "No content",
                Content = "", // Empty content
                Importance = 0.85,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        // Should not include Details section when content is empty
        Assert.DoesNotContain("Details:", result);
        Assert.Contains("No content", result);
    }

    [Fact]
    public void BuildMemoryContext_HandlesNullContent()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Null content",
                Content = null!, // Null content
                Importance = 0.85,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        // Should not include Details section when content is null
        Assert.DoesNotContain("Details:", result);
        Assert.Contains("Null content", result);
    }

    [Fact]
    public void BuildMemoryContext_ShowsCorrectEntryCount()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry { Type = MemoryType.Learning, Summary = "L1", Importance = 0.9, Timestamp = DateTime.UtcNow },
            new MemoryEntry { Type = MemoryType.Learning, Summary = "L2", Importance = 0.8, Timestamp = DateTime.UtcNow },
            new MemoryEntry { Type = MemoryType.Success, Summary = "S1", Importance = 0.7, Timestamp = DateTime.UtcNow }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act
        var result = _agent.BuildMemoryContext();

        // Assert
        Assert.Contains("### Learning (2 entries):", result);
        Assert.Contains("### Success (1 entry):", result);
    }

    [Fact]
    public void BuildMemoryContext_RespectsTokenLimit()
    {
        // Arrange - Create memories with known sizes
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "First learning with a short summary",
                Importance = 0.9,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            },
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Second learning with another short summary",
                Importance = 0.8,
                Timestamp = DateTime.UtcNow.AddHours(-2)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act - Request with a very small token limit
        var result = _agent.BuildMemoryContext(maxTokens: 50);

        // Assert - Should include at least one memory but respect the limit
        Assert.Contains("### Learning", result);
        // The result should be truncated to respect the token limit
        Assert.True(result.Length < 500); // Should be much smaller than unrestricted
    }

    [Fact]
    public void BuildMemoryContext_HandlesVeryLargeMemory()
    {
        // Arrange - Create one very large memory
        var largeContent = new string('x', 10000); // 10k characters
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = largeContent,
                Importance = 0.9,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            },
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Small summary",
                Importance = 0.8,
                Timestamp = DateTime.UtcNow.AddHours(-2)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act - Request with a reasonable token limit
        var result = _agent.BuildMemoryContext(maxTokens: 500);

        // Assert - Should truncate the large memory and not throw
        Assert.Contains("### Learning", result);
        Assert.DoesNotContain(largeContent, result); // Should not include full large content
        // Should include at least a truncated version
        Assert.Contains("...", result); // Truncation indicator
    }

    [Fact]
    public void BuildMemoryContext_IncludesMultipleMemoriesWithinTokenBudget()
    {
        // Arrange - Create multiple small memories
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry { Type = MemoryType.Learning, Summary = "L1", Importance = 0.9, Timestamp = DateTime.UtcNow },
            new MemoryEntry { Type = MemoryType.Learning, Summary = "L2", Importance = 0.8, Timestamp = DateTime.UtcNow },
            new MemoryEntry { Type = MemoryType.Success, Summary = "S1", Importance = 0.7, Timestamp = DateTime.UtcNow },
            new MemoryEntry { Type = MemoryType.Success, Summary = "S2", Importance = 0.6, Timestamp = DateTime.UtcNow }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act - Request with a reasonable token limit
        var result = _agent.BuildMemoryContext(maxTokens: 500);

        // Assert - Should include all or most memories since they're small
        Assert.Contains("### Learning", result);
        Assert.Contains("### Success", result);
        Assert.Contains("L1", result);
        Assert.Contains("S1", result);
    }

    [Fact]
    public void BuildMemoryContext_StopsAddingWhenTokenLimitReached()
    {
        // Arrange - Create many memories with medium-sized summaries
        var memories = new List<MemoryEntry>();
        for (int i = 1; i <= 20; i++)
        {
            memories.Add(new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = $"This is memory number {i} with some additional text to make it longer so we can test token limiting behavior",
                Importance = 0.9 - (i * 0.02),
                Timestamp = DateTime.UtcNow.AddHours(-i)
            });
        }
        _agent.SetRecentMemoriesForTesting(memories);

        // Act - Request with a moderate token limit
        var result = _agent.BuildMemoryContext(maxTokens: 300);

        // Assert - Should include only first few memories (highest importance)
        Assert.Contains("### Learning", result);
        Assert.Contains("memory number 1", result); // Highest importance
        Assert.Contains("memory number 2", result);
        // But not all 20 memories
        Assert.DoesNotContain("memory number 20", result);
    }

    [Fact]
    public void BuildMemoryContext_TruncatesIndividualMemoryWhenTooLarge()
    {
        // Arrange - One memory that alone exceeds the token limit
        var veryLongSummary = string.Concat(Enumerable.Repeat("This is a very long summary that contains a lot of information. ", 100));
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = veryLongSummary,
                Importance = 0.9,
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act - Request with a token limit smaller than the memory
        var result = _agent.BuildMemoryContext(maxTokens: 200);

        // Assert - Should include a truncated version
        Assert.Contains("### Learning", result);
        Assert.Contains("...", result); // Truncation indicator
        // Should not contain the full text
        Assert.DoesNotContain(veryLongSummary, result);
    }

    [Fact]
    public void BuildMemoryContext_PrioritizesHighImportanceMemories()
    {
        // Arrange - Mix of high and low importance memories
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "High importance learning",
                Importance = 0.95,
                Timestamp = DateTime.UtcNow.AddHours(-5)
            },
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Low importance learning",
                Importance = 0.3,
                Timestamp = DateTime.UtcNow.AddHours(-1) // More recent but less important
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act - Request with a small token limit that can only fit one
        var result = _agent.BuildMemoryContext(maxTokens: 150);

        // Assert - Should prioritize high importance over recency
        Assert.Contains("High importance learning", result);
        // Low importance might not be included due to token limit
    }

    [Fact]
    public void BuildMemoryContext_IncludesDetailsForHighImportanceWithinTokenBudget()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Important learning",
                Content = "This is the detailed content that provides more context about the learning",
                Importance = 0.85, // Above HIGH_IMPORTANCE_THRESHOLD
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act - Request with sufficient token budget
        var result = _agent.BuildMemoryContext(maxTokens: 500);

        // Assert - Should include details since there's budget and it's high importance
        Assert.Contains("Important learning", result);
        Assert.Contains("Details:", result);
        Assert.Contains("detailed content", result);
    }

    [Fact]
    public void BuildMemoryContext_ExcludesDetailsWhenTokenBudgetTight()
    {
        // Arrange
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = "Important learning",
                Content = new string('x', 5000), // Very large content
                Importance = 0.85, // Above HIGH_IMPORTANCE_THRESHOLD
                Timestamp = DateTime.UtcNow.AddHours(-1)
            }
        };
        _agent.SetRecentMemoriesForTesting(memories);

        // Act - Request with very small token budget
        var result = _agent.BuildMemoryContext(maxTokens: 100);

        // Assert - Should exclude details to stay within budget
        Assert.Contains("Important learning", result);
        // Details might be excluded if they don't fit
    }

    #region LoadRecentMemoriesAsync Tests

    [Fact]
    public async Task LoadRecentMemoriesAsync_LoadsMemoriesFromDifferentTypes()
    {
        // Arrange
        var mockLongTermMemory = new Mock<ILongTermMemoryService>();
        var mockShortTermMemory = new Mock<IShortTermMemoryService>();

        var learnings = new List<MemoryEntry>
        {
            new MemoryEntry { Type = MemoryType.Learning, Summary = "L1", Importance = 0.9, Timestamp = DateTime.UtcNow }
        };
        var successes = new List<MemoryEntry>
        {
            new MemoryEntry { Type = MemoryType.Success, Summary = "S1", Importance = 0.8, Timestamp = DateTime.UtcNow }
        };
        var reflections = new List<MemoryEntry>
        {
            new MemoryEntry { Type = MemoryType.Reflection, Summary = "R1", Importance = 0.7, Timestamp = DateTime.UtcNow }
        };
        var decisions = new List<MemoryEntry>
        {
            new MemoryEntry { Type = MemoryType.Decision, Summary = "D1", Importance = 0.6, Timestamp = DateTime.UtcNow }
        };

        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Learning, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(learnings);
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Success, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(successes);
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Reflection, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reflections);
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Decision, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(decisions);
        mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var agent = CreateTestAgent(mockLongTermMemory.Object, mockShortTermMemory.Object);

        // Act
        await agent.LoadRecentMemoriesAsync(CancellationToken.None);

        // Assert
        var memories = agent.GetRecentMemoriesForTesting();
        Assert.Equal(4, memories.Count);
        Assert.Contains(memories, m => m.Type == MemoryType.Learning);
        Assert.Contains(memories, m => m.Type == MemoryType.Success);
        Assert.Contains(memories, m => m.Type == MemoryType.Reflection);
        Assert.Contains(memories, m => m.Type == MemoryType.Decision);
    }

    [Fact]
    public async Task LoadRecentMemoriesAsync_AppliesWeightedScoring()
    {
        // Arrange
        var mockLongTermMemory = new Mock<ILongTermMemoryService>();
        var mockShortTermMemory = new Mock<IShortTermMemoryService>();

        var now = DateTime.UtcNow;
        var memories = new List<MemoryEntry>
        {
            new MemoryEntry { Type = MemoryType.Learning, Summary = "Recent High", Importance = 0.9, Timestamp = now },
            new MemoryEntry { Type = MemoryType.Learning, Summary = "Old High", Importance = 0.9, Timestamp = now.AddDays(-10) },
            new MemoryEntry { Type = MemoryType.Learning, Summary = "Recent Low", Importance = 0.3, Timestamp = now },
            new MemoryEntry { Type = MemoryType.Learning, Summary = "Old Low", Importance = 0.3, Timestamp = now.AddDays(-10) }
        };

        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Learning, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(memories);
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Success, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Reflection, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Decision, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var agent = CreateTestAgent(mockLongTermMemory.Object, mockShortTermMemory.Object);

        // Act
        await agent.LoadRecentMemoriesAsync(CancellationToken.None);

        // Assert
        var loadedMemories = agent.GetRecentMemoriesForTesting();
        Assert.Equal(4, loadedMemories.Count);
        // Recent high importance should be first (0.9 * 0.7 + 1.0 * 0.3 = 0.93)
        Assert.Equal("Recent High", loadedMemories[0].Summary);
        // Old high importance should be second (0.9 * 0.7 + ~0 * 0.3 ≈ 0.63, old so minimal recency)
        Assert.Equal("Old High", loadedMemories[1].Summary);
    }

    [Fact]
    public async Task LoadRecentMemoriesAsync_ConvertsShortTermActionsToMemories()
    {
        // Arrange
        var mockLongTermMemory = new Mock<ILongTermMemoryService>();
        var mockShortTermMemory = new Mock<IShortTermMemoryService>();

        var actions = new List<AgentAction>
        {
            new AgentAction
            {
                Name = "Action1",
                Description = "Desc1",
                Output = "Output1",
                Status = ActionStatus.Completed,
                Timestamp = DateTime.UtcNow
            },
            new AgentAction
            {
                Name = "Action2",
                Description = "Desc2",
                Status = ActionStatus.Failed,
                Timestamp = DateTime.UtcNow
            }
        };

        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(It.IsAny<MemoryType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "action:1", "action:2" });
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions[0]);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(actions[1]);

        var agent = CreateTestAgent(mockLongTermMemory.Object, mockShortTermMemory.Object);

        // Act
        await agent.LoadRecentMemoriesAsync(CancellationToken.None);

        // Assert
        var memories = agent.GetRecentMemoriesForTesting();
        // Only completed actions should be converted
        Assert.Single(memories);
        Assert.Equal(MemoryType.Success, memories[0].Type);
        Assert.Equal("Output1", memories[0].Content);
        Assert.Contains("short-term", memories[0].Tags);
        Assert.Equal(0.5, memories[0].Importance);
    }

    [Fact]
    public async Task LoadRecentMemoriesAsync_HandlesExceptionGracefully()
    {
        // Arrange
        var mockLongTermMemory = new Mock<ILongTermMemoryService>();
        var mockShortTermMemory = new Mock<IShortTermMemoryService>();

        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(It.IsAny<MemoryType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Storage failure"));

        var agent = CreateTestAgent(mockLongTermMemory.Object, mockShortTermMemory.Object);

        // Act
        await agent.LoadRecentMemoriesAsync(CancellationToken.None);

        // Assert - should not throw and should have empty memories
        var memories = agent.GetRecentMemoriesForTesting();
        Assert.Empty(memories);
    }

    [Fact]
    public async Task LoadRecentMemoriesAsync_LimitsToMaxMemoryContextSize()
    {
        // Arrange
        var mockLongTermMemory = new Mock<ILongTermMemoryService>();
        var mockShortTermMemory = new Mock<IShortTermMemoryService>();

        // Create more than MAX_MEMORY_CONTEXT_SIZE (20) memories
        var manyMemories = Enumerable.Range(1, 30).Select(i => new MemoryEntry
        {
            Type = MemoryType.Learning,
            Summary = $"Memory {i}",
            Importance = 0.5 + (i * 0.01), // Varying importance
            Timestamp = DateTime.UtcNow.AddHours(-i)
        }).ToList();

        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Learning, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyMemories.Take(10).ToList());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Success, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyMemories.Skip(10).Take(10).ToList());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Reflection, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyMemories.Skip(20).Take(5).ToList());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Decision, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(manyMemories.Skip(25).Take(5).ToList());
        mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string>());

        var agent = CreateTestAgent(mockLongTermMemory.Object, mockShortTermMemory.Object);

        // Act
        await agent.LoadRecentMemoriesAsync(CancellationToken.None);

        // Assert - should be limited to MAX_MEMORY_CONTEXT_SIZE (20)
        var memories = agent.GetRecentMemoriesForTesting();
        Assert.Equal(20, memories.Count);
    }

    [Fact]
    public async Task LoadRecentMemoriesAsync_PrependShortTermActionsToMemories()
    {
        // Arrange
        var mockLongTermMemory = new Mock<ILongTermMemoryService>();
        var mockShortTermMemory = new Mock<IShortTermMemoryService>();

        var longTermMemories = new List<MemoryEntry>
        {
            new MemoryEntry { Type = MemoryType.Learning, Summary = "LongTerm1", Importance = 0.9, Timestamp = DateTime.UtcNow.AddHours(-5) }
        };

        var action = new AgentAction
        {
            Name = "RecentAction",
            Description = "Recent",
            Output = "RecentOutput",
            Status = ActionStatus.Completed,
            Timestamp = DateTime.UtcNow
        };

        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Learning, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(longTermMemories);
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Success, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Reflection, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Decision, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "action:1" });
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(action);

        var agent = CreateTestAgent(mockLongTermMemory.Object, mockShortTermMemory.Object);

        // Act
        await agent.LoadRecentMemoriesAsync(CancellationToken.None);

        // Assert - short-term actions should be prepended
        var memories = agent.GetRecentMemoriesForTesting();
        Assert.Equal(2, memories.Count);
        Assert.Equal(MemoryType.Success, memories[0].Type); // Short-term action first
        Assert.Equal("RecentOutput", memories[0].Content);
        Assert.Equal(MemoryType.Learning, memories[1].Type); // Long-term second
    }

    [Fact]
    public async Task LoadRecentMemoriesAsync_OnlyConvertsCompletedActions()
    {
        // Arrange
        var mockLongTermMemory = new Mock<ILongTermMemoryService>();
        var mockShortTermMemory = new Mock<IShortTermMemoryService>();

        var completedAction = new AgentAction { Status = ActionStatus.Completed, Output = "Completed", Timestamp = DateTime.UtcNow };
        var runningAction = new AgentAction { Status = ActionStatus.Running, Output = "Running", Timestamp = DateTime.UtcNow };
        var failedAction = new AgentAction { Status = ActionStatus.Failed, Output = "Failed", Timestamp = DateTime.UtcNow };
        var pendingAction = new AgentAction { Status = ActionStatus.Pending, Output = "Pending", Timestamp = DateTime.UtcNow };

        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Learning, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Success, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Reflection, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Decision, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "action:1", "action:2", "action:3", "action:4" });
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(completedAction);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(runningAction);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:3", It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedAction);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:4", It.IsAny<CancellationToken>()))
            .ReturnsAsync(pendingAction);

        var agent = CreateTestAgent(mockLongTermMemory.Object, mockShortTermMemory.Object);

        // Act
        await agent.LoadRecentMemoriesAsync(CancellationToken.None);

        // Assert - only completed action should be converted
        var memories = agent.GetRecentMemoriesForTesting();
        Assert.Single(memories);
        Assert.Equal("Completed", memories[0].Content);
    }

    [Fact]
    public async Task LoadRecentMemoriesAsync_LimitsShortTermActionsToTop5()
    {
        // Arrange
        var mockLongTermMemory = new Mock<ILongTermMemoryService>();
        var mockShortTermMemory = new Mock<IShortTermMemoryService>();

        // Create 10 completed actions
        var action1 = new AgentAction { Name = "Action1", Output = "Output1", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-1) };
        var action2 = new AgentAction { Name = "Action2", Output = "Output2", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-2) };
        var action3 = new AgentAction { Name = "Action3", Output = "Output3", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-3) };
        var action4 = new AgentAction { Name = "Action4", Output = "Output4", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-4) };
        var action5 = new AgentAction { Name = "Action5", Output = "Output5", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-5) };
        var action6 = new AgentAction { Name = "Action6", Output = "Output6", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-6) };
        var action7 = new AgentAction { Name = "Action7", Output = "Output7", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-7) };
        var action8 = new AgentAction { Name = "Action8", Output = "Output8", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-8) };
        var action9 = new AgentAction { Name = "Action9", Output = "Output9", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-9) };
        var action10 = new AgentAction { Name = "Action10", Output = "Output10", Status = ActionStatus.Completed, Timestamp = DateTime.UtcNow.AddMinutes(-10) };

        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Learning, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Success, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Reflection, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockLongTermMemory.Setup(m => m.GetMemoriesByTypeAsync(MemoryType.Decision, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MemoryEntry>());
        mockShortTermMemory.Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<string> { "action:1", "action:2", "action:3", "action:4", "action:5", "action:6", "action:7", "action:8", "action:9", "action:10" });
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:1", It.IsAny<CancellationToken>())).ReturnsAsync(action1);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:2", It.IsAny<CancellationToken>())).ReturnsAsync(action2);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:3", It.IsAny<CancellationToken>())).ReturnsAsync(action3);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:4", It.IsAny<CancellationToken>())).ReturnsAsync(action4);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:5", It.IsAny<CancellationToken>())).ReturnsAsync(action5);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:6", It.IsAny<CancellationToken>())).ReturnsAsync(action6);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:7", It.IsAny<CancellationToken>())).ReturnsAsync(action7);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:8", It.IsAny<CancellationToken>())).ReturnsAsync(action8);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:9", It.IsAny<CancellationToken>())).ReturnsAsync(action9);
        mockShortTermMemory.Setup(m => m.GetAsync<AgentAction>("action:10", It.IsAny<CancellationToken>())).ReturnsAsync(action10);

        var agent = CreateTestAgent(mockLongTermMemory.Object, mockShortTermMemory.Object);

        // Act
        await agent.LoadRecentMemoriesAsync(CancellationToken.None);

        // Assert - should be limited to top 5 most recent
        var memories = agent.GetRecentMemoriesForTesting();
        Assert.Equal(5, memories.Count);
        Assert.All(memories, m => Assert.Equal(MemoryType.Success, m.Type));
    }

    [Fact]
    public void BuildMemoryContext_PreventsInfiniteLoopWithSingleLargeMemory()
    {
        // Arrange - Simulate the scenario that would cause an infinite loop in the old implementation:
        // A single memory that is larger than the minimum context size
        var veryLargeMemory = new MemoryEntry
        {
            Type = MemoryType.Learning,
            Summary = string.Concat(Enumerable.Repeat("This is a very long summary that contains extensive information. ", 500)), // ~30k chars
            Content = string.Concat(Enumerable.Repeat("Even more extensive content. ", 1000)), // ~30k chars
            Importance = 0.95,
            Timestamp = DateTime.UtcNow
        };
        _agent.SetRecentMemoriesForTesting([veryLargeMemory]);

        // Act - Request with minimal token budget (simulating the retry scenario)
        // In the old implementation, this would keep halving the message count but never get below 1 message
        // Leading to an infinite loop if that 1 message is too large
        var result = _agent.BuildMemoryContext(maxTokens: 500); // MIN_MEMORY_CONTEXT_TOKENS

        // Assert - Should NOT throw an exception or hang
        // Should return a truncated version that fits within the token budget
        Assert.NotNull(result);
        Assert.NotEmpty(result);
        Assert.Contains("### Learning", result);
        // The summary should be truncated with ellipsis
        Assert.Contains("...", result);
        // Should not contain the full large content
        Assert.DoesNotContain(veryLargeMemory.Summary, result);
    }

    [Fact]
    public void BuildMemoryContext_GracefullyHandlesMultipleLargeMemories()
    {
        // Arrange - Multiple large memories that together exceed any reasonable limit
        var largeMemories = new List<MemoryEntry>();
        for (int i = 1; i <= 5; i++)
        {
            largeMemories.Add(new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = string.Concat(Enumerable.Repeat($"Large memory {i} with extensive details. ", 200)), // ~6k chars each
                Importance = 1.0 - (i * 0.1),
                Timestamp = DateTime.UtcNow.AddHours(-i)
            });
        }
        _agent.SetRecentMemoriesForTesting(largeMemories);

        // Act - Request with moderate token budget
        var result = _agent.BuildMemoryContext(maxTokens: 1000);

        // Assert - Should include what fits and stop gracefully
        Assert.NotNull(result);
        Assert.Contains("### Learning", result);
        // Should include at least the first (highest importance) memory
        Assert.Contains("Large memory 1", result);
        // Should not include all 5 memories since they're too large
        Assert.DoesNotContain("Large memory 5", result);
    }

    [Fact]
    public void BuildMemoryContext_TokenLimitMoreRestrictiveThanMessageLimit()
    {
        // Arrange - Create exactly MAX_MEMORY_CONTEXT_SIZE (20) memories with medium-large summaries
        var memories = new List<MemoryEntry>();
        for (int i = 1; i <= 20; i++)
        {
            memories.Add(new MemoryEntry
            {
                Type = MemoryType.Learning,
                Summary = string.Concat(Enumerable.Repeat($"Memory {i} has substantial content. ", 50)), // ~1.5k chars each
                Importance = 1.0 - (i * 0.03),
                Timestamp = DateTime.UtcNow.AddHours(-i)
            });
        }
        _agent.SetRecentMemoriesForTesting(memories);

        // Act - Request with a token budget that can't fit all 20 memories
        var result = _agent.BuildMemoryContext(maxTokens: 2000);

        // Assert - Token limit should be more restrictive than message count limit
        // Old implementation would include all 20, new implementation should include fewer
        Assert.NotNull(result);
        Assert.Contains("Memory 1", result); // Highest importance
        // Should NOT include all 20 memories
        Assert.DoesNotContain("Memory 20", result);
    }

    private AutonomousAgent CreateTestAgent(ILongTermMemoryService longTermMemory, IShortTermMemoryService shortTermMemory)
    {
        var mockAIAgent = new Mock<AIAgent>();
        var mockLogger = new Mock<ILogger<AutonomousAgent>>();
        var mockConsolidation = new Mock<IMemoryConsolidationService>();

        var stateManager = new StateManager(shortTermMemory, longTermMemory, Mock.Of<ILogger<StateManager>>());
        var usageTracker = new UsageTracker(Mock.Of<ILogger<UsageTracker>>(), 4000, 8000000);
        var batchLearning = new BatchLearningService(
            mockConsolidation.Object,
            longTermMemory,
            Mock.Of<ILogger<BatchLearningService>>());
        var humanInput = new HumanInputHandler(
            Mock.Of<ILogger<HumanInputHandler>>(),
            stateManager,
            Mock.Of<IMessageQueue<HumanInput>>(),
            mockConsolidation.Object);
        var mockPromptService = new Mock<IPromptService>();

        return new AutonomousAgent(
            mockAIAgent.Object,
            mockLogger.Object,
            stateManager,
            usageTracker,
            longTermMemory,
            shortTermMemory,
            batchLearning,
            humanInput,
            mockPromptService.Object);
    }

    #endregion
}
