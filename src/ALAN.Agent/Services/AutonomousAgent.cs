using ALAN.Shared.Models;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ALAN.Agent.Services;

public class AutonomousAgent
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly ILogger<AutonomousAgent> _logger;
    private readonly StateManager _stateManager;
    private bool _isRunning;
    private string _currentPrompt = "You are an autonomous AI agent. Think about interesting things and take actions to learn and explore.";

    public AutonomousAgent(AIAgent agent, ILogger<AutonomousAgent> logger, StateManager stateManager)
    {
        _agent = agent;
        _thread = agent.GetNewThread();
        _logger = logger;
        _stateManager = stateManager;
    }

    public void UpdatePrompt(string prompt)
    {
        _currentPrompt = prompt;
        _stateManager.UpdatePrompt(prompt);
        _logger.LogInformation("Prompt updated: {Prompt}", prompt);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _isRunning = true;
        _logger.LogInformation("Autonomous agent started");

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await ThinkAndActAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Agent operation cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in agent loop");
                _stateManager.UpdateStatus(AgentStatus.Error);
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }

        _logger.LogInformation("Autonomous agent stopped");
    }

    private async Task ThinkAndActAsync(CancellationToken cancellationToken)
    {
        _stateManager.UpdateStatus(AgentStatus.Thinking);

        // Record observation
        var observation = new AgentThought
        {
            Type = ThoughtType.Observation,
            Content = $"Current time: {DateTime.UtcNow:HH:mm:ss}. Current prompt: {_currentPrompt}"
        };
        _stateManager.AddThought(observation);
        _logger.LogInformation("Agent observed: {Content}", observation.Content);

        // Get AI response
        var prompt = $@"You are an autonomous agent. Your current directive is: {_currentPrompt}

Previous thoughts and actions are stored in your memory.
Think about what you should do next. Be creative and thoughtful.
Respond with a JSON object containing:
- reasoning: your thought process
- action: what you plan to do
- goal: what you're trying to achieve

Example:
{{
  ""reasoning"": ""I should explore new concepts"",
  ""action"": ""Learn about quantum computing"",
  ""goal"": ""Expand my knowledge base""
}}";

        try
        {
            var result = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);
            var response = result.Text ?? result.ToString();

            // Record reasoning
            var reasoning = new AgentThought
            {
                Type = ThoughtType.Reasoning,
                Content = response
            };
            _stateManager.AddThought(reasoning);
            _logger.LogInformation("Agent reasoning: {Content}", response);

            // Parse and execute action
            await ParseAndExecuteActionAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during thinking process");
        }
    }

    private async Task ParseAndExecuteActionAsync(string response, CancellationToken cancellationToken)
    {
        _stateManager.UpdateStatus(AgentStatus.Acting);

        try
        {
            // Try to parse as JSON
            var actionPlan = JsonSerializer.Deserialize<ActionPlan>(response);

            if (actionPlan != null && !string.IsNullOrEmpty(actionPlan.Action))
            {
                var action = new AgentAction
                {
                    Name = "ExecutePlan",
                    Description = actionPlan.Action,
                    Input = actionPlan.Reasoning ?? "",
                    Status = ActionStatus.Running
                };

                _stateManager.AddAction(action);
                _stateManager.UpdateGoal(actionPlan.Goal ?? "General exploration");

                // Simulate action execution
                await Task.Delay(1000, cancellationToken);

                action.Status = ActionStatus.Completed;
                action.Output = $"Completed: {action.Description}";
                _stateManager.UpdateAction(action);

                _logger.LogInformation("Action completed: {Description}", action.Description);
            }
        }
        catch (JsonException)
        {
            // If not valid JSON, just log as a thought
            var thought = new AgentThought
            {
                Type = ThoughtType.Reflection,
                Content = response
            };
            _stateManager.AddThought(thought);
        }

        _stateManager.UpdateStatus(AgentStatus.Idle);
    }

    public void Stop()
    {
        _isRunning = false;
    }
}

public class ActionPlan
{
    public string? Reasoning { get; set; }
    public string? Action { get; set; }
    public string? Goal { get; set; }
}
