namespace ALAN.Shared.Models;

/// <summary>
/// Represents a chat message between human and agent.
/// </summary>
public class ChatMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ChatMessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
}

public enum ChatMessageRole
{
    Human,
    Agent
}

/// <summary>
/// Request to send a chat message to the agent.
/// </summary>
public class ChatRequest
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Response from the agent to a chat message.
/// </summary>
public class ChatResponse
{
    public string MessageId { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
