using ALAN.Shared.Services.Memory;
using Microsoft.Agents.AI;
using System.Text;

namespace ALAN.ChatApi.Services;

/// <summary>
/// Service for handling real-time chat with the AI agent via WebSockets with streaming support.
/// </summary>
public class ChatService : IDisposable
{
    private readonly AIAgent _agent;
    private readonly ILogger<ChatService> _logger;
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly IPromptService _promptService;
    private readonly Dictionary<string, AgentThread> _activeThreads = [];
    private readonly SemaphoreSlim _threadLock = new(1, 1);
    private bool _disposed;

    public ChatService(
        AIAgent agent,
        ILogger<ChatService> logger,
        ILongTermMemoryService longTermMemory,
        IPromptService promptService)
    {
        _agent = agent;
        _logger = logger;
        _longTermMemory = longTermMemory; // Reserved for future use - retrieving conversation context from long-term memory
        _promptService = promptService;
    }

    /// <summary>
    /// Process a chat message and stream the response.
    /// </summary>
    public async Task<string> ProcessChatAsync(
        string sessionId,
        string message,
        Func<string, Task> onTokenReceived,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing chat for session {SessionId}: {Message}", sessionId, message);

        try
        {
            // Get or create thread for this session
            var thread = await GetOrCreateThreadAsync(sessionId, cancellationToken);

            // Create a prompt that includes context about the agent's knowledge
            var prompt = _promptService.RenderTemplate("chat-conversation", new { message });

            // Run with streaming
            var responseBuilder = new StringBuilder();
            
            await foreach (var streamEvent in _agent.RunStreamingAsync(prompt, thread, cancellationToken: cancellationToken))
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Chat processing cancelled for session {SessionId}", sessionId);
                    break;
                }

                var token = streamEvent.Text ?? string.Empty;
                if (!string.IsNullOrEmpty(token))
                {
                    responseBuilder.Append(token);
                    await onTokenReceived(token);
                }
            }

            var fullResponse = responseBuilder.ToString();
            _logger.LogInformation("Chat completed for session {SessionId}, response length: {Length}", 
                sessionId, fullResponse.Length);

            return fullResponse;
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Chat cancelled for session {SessionId}", sessionId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat for session {SessionId}", sessionId);
            throw;
        }
    }

    /// <summary>
    /// Clear chat history for a session.
    /// </summary>
    public async Task ClearHistoryAsync(string sessionId, CancellationToken cancellationToken)
    {
        await _threadLock.WaitAsync(cancellationToken);
        try
        {
            if (_activeThreads.Remove(sessionId))
            {
                _logger.LogInformation("Cleared chat history for session {SessionId}", sessionId);
            }
        }
        finally
        {
            _threadLock.Release();
        }
    }

    private async Task<AgentThread> GetOrCreateThreadAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        await _threadLock.WaitAsync(cancellationToken);
        try
        {
            if (!_activeThreads.TryGetValue(sessionId, out var thread))
            {
                thread = _agent.GetNewThread();
                _activeThreads[sessionId] = thread;
                _logger.LogInformation("Created new thread for session {SessionId}", sessionId);
            }
            return thread;
        }
        finally
        {
            _threadLock.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _threadLock?.Dispose();
        _disposed = true;
    }
}
