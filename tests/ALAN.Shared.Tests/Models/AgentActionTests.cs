using ALAN.Shared.Models;

namespace ALAN.Shared.Tests.Models;

public class AgentActionTests
{
    [Fact]
    public void AgentAction_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var action = new AgentAction();

        // Assert
        Assert.NotNull(action.Id);
        Assert.NotEqual(Guid.Empty.ToString(), action.Id);
        Assert.True(action.Timestamp <= DateTime.UtcNow);
        Assert.Equal(string.Empty, action.Name);
        Assert.Equal(string.Empty, action.Description);
        Assert.Equal(string.Empty, action.Input);
        Assert.Null(action.Output);
        Assert.Equal(ActionStatus.Pending, action.Status);
        Assert.Null(action.ToolCalls);
    }

    [Theory]
    [InlineData(ActionStatus.Pending)]
    [InlineData(ActionStatus.Running)]
    [InlineData(ActionStatus.Completed)]
    [InlineData(ActionStatus.Failed)]
    public void AgentAction_Status_CanBeSetToAnyValue(ActionStatus status)
    {
        // Arrange
        var action = new AgentAction();

        // Act
        action.Status = status;

        // Assert
        Assert.Equal(status, action.Status);
    }

    [Fact]
    public void AgentAction_Properties_CanBeSet()
    {
        // Arrange
        var action = new AgentAction();

        // Act
        action.Name = "TestAction";
        action.Description = "This is a test action";
        action.Input = "test input";
        action.Output = "test output";

        // Assert
        Assert.Equal("TestAction", action.Name);
        Assert.Equal("This is a test action", action.Description);
        Assert.Equal("test input", action.Input);
        Assert.Equal("test output", action.Output);
    }

    [Fact]
    public void AgentAction_ToolCalls_CanBePopulated()
    {
        // Arrange
        var action = new AgentAction();
        var toolCalls = new List<ToolCall>
        {
            new()
            {
                ToolName = "ExecuteTool",
                Success = true,
                DurationMs = 100
            },
            new()
            {
                ToolName = "VerifyTool",
                Success = false,
                DurationMs = 50
            }
        };

        // Act
        action.ToolCalls = toolCalls;

        // Assert
        Assert.NotNull(action.ToolCalls);
        Assert.Equal(2, action.ToolCalls.Count);
        Assert.Contains(action.ToolCalls, tc => tc.ToolName == "ExecuteTool" && tc.Success);
        Assert.Contains(action.ToolCalls, tc => tc.ToolName == "VerifyTool" && !tc.Success);
    }
}
