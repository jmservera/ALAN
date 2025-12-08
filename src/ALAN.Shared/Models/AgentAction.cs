namespace ALAN.Shared.Models;

public class AgentAction
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Input { get; set; } = string.Empty;
    public string? Output { get; set; }
    public ActionStatus Status { get; set; }
}

public enum ActionStatus
{
    Pending,
    Running,
    Completed,
    Failed
}
