namespace ALAN.Shared.Models;

public class AgentThought
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Content { get; set; } = string.Empty;
    public ThoughtType Type { get; set; }

    // MCP and tool usage tracking (minimal metadata)
    public List<ToolCall>? ToolCalls { get; set; }
}

public enum ThoughtType
{
    Observation,
    Planning,
    Reasoning,
    Decision,
    Reflection
}

public class ToolCall
{
    public string ToolName { get; set; } = string.Empty;
    public string? McpServer { get; set; }
    public string? Arguments { get; set; }
    public string? Result { get; set; }
    public bool Success { get; set; } = true;
    public double? DurationMs { get; set; }
}
