using ALAN.Shared.Models;
using Microsoft.Extensions.Logging;
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
    private AutonomousAgent? _agent;

    public HumanInputHandler(
        ILogger<HumanInputHandler> logger,
        StateManager stateManager)
    {
        _logger = logger;
        _stateManager = stateManager;
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

    private async Task<HumanInputResponse> ProcessInputAsync(HumanInput input, AutonomousAgent agent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing human input: {Type}", input.Type);

        switch (input.Type)
        {
            case HumanInputType.UpdatePrompt:
                agent.UpdatePrompt(input.Content);
                return new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Prompt updated successfully"
                };

            case HumanInputType.PauseAgent:
                agent.Pause();
                return new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Agent paused"
                };

            case HumanInputType.ResumeAgent:
                agent.Resume();
                return new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Agent resumed"
                };

            case HumanInputType.TriggerBatchLearning:
                // This would trigger the batch learning process
                _logger.LogInformation("Batch learning trigger requested by human");
                return new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Batch learning will be triggered in next iteration"
                };

            case HumanInputType.QueryState:
                var state = _stateManager.GetCurrentState();
                return new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = "Current state retrieved",
                    Data = new Dictionary<string, object>
                    {
                        ["state"] = state
                    }
                };

            case HumanInputType.AddGoal:
                _stateManager.UpdateGoal(input.Content);
                return new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = true,
                    Message = $"Goal updated to: {input.Content}"
                };

            default:
                _logger.LogWarning("Unknown input type: {Type}", input.Type);
                return new HumanInputResponse
                {
                    InputId = input.Id,
                    Success = false,
                    Message = $"Unknown input type: {input.Type}"
                };
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
