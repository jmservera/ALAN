using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
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
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly IShortTermMemoryService _shortTermMemory;
    private readonly BatchLearningService _batchLearningService;
    private readonly HumanInputHandler _humanInputHandler;
    private bool _isRunning;
    private bool _isPaused;
    private string _currentPrompt = "You are an autonomous AI agent. Think about how to improve yourself.";
    private int _consecutiveThrottles = 0;
    private int _iterationCount = 0;

    public AutonomousAgent(
        AIAgent agent,
        ILogger<AutonomousAgent> logger,
        StateManager stateManager,
        UsageTracker usageTracker,
        ILongTermMemoryService longTermMemory,
        IShortTermMemoryService shortTermMemory,
        BatchLearningService batchLearningService,
        HumanInputHandler humanInputHandler)
    {
        _agent = agent;
        _thread = agent.GetNewThread();
        _logger = logger;
        _stateManager = stateManager;
        _usageTracker = usageTracker;
        _longTermMemory = longTermMemory;
        _shortTermMemory = shortTermMemory;
        _batchLearningService = batchLearningService;
        _humanInputHandler = humanInputHandler;
        _humanInputHandler.SetAgent(this);
    }

    public void UpdatePrompt(string prompt)
    {
        _currentPrompt = prompt;
        _stateManager.UpdatePrompt(prompt);
        _logger.LogInformation("Prompt updated: {Prompt}", prompt);
    }

    public void Pause()
    {
        _isPaused = true;
        _stateManager.UpdateStatus(AgentStatus.Paused);
        _logger.LogInformation("Agent paused");
    }

    public void Resume()
    {
        _isPaused = false;
        _stateManager.UpdateStatus(AgentStatus.Idle);
        _logger.LogInformation("Agent resumed");
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
                // Process human inputs first
                await _humanInputHandler.ProcessPendingInputsAsync(this, cancellationToken);

                // Check if paused
                if (_isPaused)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }

                // Check if we should run batch learning
                if (_batchLearningService.ShouldRunBatch(_iterationCount))
                {
                    _logger.LogInformation("Pausing agent loop for batch learning");
                    _stateManager.UpdateStatus(AgentStatus.Paused);
                    await _batchLearningService.RunBatchLearningAsync(cancellationToken);
                    _iterationCount = 0; // Reset iteration counter
                    _logger.LogInformation("Batch learning complete, resuming agent loop");
                }

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
                _iterationCount++;

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

        // Store current time to short-term memory
        await _shortTermMemory.SetAsync("last_think_time", DateTime.UtcNow, TimeSpan.FromHours(1), cancellationToken);

        // Record observation
        var observation = new AgentThought
        {
            Type = ThoughtType.Observation,
            Content = $"Current time: {DateTime.UtcNow:HH:mm:ss}. Current prompt: {_currentPrompt}"
        };
        _stateManager.AddThought(observation);
        _logger.LogInformation("Agent observed: {Content}", observation.Content);

        // Store observation to long-term memory
        var observationMemory = new MemoryEntry
        {
            Type = MemoryType.Observation,
            Content = observation.Content,
            Summary = "Agent observation",
            Importance = 0.3,
            Tags = new List<string> { "observation", "system" }
        };
        await _longTermMemory.StoreMemoryAsync(observationMemory, cancellationToken);

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

            // Store reasoning to long-term memory
            var reasoningMemory = new MemoryEntry
            {
                Type = MemoryType.Decision,
                Content = response,
                Summary = "Agent reasoning and decision",
                Importance = 0.6,
                Tags = new List<string> { "reasoning", "decision" }
            };
            await _longTermMemory.StoreMemoryAsync(reasoningMemory, cancellationToken);

            // Parse and execute action
            await ParseAndExecuteActionAsync(response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during thinking process");
            
            // Store error to long-term memory
            var errorMemory = new MemoryEntry
            {
                Type = MemoryType.Error,
                Content = $"Error during thinking: {ex.Message}",
                Summary = "Agent error",
                Importance = 0.7,
                Tags = new List<string> { "error", "system" }
            };
            await _longTermMemory.StoreMemoryAsync(errorMemory, cancellationToken);
        }
    }

    private async Task ParseAndExecuteActionAsync(string response, CancellationToken cancellationToken)
    {
        _stateManager.UpdateStatus(AgentStatus.Acting);

        try
        {
            // Try to parse as JSON
            var actionPlan = JsonSerializer.Deserialize<ActionPlan>(response, new JsonSerializerOptions 
            { 
                PropertyNameCaseInsensitive = true 
            });

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

                var prompt = $@"You are an autonomous AI agent executing an action based on your previous reasoning.

Current Goal: {actionPlan.Goal ?? "General exploration"}

Reasoning: {actionPlan.Reasoning}

Action to Execute: {actionPlan.Action}

Please execute this action and provide:
1. Steps you took to complete the action
2. Any observations or insights gained
3. Challenges encountered (if any)
4. Next steps or recommendations

Be specific and detailed in your response.";
                // Simulate action execution
                var result=await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

                action.Status = ActionStatus.Completed;
                action.Output = $"Completed: {result.Text}";
                _stateManager.UpdateAction(action);

                _logger.LogInformation("Action completed: {Description}", action.Description);

                // Store successful action to long-term memory
                var actionMemory = new MemoryEntry
                {
                    Type = MemoryType.Success,
                    Content = $"Action: {action.Description}\nResult: {action.Output}",
                    Summary = action.Name,
                    Importance = 0.5,
                    Tags = new List<string> { "action", "success" }
                };
                await _longTermMemory.StoreMemoryAsync(actionMemory, cancellationToken);
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
