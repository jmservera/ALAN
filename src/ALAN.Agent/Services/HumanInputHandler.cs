using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Agents.AI;
using System.Collections.Concurrent;

namespace ALAN.Agent.Services;

/// <summary>
/// Manages human input commands for steering the agent.
/// Provides a queue-based system for processing human directives.
/// </summary>
public class HumanInputHandler
{
    private readonly ConcurrentQueue<HumanInput> _inputQueue = new();
    private readonly ILogger<HumanInputHandler> _logger;
    private readonly StateManager _stateManager;
    private readonly IShortTermMemoryService _shortTermMemory;
    private readonly AIAgent _aiAgent;
    private AutonomousAgent? _agent;

    public HumanInputHandler(
        ILogger<HumanInputHandler> logger,
        StateManager stateManager,
        IShortTermMemoryService shortTermMemory,
        AIAgent aiAgent)
    {
        _logger = logger;
        _stateManager = stateManager;
        _shortTermMemory = shortTermMemory;
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

        // Check for chat requests in memory
        await ProcessChatRequestsAsync(cancellationToken);

        // Check for human inputs in memory
        await ProcessHumanInputsFromMemoryAsync(agent, cancellationToken);

        // Process inputs from the queue
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

    private async Task ProcessHumanInputsFromMemoryAsync(AutonomousAgent agent, CancellationToken cancellationToken)
    {
        try
        {
            // Look for human inputs in short-term memory
            var keys = await _shortTermMemory.GetKeysAsync("human-input:*", cancellationToken);
            
            foreach (var key in keys)
            {
                var input = await _shortTermMemory.GetAsync<HumanInput>(key, cancellationToken);
                if (input != null && !input.Processed)
                {
                    _logger.LogInformation("Processing human input from memory: {Type}", input.Type);
                    
                    try
                    {
                        await ProcessInputAsync(input, agent, cancellationToken);
                        
                        // Mark as processed and remove from memory
                        input.Processed = true;
                        await _shortTermMemory.DeleteAsync(key, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error processing human input from memory {Id}", input.Id);
                        await _shortTermMemory.DeleteAsync(key, cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing human inputs from memory");
        }
    }

    private async Task ProcessChatRequestsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Look for chat requests in short-term memory
            var keys = await _shortTermMemory.GetKeysAsync("chat-request:*", cancellationToken);
            
            foreach (var key in keys)
            {
                var chatRequest = await _shortTermMemory.GetAsync<HumanInput>(key, cancellationToken);
                if (chatRequest != null && chatRequest.Type == HumanInputType.ChatWithAgent)
                {
                    _logger.LogInformation("Processing chat request: {Content}", chatRequest.Content);
                    
                    try
                    {
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

                        // Store the response
                        var chatResponse = new ChatResponse
                        {
                            MessageId = chatRequest.Id,
                            Response = responseText,
                            Timestamp = DateTime.UtcNow
                        };

                        await _shortTermMemory.SetAsync(
                            $"chat-response:{chatRequest.Id}",
                            chatResponse,
                            TimeSpan.FromMinutes(5),
                            cancellationToken);

                        _logger.LogInformation("Chat response generated for request {Id}", chatRequest.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error generating chat response");
                        
                        // Store error response
                        var errorResponse = new ChatResponse
                        {
                            MessageId = chatRequest.Id,
                            Response = "I'm sorry, I encountered an error processing your message. Please try again.",
                            Timestamp = DateTime.UtcNow
                        };

                        await _shortTermMemory.SetAsync(
                            $"chat-response:{chatRequest.Id}",
                            errorResponse,
                            TimeSpan.FromMinutes(5),
                            cancellationToken);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat requests");
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
