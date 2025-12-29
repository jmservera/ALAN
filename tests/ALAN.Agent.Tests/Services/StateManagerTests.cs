using ALAN.Agent.Services;
using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace ALAN.Agent.Tests.Services;

public class StateManagerTests
{
    private readonly Mock<IShortTermMemoryService> _mockShortTermMemory;
    private readonly Mock<ILongTermMemoryService> _mockLongTermMemory;
    private readonly Mock<ILogger<StateManager>> _mockLogger;
    private readonly StateManager _stateManager;

    public StateManagerTests()
    {
        _mockShortTermMemory = new Mock<IShortTermMemoryService>();
        _mockLongTermMemory = new Mock<ILongTermMemoryService>();
        _mockLogger = new Mock<ILogger<StateManager>>();
        _stateManager = new StateManager(_mockShortTermMemory.Object, _mockLongTermMemory.Object, _mockLogger.Object);
    }

    [Fact]
    public void GetCurrentState_ReturnsInitialState()
    {
        // Act
        var state = _stateManager.GetCurrentState();

        // Assert
        Assert.NotNull(state);
        Assert.NotNull(state.Id);
        Assert.Empty(state.RecentThoughts);
        Assert.Empty(state.RecentActions);
    }

    [Fact]
    public void AddThought_AddsToState()
    {
        // Arrange
        var thought = new AgentThought
        {
            Content = "Test thought",
            Type = ThoughtType.Planning
        };

        // Act
        _stateManager.AddThought(thought);
        var state = _stateManager.GetCurrentState();

        // Assert
        Assert.Single(state.RecentThoughts);
        Assert.Equal("Test thought", state.RecentThoughts[0].Content);
    }

    [Fact]
    public void AddThought_StoresInShortTermMemory()
    {
        // Arrange
        var thought = new AgentThought
        {
            Content = "Test thought",
            Type = ThoughtType.Planning
        };

        // Act
        _stateManager.AddThought(thought);

        // Assert
        _mockShortTermMemory.Verify(
            m => m.SetAsync(
                It.Is<string>(key => key.StartsWith("thought:")),
                It.IsAny<AgentThought>(),
                It.Is<TimeSpan?>(ts => ts == TimeSpan.FromHours(8)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void AddThought_LimitsTo100Thoughts()
    {
        // Arrange & Act
        for (int i = 0; i < 150; i++)
        {
            _stateManager.AddThought(new AgentThought { Content = $"Thought {i}" });
        }

        var state = _stateManager.GetCurrentState();

        // Assert
        Assert.Equal(20, state.RecentThoughts.Count); // Only returns last 20
    }

    [Fact]
    public void AddAction_AddsToState()
    {
        // Arrange
        var action = new AgentAction
        {
            Name = "TestAction",
            Description = "Test description",
            Status = ActionStatus.Running
        };

        // Act
        _stateManager.AddAction(action);
        var state = _stateManager.GetCurrentState();

        // Assert
        Assert.Single(state.RecentActions);
        Assert.Equal("TestAction", state.RecentActions[0].Name);
    }

    [Fact]
    public void AddAction_StoresInShortTermMemory()
    {
        // Arrange
        var action = new AgentAction
        {
            Name = "TestAction",
            Status = ActionStatus.Running
        };

        // Act
        _stateManager.AddAction(action);

        // Assert
        _mockShortTermMemory.Verify(
            m => m.SetAsync(
                It.Is<string>(key => key.StartsWith("action:")),
                It.IsAny<AgentAction>(),
                It.Is<TimeSpan?>(ts => ts == TimeSpan.FromHours(8)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void UpdateAction_UpdatesExistingAction()
    {
        // Arrange
        var action = new AgentAction
        {
            Name = "TestAction",
            Status = ActionStatus.Running
        };
        _stateManager.AddAction(action);

        // Act
        action.Status = ActionStatus.Completed;
        action.Output = "Success";
        _stateManager.UpdateAction(action);

        var state = _stateManager.GetCurrentState();

        // Assert
        Assert.Single(state.RecentActions);
        Assert.Equal(ActionStatus.Completed, state.RecentActions[0].Status);
        Assert.Equal("Success", state.RecentActions[0].Output);
    }

    [Fact]
    public void UpdateStatus_UpdatesAgentStatus()
    {
        // Arrange
        var originalStatus = _stateManager.GetCurrentState().Status;

        // Act
        _stateManager.UpdateStatus(AgentStatus.Thinking);
        var state = _stateManager.GetCurrentState();

        // Assert
        Assert.Equal(AgentStatus.Thinking, state.Status);
    }

    [Fact]
    public void UpdateStatus_StoresInShortTermMemory()
    {
        // Act
        _stateManager.UpdateStatus(AgentStatus.Acting);

        // Assert
        _mockShortTermMemory.Verify(
            m => m.SetAsync(
                "agent:current-state",
                It.IsAny<AgentState>(),
                It.Is<TimeSpan?>(ts => ts == TimeSpan.FromHours(1)),
                It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public void UpdateGoal_UpdatesCurrentGoal()
    {
        // Arrange
        var goal = "Test autonomous agent";

        // Act
        _stateManager.UpdateGoal(goal);
        var state = _stateManager.GetCurrentState();

        // Assert
        Assert.Equal(goal, state.CurrentGoal);
    }

    [Fact]
    public void UpdatePrompt_UpdatesCurrentPrompt()
    {
        // Arrange
        var prompt = "What should I do next?";

        // Act
        _stateManager.UpdatePrompt(prompt);
        var state = _stateManager.GetCurrentState();

        // Assert
        Assert.Equal(prompt, state.CurrentPrompt);
    }

    [Fact]
    public void StateChanged_EventIsRaised()
    {
        // Arrange
        AgentState? capturedState = null;
        _stateManager.StateChanged += (sender, state) => capturedState = state;

        // Act
        _stateManager.UpdateStatus(AgentStatus.Thinking);

        // Assert
        Assert.NotNull(capturedState);
        Assert.Equal(AgentStatus.Thinking, capturedState.Status);
    }

    [Fact]
    public async Task GetAllThoughtsFromMemoryAsync_ReturnsThoughts()
    {
        // Arrange
        _stateManager.AddThought(new AgentThought { Content = "Thought 1" });
        _stateManager.AddThought(new AgentThought { Content = "Thought 2" });

        // Act
        var thoughts = await _stateManager.GetAllThoughtsFromMemoryAsync();

        // Assert
        Assert.Equal(2, thoughts.Count);
    }

    [Fact]
    public async Task GetAllActionsFromMemoryAsync_ReturnsActions()
    {
        // Arrange
        _stateManager.AddAction(new AgentAction { Name = "Action 1" });
        _stateManager.AddAction(new AgentAction { Name = "Action 2" });

        // Act
        var actions = await _stateManager.GetAllActionsFromMemoryAsync();

        // Assert
        Assert.Equal(2, actions.Count);
    }
}
