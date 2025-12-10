using ALAN.Shared.Models;

namespace ALAN.Shared.Tests.Models;

public class AgentThoughtTests
{
    [Fact]
    public void AgentThought_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var thought = new AgentThought();

        // Assert
        Assert.NotNull(thought.Id);
        Assert.NotEqual(Guid.Empty.ToString(), thought.Id);
        Assert.True(thought.Timestamp <= DateTime.UtcNow);
        Assert.Equal(string.Empty, thought.Content);
        Assert.Equal(ThoughtType.Observation, thought.Type);
        Assert.Null(thought.ToolCalls);
    }

    [Theory]
    [InlineData(ThoughtType.Observation)]
    [InlineData(ThoughtType.Planning)]
    [InlineData(ThoughtType.Reasoning)]
    [InlineData(ThoughtType.Decision)]
    [InlineData(ThoughtType.Reflection)]
    public void AgentThought_Type_CanBeSetToAnyValue(ThoughtType type)
    {
        // Arrange
        var thought = new AgentThought();

        // Act
        thought.Type = type;

        // Assert
        Assert.Equal(type, thought.Type);
    }

    [Fact]
    public void AgentThought_Content_CanBeSet()
    {
        // Arrange
        var thought = new AgentThought();
        var content = "I need to analyze the current state";

        // Act
        thought.Content = content;

        // Assert
        Assert.Equal(content, thought.Content);
    }

    [Fact]
    public void AgentThought_ToolCalls_CanBePopulated()
    {
        // Arrange
        var thought = new AgentThought();
        var toolCalls = new List<ToolCall>
        {
            new()
            {
                ToolName = "SearchTool",
                McpServer = "github-mcp",
                Arguments = "{\"query\":\"test\"}",
                Result = "Success",
                Success = true,
                DurationMs = 150.5
            }
        };

        // Act
        thought.ToolCalls = toolCalls;

        // Assert
        Assert.NotNull(thought.ToolCalls);
        Assert.Single(thought.ToolCalls);
        Assert.Equal("SearchTool", thought.ToolCalls[0].ToolName);
        Assert.True(thought.ToolCalls[0].Success);
    }
}

public class ToolCallTests
{
    [Fact]
    public void ToolCall_DefaultConstructor_SetsDefaultValues()
    {
        // Arrange & Act
        var toolCall = new ToolCall();

        // Assert
        Assert.Equal(string.Empty, toolCall.ToolName);
        Assert.Null(toolCall.McpServer);
        Assert.Null(toolCall.Arguments);
        Assert.Null(toolCall.Result);
        Assert.True(toolCall.Success);
        Assert.Null(toolCall.DurationMs);
    }

    [Fact]
    public void ToolCall_AllProperties_CanBeSet()
    {
        // Arrange
        var toolCall = new ToolCall();

        // Act
        toolCall.ToolName = "TestTool";
        toolCall.McpServer = "test-mcp";
        toolCall.Arguments = "{\"key\":\"value\"}";
        toolCall.Result = "Test result";
        toolCall.Success = false;
        toolCall.DurationMs = 250.75;

        // Assert
        Assert.Equal("TestTool", toolCall.ToolName);
        Assert.Equal("test-mcp", toolCall.McpServer);
        Assert.Equal("{\"key\":\"value\"}", toolCall.Arguments);
        Assert.Equal("Test result", toolCall.Result);
        Assert.False(toolCall.Success);
        Assert.Equal(250.75, toolCall.DurationMs);
    }
}
