using ALAN.Agent.Services;
using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using ALAN.Shared.Services.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.AI;
using Moq;
using ALAN.Shared.Services;

namespace ALAN.Agent.Tests.Services;

public class StateRestorationTests
{
    private readonly Mock<AIAgent> _mockAIAgent;
    private readonly Mock<ILogger<AutonomousAgent>> _mockLogger;
    private readonly Mock<ILongTermMemoryService> _mockLongTermMemory;
    private readonly Mock<IShortTermMemoryService> _mockShortTermMemory;
    private readonly StateManager _stateManager;
    private readonly UsageTracker _usageTracker;
    private readonly Mock<IPromptService> _mockPromptService;
    private readonly Mock<IMemoryConsolidationService> _mockConsolidation;

    public StateRestorationTests()
    {
        _mockAIAgent = new Mock<AIAgent>();
        _mockLogger = new Mock<ILogger<AutonomousAgent>>();
        _mockLongTermMemory = new Mock<ILongTermMemoryService>();
        _mockShortTermMemory = new Mock<IShortTermMemoryService>();
        _mockConsolidation = new Mock<IMemoryConsolidationService>();
        _mockPromptService = new Mock<IPromptService>();

        _stateManager = new StateManager(_mockShortTermMemory.Object, _mockLongTermMemory.Object);
        _usageTracker = new UsageTracker(Mock.Of<ILogger<UsageTracker>>(), 4000, 8000000);
    }

    private AutonomousAgent CreateAgent()
    {
        var batchLearning = new BatchLearningService(
            _mockConsolidation.Object,
            _mockLongTermMemory.Object,
            Mock.Of<ILogger<BatchLearningService>>());
        var humanInput = new HumanInputHandler(
            Mock.Of<ILogger<HumanInputHandler>>(),
            _stateManager,
            Mock.Of<IMessageQueue<HumanInput>>(),
            _mockConsolidation.Object);

        return new AutonomousAgent(
            _mockAIAgent.Object,
            _mockLogger.Object,
            _stateManager,
            _usageTracker,
            _mockLongTermMemory.Object,
            _mockShortTermMemory.Object,
            batchLearning,
            humanInput,
            _mockPromptService.Object);
    }

    [Fact]
    public async Task RestoreState_WithValidRecentState_RestoresSuccessfully()
    {
        // Arrange
        var previousState = new AgentState
        {
            Id = "test-id",
            LastUpdated = DateTime.UtcNow.AddHours(-2), // 2 hours ago
            CurrentGoal = "Previous goal",
            CurrentPrompt = "Previous prompt",
            Status = AgentStatus.Idle
        };

        _mockShortTermMemory
            .Setup(m => m.GetAsync<AgentState>("agent:current-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync(previousState);

        // Setup for memory loading
        _mockLongTermMemory
            .Setup(m => m.GetMemoriesByTypeAsync(It.IsAny<MemoryType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockShortTermMemory
            .Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var agent = CreateAgent();

        // Act
        var restoreResult = await agent.TryRestorePreviousStateAsync(CancellationToken.None);

        // Assert
        Assert.True(restoreResult);
        var currentState = _stateManager.GetCurrentState();
        Assert.Equal("Previous goal", currentState.CurrentGoal);
        Assert.Equal("Previous prompt", currentState.CurrentPrompt);

        // Verify logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Restored previous state")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RestoreState_WithStateTooOld_StartsFromScratch()
    {
        // Arrange - State is 25 hours old (exceeds 24-hour threshold)
        var oldState = new AgentState
        {
            Id = "test-id",
            LastUpdated = DateTime.UtcNow.AddHours(-25),
            CurrentGoal = "Old goal",
            CurrentPrompt = "Old prompt",
            Status = AgentStatus.Idle
        };

        _mockShortTermMemory
            .Setup(m => m.GetAsync<AgentState>("agent:current-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync(oldState);

        var agent = CreateAgent();

        // Act
        var restoreResult = await agent.TryRestorePreviousStateAsync(CancellationToken.None);

        // Assert
        Assert.False(restoreResult);
        var currentState = _stateManager.GetCurrentState();
        Assert.NotEqual("Old goal", currentState.CurrentGoal);

        // Verify logging indicates state was too old
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("too old")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RestoreState_WithNoPreviousState_StartsFromScratch()
    {
        // Arrange - No previous state exists
        _mockShortTermMemory
            .Setup(m => m.GetAsync<AgentState>("agent:current-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync((AgentState?)null);

        var agent = CreateAgent();

        // Act
        var restoreResult = await agent.TryRestorePreviousStateAsync(CancellationToken.None);

        // Assert
        Assert.False(restoreResult);

        // Verify logging indicates no previous state
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("No previous state found")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RestoreState_WithCorruptState_HandlesGracefully()
    {
        // Arrange - Short-term memory throws exception (simulating corrupt data)
        _mockShortTermMemory
            .Setup(m => m.GetAsync<AgentState>("agent:current-state", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Corrupt data"));

        var agent = CreateAgent();

        // Act
        var restoreResult = await agent.TryRestorePreviousStateAsync(CancellationToken.None);

        // Assert
        Assert.False(restoreResult);

        // Verify error logging
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Failed to restore previous state")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task RestoreState_WithStateAtThreshold_RestoresSuccessfully()
    {
        // Arrange - State is exactly 24 hours old (at threshold)
        var thresholdState = new AgentState
        {
            Id = "test-id",
            LastUpdated = DateTime.UtcNow.AddHours(-23.9), // Just under 24 hours
            CurrentGoal = "Threshold goal",
            CurrentPrompt = "Threshold prompt",
            Status = AgentStatus.Idle
        };

        _mockShortTermMemory
            .Setup(m => m.GetAsync<AgentState>("agent:current-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync(thresholdState);

        // Setup for memory loading
        _mockLongTermMemory
            .Setup(m => m.GetMemoriesByTypeAsync(It.IsAny<MemoryType>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _mockShortTermMemory
            .Setup(m => m.GetKeysAsync("action:*", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var agent = CreateAgent();

        // Act
        var restoreResult = await agent.TryRestorePreviousStateAsync(CancellationToken.None);

        // Assert - Should restore since it's just under threshold
        Assert.True(restoreResult);
        var currentState = _stateManager.GetCurrentState();
        Assert.Equal("Threshold goal", currentState.CurrentGoal);
    }

    [Fact]
    public async Task RestoreState_WithErrorStatus_StartsFromScratch()
    {
        // Arrange - Previous state had Error status
        var errorState = new AgentState
        {
            Id = "test-id",
            LastUpdated = DateTime.UtcNow.AddHours(-1),
            CurrentGoal = "Error goal",
            CurrentPrompt = "Error prompt",
            Status = AgentStatus.Error
        };

        _mockShortTermMemory
            .Setup(m => m.GetAsync<AgentState>("agent:current-state", It.IsAny<CancellationToken>()))
            .ReturnsAsync(errorState);

        var agent = CreateAgent();

        // Act
        var restoreResult = await agent.TryRestorePreviousStateAsync(CancellationToken.None);

        // Assert
        Assert.False(restoreResult);

        // Verify logging indicates error state was ignored
        _mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("error status")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
