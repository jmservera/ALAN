using ALAN.Shared.Models;
using ALAN.Shared.Services.Queue;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ALAN.Agent.Services;

/// <summary>
/// Manages human input commands for steering the agent.
/// Uses Azure Storage Queue for reliable message processing.
/// Chat functionality is handled by the separate ChatApi service.
/// </summary>
public class HumanInputHandler
{
    private readonly ConcurrentQueue<HumanInput> _inputQueue = new();
    private readonly ILogger<HumanInputHandler> _logger;
    private readonly StateManager _stateManager;
    private readonly IMessageQueue<HumanInput> _humanInputQueue;
    private AutonomousAgent? _agent;

    public HumanInputHandler(
        ILogger<HumanInputHandler> logger,
        StateManager stateManager,
        IMessageQueue<HumanInput> humanInputQueue)
    {
        _logger = logger;
        _stateManager = stateManager;
        _humanInputQueue = humanInputQueue;
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
        const int maxRetryCount = 5; // Maximum number of retries before considering message as dead-letter
        
        try
        {
            // Receive messages from the human input queue (steering commands only)
            var messages = await _humanInputQueue.ReceiveAsync(
                maxMessages: 10,
                visibilityTimeout: TimeSpan.FromSeconds(60), // Increased from 30 to 60 for longer operations
                cancellationToken: cancellationToken);

            foreach (var msg in messages)
            {
                try
                {
                    var input = msg.Content;
                    _logger.LogInformation("Processing queued input: {Type} (DequeueCount: {Count})", 
                        input.Type, msg.DequeueCount);

                    // Check if message has been retried too many times
                    if (msg.DequeueCount > maxRetryCount)
                    {
                        _logger.LogError(
                            "Message {MessageId} exceeded max retry count ({MaxRetry}). Moving to dead-letter. Type: {Type}, Content: {Content}",
                            msg.MessageId, maxRetryCount, input.Type, input.Content);
                        
                        // Delete message to prevent infinite retry
                        await _humanInputQueue.DeleteAsync(msg.MessageId, msg.PopReceipt, cancellationToken);
                        continue;
                    }

                    // Skip chat requests - they are handled by ChatApi
                    if (input.Type == HumanInputType.ChatWithAgent)
                    {
                        _logger.LogWarning("Chat request in steering queue - deleting (should go to ChatApi)");
                        await _humanInputQueue.DeleteAsync(msg.MessageId, msg.PopReceipt, cancellationToken);
                        continue;
                    }

                    // Process steering commands
                    await ProcessInputAsync(input, agent, cancellationToken);
                    
                    // Delete message after successful processing
                    await _humanInputQueue.DeleteAsync(msg.MessageId, msg.PopReceipt, cancellationToken);
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
                // Chat requests are handled by ChatApi via WebSockets
                _logger.LogWarning("Chat request should not be in steering queue - redirecting to ChatApi");
                return Task.FromResult(new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = false,
                    Message = "Chat requests should be sent to ChatApi WebSocket endpoint"
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
