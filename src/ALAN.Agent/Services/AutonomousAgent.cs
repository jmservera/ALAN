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
    private readonly UsageTracker _usageTracker;
    private bool _isRunning;
    private string _currentPrompt = "You are an autonomous AI agent. Think about interesting things and take actions to learn and explore.";
    private int _consecutiveThrottles = 0;

    public AutonomousAgent(AIAgent agent, ILogger<AutonomousAgent> logger, StateManager stateManager, UsageTracker usageTracker)
    {
        _agent = agent;
        _thread = agent.GetNewThread();
        _logger = logger;
        _stateManager = stateManager;
        _usageTracker = usageTracker;
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

        // Log initial usage stats
        var stats = _usageTracker.GetTodayStats();
        _logger.LogInformation("Today's usage: {Stats}", stats);

        while (_isRunning && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Check if we can execute another loop
                if (!_usageTracker.CanExecuteLoop(out var reason))
                {
                    _logger.LogWarning("Agent throttled: {Reason}", reason);
                    _stateManager.UpdateStatus(AgentStatus.Throttled);

                    _consecutiveThrottles++;

                    // If throttled multiple times, increase wait time exponentially
                    var waitMinutes = Math.Min(60, Math.Pow(2, _consecutiveThrottles));
                    _logger.LogInformation("Waiting {Minutes} minutes before retry (attempt {Attempt})",
                        waitMinutes, _consecutiveThrottles);

                    await Task.Delay(TimeSpan.FromMinutes(waitMinutes), cancellationToken);
                    continue;
                }

                _consecutiveThrottles = 0;
                await ThinkAndActAsync(cancellationToken);

                // Record the loop execution
                _usageTracker.RecordLoop(estimatedTokens: 2000);

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

        // Log final usage stats
        var finalStats = _usageTracker.GetTodayStats();
        _logger.LogInformation("Agent stopped. Final usage: {Stats}", finalStats);
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
            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var actionPlan = JsonSerializer.Deserialize<ActionPlan>(response, jsonOptions);

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
                _logger.LogInformation("Executing action: {Description}", action.Description);
                // Execute search action using the agent's search tool
                var searchPrompt = $"Search the internet for information about: {action.Description}. Provide a summary of your findings.";
                var searchResult = await _agent.RunAsync(searchPrompt, _thread, cancellationToken: cancellationToken);
                var searchResponse = searchResult.Text ?? searchResult.ToString();

                // Add search results to memory as a thought
                var searchThought = new AgentThought
                {
                    Type = ThoughtType.Observation,
                    Content = $"Search results for '{action.Description}': {searchResponse}"
                };
                _stateManager.AddThought(searchThought);
                _logger.LogInformation("Search results added to memory: {Content}", searchResponse);

                var summaryResult = await _agent.RunAsync($"Based on the search results, summarize your findings regarding: {action.Description}", _thread, cancellationToken: cancellationToken);
                var summaryResponse = summaryResult.Text ?? summaryResult.ToString();
                _stateManager.AddThought(new AgentThought
                {
                    Type = ThoughtType.Reflection,
                    Content = $"Summary of findings for '{action.Description}': {summaryResponse}"
                });

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
