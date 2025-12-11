using ALAN.Shared.Models;
using ALAN.Shared.Services.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.AI;
using System.Collections.Concurrent;

namespace ALAN.Agent.Services;

/// <summary>
/// Manages human input commands for steering the agent.
/// Uses Azure Storage Queue for reliable message processing.
/// </summary>
public class HumanInputHandler
{
    private readonly ConcurrentQueue<HumanInput> _inputQueue = new();
    private readonly ILogger<HumanInputHandler> _logger;
    private readonly StateManager _stateManager;
    private readonly IMessageQueue<HumanInput> _humanInputQueue;
    private readonly IMessageQueue<ChatResponse> _chatResponseQueue;
    private readonly AIAgent _aiAgent;
    private AutonomousAgent? _agent;

    public HumanInputHandler(
        ILogger<HumanInputHandler> logger,
        StateManager stateManager,
        IMessageQueue<HumanInput> humanInputQueue,
        IMessageQueue<ChatResponse> chatResponseQueue,
        AIAgent aiAgent)
    {
        _logger = logger;
        _stateManager = stateManager;
        _humanInputQueue = humanInputQueue;
        _chatResponseQueue = chatResponseQueue;
        _aiAgent = aiAgent;
    }

    public void SetAgent(AutonomousAgent agent)
    {
        _agent = agent;
    }

    /// <summary>
    /// Submit a new human input command.
    /// </summary>
    public string SubmitInput(HumanInput input)
    {
        _inputQueue.Enqueue(input);
        _logger.LogInformation("Received human input: {Type} - {Content}", input.Type, input.Content);
        return input.Id;
    }

    /// <summary>
    /// Process pending human inputs.
    /// Should be called periodically from the agent loop.
    /// </summary>
    public async Task<List<HumanInputResponse>> ProcessPendingInputsAsync(AutonomousAgent agent, CancellationToken cancellationToken = default)
    {
        var responses = new List<HumanInputResponse>();

        // Process messages from the queue
        await ProcessQueuedInputsAsync(agent, cancellationToken);

        // Process inputs from the in-memory queue (legacy support)
        while (_inputQueue.TryDequeue(out var input))
        {
            try
            {
                var response = await ProcessInputAsync(input, agent, cancellationToken);
                responses.Add(response);
                input.Processed = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing human input {Id}", input.Id);
                responses.Add(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = false,
                    Message = $"Error: {ex.Message}"
                });
            }
        }

        return responses;
    }

    private async Task ProcessQueuedInputsAsync(AutonomousAgent agent, CancellationToken cancellationToken)
    {
        try
        {
            // Receive messages from the human input queue
            var messages = await _humanInputQueue.ReceiveAsync(
                maxMessages: 10,
                visibilityTimeout: TimeSpan.FromSeconds(30),
                cancellationToken: cancellationToken);

            foreach (var msg in messages)
            {
                try
                {
                    var input = msg.Content;
                    _logger.LogInformation("Processing queued input: {Type}", input.Type);

                    // Handle chat requests separately
                    if (input.Type == HumanInputType.ChatWithAgent)
                    {
                        await ProcessChatRequestAsync(input, msg.MessageId, msg.PopReceipt, cancellationToken);
                    }
                    else
                    {
                        // Process other human inputs
                        await ProcessInputAsync(input, agent, cancellationToken);
                        
                        // Delete message after successful processing
                        await _humanInputQueue.DeleteAsync(msg.MessageId, msg.PopReceipt, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing queued input {MessageId}", msg.MessageId);
                    // Message will become visible again for retry
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error receiving messages from human input queue");
        }
    }

    private async Task ProcessChatRequestAsync(HumanInput chatRequest, string messageId, string popReceipt, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Processing chat request: {Content}", chatRequest.Content);

            // Get a new thread for this chat conversation
            var chatThread = _aiAgent.GetNewThread();

            // Create a prompt that includes context about the agent's knowledge
            var prompt = $@"You are ALAN, an autonomous AI agent. A human user is chatting with you to learn about your knowledge and capabilities.

User message: {chatRequest.Content}

Please respond naturally and helpfully. You can share:
- Your current goals and what you're learning
- Your capabilities and the tools you have access to
- Your thoughts on self-improvement and learning
- Any insights from your memory and experiences

Keep your response concise and conversational.";

            var result = await _aiAgent.RunAsync(prompt, chatThread, cancellationToken: cancellationToken);
            var responseText = result.Text ?? result.ToString();

            // Send the response to the chat response queue
            var chatResponse = new ChatResponse
            {
                MessageId = chatRequest.Id,
                Response = responseText,
                Timestamp = DateTime.UtcNow
            };

            await _chatResponseQueue.SendAsync(chatResponse, cancellationToken);
            
            // Delete the chat request message after successful processing
            await _humanInputQueue.DeleteAsync(messageId, popReceipt, cancellationToken);

            _logger.LogInformation("Chat response sent for request {Id}", chatRequest.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating chat response");

            // Send error response
            var errorResponse = new ChatResponse
            {
                MessageId = chatRequest.Id,
                Response = "I'm sorry, I encountered an error processing your message. Please try again.",
                Timestamp = DateTime.UtcNow
            };

            await _chatResponseQueue.SendAsync(errorResponse, cancellationToken);
            
            // Delete the chat request message to prevent retry
            await _humanInputQueue.DeleteAsync(messageId, popReceipt, cancellationToken);
        }
    }

    private Task<HumanInputResponse> ProcessInputAsync(HumanInput input, AutonomousAgent agent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing human input: {Type}", input.Type);

        switch (input.Type)
        {
            case HumanInputType.UpdatePrompt:
                agent.UpdatePrompt(input.Content);
                return Task.FromResult(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Prompt updated successfully"
                });

            case HumanInputType.PauseAgent:
                agent.Pause();
                return Task.FromResult(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Agent paused"
                });

            case HumanInputType.ResumeAgent:
                agent.Resume();
                return Task.FromResult(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Agent resumed"
                });

            case HumanInputType.TriggerBatchLearning:
                // This would trigger the batch learning process
                _logger.LogInformation("Batch learning trigger requested by human");
                return Task.FromResult(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Batch learning will be triggered in next iteration"
                });

            case HumanInputType.QueryState:
                var state = _stateManager.GetCurrentState();
                return Task.FromResult(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Current state retrieved",
                    Data = new Dictionary<string, object>
                    {
                        ["state"] = state
                    }
                });

            case HumanInputType.AddGoal:
                _stateManager.UpdateGoal(input.Content);
                return Task.FromResult(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = $"Goal updated to: {input.Content}"
                });

            case HumanInputType.ChatWithAgent:
                // Chat requests are handled separately in ProcessChatRequestsAsync
                return Task.FromResult(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Chat request processed"
                });

            default:
                _logger.LogWarning("Unknown input type: {Type}", input.Type);
                return Task.FromResult(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = false,
                    Message = $"Unknown input type: {input.Type}"
                });
        }
    }

    /// <summary>
    /// Get count of pending inputs.
    /// </summary>
    public int GetPendingCount()
    {
        return _inputQueue.Count;
    }

    /// <summary>
    /// Clear all pending inputs.
    /// </summary>
    public void ClearPending()
    {
        while (_inputQueue.TryDequeue(out _)) { }
        _logger.LogInformation("Cleared all pending inputs");
    }
}
