namespace ALAN.Shared.Models;

public class AgentThought
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Content { get; set; } = string.Empty;
    public ThoughtType Type { get; set; }
}

public enum ThoughtType
{
    Observation,
    Planning,
    Reasoning,
    Decision,
    Reflection
}
