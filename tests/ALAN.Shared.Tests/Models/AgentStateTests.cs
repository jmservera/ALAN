using ALAN.Shared.Models;

namespace ALAN.Shared.Tests.Models;

public class AgentStateTests
{
    [Fact]
    public void AgentState_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var state = new AgentState();

        // Assert
        Assert.NotNull(state.Id);
        Assert.NotEqual(Guid.Empty.ToString(), state.Id);
        Assert.True(state.LastUpdated <= DateTime.UtcNow);
        Assert.Equal(string.Empty, state.CurrentGoal);
        Assert.Equal(AgentStatus.Idle, state.Status);
        Assert.Null(state.CurrentPrompt);
        Assert.NotNull(state.RecentThoughts);
        Assert.Empty(state.RecentThoughts);
        Assert.NotNull(state.RecentActions);
        Assert.Empty(state.RecentActions);
    }

    [Theory]
    [InlineData(AgentStatus.Idle)]
    [InlineData(AgentStatus.Thinking)]
    [InlineData(AgentStatus.Acting)]
    [InlineData(AgentStatus.Paused)]
    [InlineData(AgentStatus.Throttled)]
    [InlineData(AgentStatus.Error)]
    public void AgentState_Status_CanBeSetToAnyValue(AgentStatus status)
    {
        // Arrange
        var state = new AgentState();

        // Act
        state.Status = status;

        // Assert
        Assert.Equal(status, state.Status);
    }

    [Fact]
    public void AgentState_CurrentGoal_CanBeSet()
    {
        // Arrange
        var state = new AgentState();
        var goal = "Test autonomous agent functionality";

        // Act
        state.CurrentGoal = goal;

        // Assert
        Assert.Equal(goal, state.CurrentGoal);
    }

    [Fact]
    public void AgentState_RecentThoughts_CanBePopulated()
    {
        // Arrange
        var state = new AgentState();
        var thoughts = new List<AgentThought>
        {
            new() { Content = "Thought 1", Type = ThoughtType.Observation },
            new() { Content = "Thought 2", Type = ThoughtType.Planning }
        };

        // Act
        state.RecentThoughts = thoughts;

        // Assert
        Assert.Equal(2, state.RecentThoughts.Count);
        Assert.Contains(state.RecentThoughts, t => t.Content == "Thought 1");
        Assert.Contains(state.RecentThoughts, t => t.Content == "Thought 2");
    }

    [Fact]
    public void AgentState_RecentActions_CanBePopulated()
    {
        // Arrange
        var state = new AgentState();
        var actions = new List<AgentAction>
        {
            new() { Name = "Action 1", Status = ActionStatus.Completed },
            new() { Name = "Action 2", Status = ActionStatus.Running }
        };

        // Act
        state.RecentActions = actions;

        // Assert
        Assert.Equal(2, state.RecentActions.Count);
        Assert.Contains(state.RecentActions, a => a.Name == "Action 1");
        Assert.Contains(state.RecentActions, a => a.Name == "Action 2");
    }
}
