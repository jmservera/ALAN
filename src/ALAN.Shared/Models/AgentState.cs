namespace ALAN.Shared.Models;

public class AgentState
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    public string CurrentGoal { get; set; } = string.Empty;
    public AgentStatus Status { get; set; }
    public string? CurrentPrompt { get; set; }
    public List<AgentThought> RecentThoughts { get; set; } = new();
    public List<AgentAction> RecentActions { get; set; } = new();
}

public enum AgentStatus
{
    Idle,
    Thinking,
    Acting,
    Paused,
    Throttled,
    Error
}
