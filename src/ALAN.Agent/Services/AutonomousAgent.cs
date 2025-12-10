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
        await _shortTermMemory.SetAsync("last-think-time", DateTime.UtcNow, TimeSpan.FromHours(1), cancellationToken);

        // Record observation
        var observation = new AgentThought
        {
            Type = ThoughtType.Observation,
            Content = _currentPrompt
        };
        _stateManager.AddThought(observation);
        _logger.LogInformation("Agent observed: {Content}", observation.Content);

        // Get AI response
        var prompt = $@"You are an autonomous agent. Your current directive is: {_currentPrompt}

You have access to the following tools:
- GitHub MCP Server: Query repositories, read files, search code, view commits
- Microsoft Learn MCP Server: Fetch documentation, search learning resources

Previous thoughts and actions are stored in your memory.
Think about what you should do next. Use the available tools when they would be helpful.
For example, you can search GitHub for code examples, read documentation from Microsoft Learn, or analyze repository files.

Respond with a JSON object containing:
- reasoning: your thought process (mention if you plan to use any tools)
- actions: the specific action(s) you will take as an array of:
    - action: the action to perform
    - goal: what you're trying to achieve
    - extra (optional): any additional information

Example:
{{
  ""reasoning"": ""I should search GitHub for examples of autonomous agents to learn from them"",
  ""actions"": [{{
    ""action"": ""Search GitHub repositories for autonomous agent implementations"",
    ""goal"": ""Learn from existing autonomous agent projects"",
    ""extra"": ""I found several repositories that look promising:\n1. **Architecture**: abc from https://abc.com .\n2. **Semantic Kernel Integration**: The project incorporates Semantic Kernel, enhancing its AI capabilities. Ensuring a robust integration with Semantic Kernel can allow for more advanced reasoning and task handling in my implementation.""
  }}]
}}
";

        try
        {
            var result = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);
            var response = result.Text ?? result.ToString();

            // Extract tool call information (minimal metadata)
            var toolCalls = ExtractToolCalls(result);
            if (toolCalls != null && toolCalls.Count > 0)
            {
                _logger.LogInformation("Action used {Count} tool(s): {Tools}",
                    toolCalls.Count,
                    string.Join(", ", toolCalls.Select(t => t.ToolName)));
            }
            else
            {
                _logger.LogInformation("No tool calls detected in action execution");
            }
            // Record reasoning
            var reasoning = new AgentThought
            {
                Type = ThoughtType.Reasoning,
                Content = response,
                ToolCalls = toolCalls
            };
            _stateManager.AddThought(reasoning);
            _logger.LogTrace("Agent reasoning: {Content}", response);

            // Parse and execute action
            await ParseAndExecuteActionAsync(response, cancellationToken);
        }
        catch (System.ClientModel.ClientResultException clientEx)
        {
            _logger.LogError(clientEx, "Azure OpenAI client error during thinking process. Status: {Status}",
                clientEx.Status);

            // Store API errors for learning
            var errorMemory = new MemoryEntry
            {
                Type = MemoryType.Error,
                Content = $"Azure OpenAI API error (Status {clientEx.Status}): {clientEx.Message}",
                Summary = "API communication error",
                Importance = 0.6,
                Tags = new List<string> { "error", "api", "azure-openai" }
            };
            await _longTermMemory.StoreMemoryAsync(errorMemory, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during thinking process");

            // Store critical errors directly to long-term memory for persistence
            // (errors are important enough to skip short-term storage)
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
            // Try to extract JSON from the response (it might have additional text)
            var jsonMatch = System.Text.RegularExpressions.Regex.Match(response, @"\{.*\}",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            var jsonString = jsonMatch.Success ? jsonMatch.Value : response;

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            };

            var singlePlan = JsonSerializer.Deserialize<ActionPlan>(jsonString, options);


            await Parallel.ForEachAsync(singlePlan.Actions ?? [], async (actionPlan, cancellationToken) =>
            {
                if (!string.IsNullOrEmpty(actionPlan.Action))
                {
                    var action = new AgentAction
                    {
                        Name = "ExecutePlan",
                        Description = actionPlan.Action,
                        Input = $"{singlePlan.Reasoning} : {actionPlan.Goal} \n {actionPlan.Extra}",
                        Status = ActionStatus.Running
                    };

                    _stateManager.AddAction(action);
                    try
                    {
                        _stateManager.UpdateGoal(actionPlan.Goal ?? "General exploration");

                        var prompt = $@"You are an autonomous AI agent executing an action based on your previous reasoning.

Current Goal: {actionPlan.Goal ?? "General exploration"}

Reasoning: {singlePlan.Reasoning}

Action to Execute: {actionPlan.Action}

{actionPlan.Extra}

You have access to the following tools:
- GitHub MCP Server: Search repositories, read code files, view commits, search code
- Microsoft Learn MCP Server: Fetch documentation and learning resources

Please execute this action using the available tools when appropriate. For example:
- If learning about a topic, use Microsoft Learn to fetch relevant documentation
- If analyzing code or repositories, use GitHub to search and read files
- If researching patterns or examples, search GitHub for relevant projects

Provide:
1. Steps you took to complete the action (mention any tools used)
2. Any observations or insights gained
3. Challenges encountered (if any)
4. Next steps or recommendations

Be specific about which tools you use and what you discover.";
                        // Simulate action execution
                        var result = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);

                        // Extract tool calls from action execution
                        var toolCalls = ExtractToolCalls(result);
                        if (toolCalls != null && toolCalls.Count > 0)
                        {
                            _logger.LogInformation("Action used {Count} tool(s): {Tools}",
                                toolCalls.Count,
                                string.Join(", ", toolCalls.Select(t => t.ToolName)));
                        }
                        else
                        {
                            _logger.LogInformation("No tool calls detected in action execution");
                        }

                        action.Status = ActionStatus.Completed;
                        action.Output = $"Completed: {result.Text}";
                        action.ToolCalls = toolCalls;
                        _stateManager.UpdateAction(action);

                        _logger.LogInformation("Action completed: {Description}", action.Description);
                    }
                    catch (Exception ex)
                    {
                        action.Status = ActionStatus.Failed;
                        action.Output = $"Error: {ex.Message}";
                        _stateManager.UpdateAction(action);

                        _logger.LogError(ex, "Error executing action: {Description}", action.Description);
                    }
                }
            });
        }
        catch (JsonException jsonEx)
        {
            // If not valid JSON, just log as a thought
            _logger.LogWarning(jsonEx, "Failed to parse action response as JSON. Response: {Response}",
                response.Length > 200 ? response.Substring(0, 200) + "..." : response);

            _logger.LogTrace("Full response: {Response}", response);

            var thought = new AgentThought
            {
                Type = ThoughtType.Reflection,
                Content = response
            };
            _stateManager.AddThought(thought);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing and executing action");
        }

        _stateManager.UpdateStatus(AgentStatus.Idle);
    }

    private List<ToolCall>? ExtractToolCalls(AgentRunResponse result)
    {
        try
        {
            var toolCalls = new Dictionary<string, ToolCall>();

            // Access Messages property from AgentRunResponse
            if (result.Messages != null)
            {
                foreach (var message in result.Messages)
                {
                    if (message.Contents != null)
                    {
                        foreach (var content in message.Contents)
                        {
                            // Check if this is a function call
                            if (content is FunctionCallContent functionCall)
                            {
                                _logger.LogTrace("Found function call: {FunctionName}", functionCall.Name);

                                var toolCall = new ToolCall
                                {
                                    ToolName = functionCall.Name ?? "unknown",
                                    Arguments = functionCall.Arguments != null
                                        ? System.Text.Json.JsonSerializer.Serialize(functionCall.Arguments)
                                        : null
                                };

                                toolCall.McpServer = DetermineMcpServer(toolCall.ToolName);
                                toolCall.Success = true; // Will be updated if we find a matching result

                                toolCalls[functionCall.CallId] = toolCall;
                                _logger.LogInformation("Extracted tool call: {ToolName} from {McpServer} with CallId: {CallId}",
                                    toolCall.ToolName, toolCall.McpServer ?? "unknown", functionCall.CallId);
                            }
                            else if (content is FunctionResultContent functionResult)
                            {
                                _logger.LogTrace("Found function result for CallId: {CallId}", functionResult.CallId);

                                if (toolCalls.ContainsKey(functionResult.CallId))
                                {
                                    toolCalls[functionResult.CallId].Result = functionResult.Result != null
                                        ? System.Text.Json.JsonSerializer.Serialize(functionResult.Result)
                                        : null;

                                    // Check if result indicates an error
                                    var resultType = functionResult.Result?.GetType();
                                    if (resultType?.GetProperty("isError")?.GetValue(functionResult.Result) is bool errorFlag)
                                    {
                                        toolCalls[functionResult.CallId].Success = !errorFlag;
                                    }

                                    _logger.LogInformation("Extracted result for CallId: {CallId}, Success: {Success}",
                                        functionResult.CallId, toolCalls[functionResult.CallId].Success);
                                }
                            }
                        }
                    }
                }
            }

            var toolCallList = toolCalls.Values.ToList();

            if (toolCallList.Count > 0)
            {
                _logger.LogInformation("✓ Extracted {Count} tool calls from agent result", toolCallList.Count);
            }
            else
            {
                _logger.LogDebug("No tool calls found in agent result");
            }

            return toolCallList.Count > 0 ? toolCallList : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to extract tool calls from result");
            return null;
        }
    }

    private string? DetermineMcpServer(string toolName)
    {
        // Match tool names to MCP servers based on naming conventions
        var lowerName = toolName.ToLowerInvariant();

        if (lowerName.Contains("github")) return "github";
        if (lowerName.Contains("fetch") || lowerName.Contains("learn")) return "microsoft-learn";

        return null;
    }

    public void Stop()
    {
        _isRunning = false;
    }
}

public struct ActionPlan
{
    public string? Reasoning { get; set; }

    public List<PlannedAction>? Actions { get; set; }
}

public struct PlannedAction
{
    public string Action { get; set; }

    public string? Goal { get; set; }

    public string? Extra { get; set; }
}
