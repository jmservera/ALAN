namespace ALAN.Shared.Models;

/// <summary>
/// Represents input from a human operator to steer the agent.
/// </summary>
public class HumanInput
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public HumanInputType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public bool Processed { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = new();
}

public enum HumanInputType
{
    UpdatePrompt,
    PauseAgent,
    ResumeAgent,
    TriggerBatchLearning,
    ApproveCodeChange,
    RejectCodeChange,
    AddGoal,
    RemoveGoal,
    QueryState,
    ResetMemory,
    ChatWithAgent
}

/// <summary>
/// Response to human input.
/// </summary>
public class HumanInputResponse
{
    public string InputId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Dictionary<string, object> Data { get; set; } = new();
}
