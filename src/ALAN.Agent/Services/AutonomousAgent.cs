using ALAN.Shared.Models;
using ALAN.Shared.Services;
using ALAN.Shared.Services.Memory;
using ALAN.Shared.Services.Resilience;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using Polly;
using System.Text.Json;

namespace ALAN.Agent.Services;

public class AutonomousAgent
{
    // Memory configuration constants
    private const double IMPORTANCE_WEIGHT = 0.7;
    private const double RECENCY_WEIGHT = 0.3;
    private const int RECENCY_CUTOFF_DAYS = 7;
    private const int MAX_MEMORY_CONTEXT_SIZE = 20;
    private const int MEMORY_REFRESH_INTERVAL_ITERATIONS = 10;
    private const int MEMORY_REFRESH_INTERVAL_HOURS = 1;
    private const double HIGH_IMPORTANCE_THRESHOLD = 0.8;
    private const int CONTENT_PREVIEW_MAX_LENGTH = 200;

    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly ILogger<AutonomousAgent> _logger;
    private readonly StateManager _stateManager;
    private readonly UsageTracker _usageTracker;
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly IShortTermMemoryService _shortTermMemory;
    private readonly BatchLearningService _batchLearningService;
    private readonly HumanInputHandler _humanInputHandler;
    private readonly IPromptService _promptService;
    private readonly ResiliencePipeline _resiliencePipeline;
    private readonly MemoryAgent? _memoryAgent; // Optional: only available when Azure AI Search is configured
    private bool _isRunning;
    private bool _isPaused;
    private const string DEFAULT_DIRECTIVE = "Think about how to improve yourself.";
    private string _receivedDirective = DEFAULT_DIRECTIVE;
    private string _currentDirective = DEFAULT_DIRECTIVE;
    private int _consecutiveThrottles = 0;
    private int _iterationCount = 0;
    private volatile List<MemoryEntry> _recentMemories = new();
    private DateTime _lastMemoryLoad = DateTime.MinValue;
    private int _currentMemoryContextSize = MAX_MEMORY_CONTEXT_SIZE;

    public AutonomousAgent(
        AIAgent agent,
        ILogger<AutonomousAgent> logger,
        StateManager stateManager,
        UsageTracker usageTracker,
        ILongTermMemoryService longTermMemory,
        IShortTermMemoryService shortTermMemory,
        BatchLearningService batchLearningService,
        HumanInputHandler humanInputHandler,
        IPromptService promptService,
        MemoryAgent? memoryAgent = null) // Optional: only available when Azure AI Search is configured
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
        _promptService = promptService;
        _memoryAgent = memoryAgent;
        _resiliencePipeline = ResiliencePolicy.CreateOpenAIRetryPipeline(logger);
        _humanInputHandler.SetAgent(this);

        if (_memoryAgent != null)
        {
            _logger.LogInformation("MemoryAgent is available - vector search enabled");
        }
        else
        {
            _logger.LogInformation("MemoryAgent not available - using traditional memory retrieval");
        }
    }

    public void UpdatePrompt(string prompt)
    {
        _receivedDirective = prompt;
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

        // Load initial memories from long-term storage
        await LoadRecentMemoriesAsync(cancellationToken);

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
                    await PauseAndRunBatchLearningAsync(cancellationToken);
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

                // Refresh memories periodically before thinking (every N iterations or M hours)
                // Skip refresh on iteration 0 since memories were just loaded at startup
                if ((_iterationCount > 0 && _iterationCount % MEMORY_REFRESH_INTERVAL_ITERATIONS == 0) ||
                    (DateTime.UtcNow - _lastMemoryLoad).TotalHours >= MEMORY_REFRESH_INTERVAL_HOURS)
                {
                    await LoadRecentMemoriesAsync(cancellationToken);
                }

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

    public async Task PauseAndRunBatchLearningAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Pausing agent loop for batch learning");
        _stateManager.UpdateStatus(AgentStatus.Paused);
        //TODO: wait until current iteration is complete
        await _batchLearningService.RunBatchLearningAsync(cancellationToken);
        _iterationCount = 0; // Reset iteration counter
        _logger.LogInformation("Batch learning complete, resuming agent loop");
    }

    /// <summary>
    /// Loads recent memories and learnings from long-term storage to provide context for decision-making.
    /// This ensures the agent builds on previous knowledge rather than starting from scratch each iteration.
    /// Uses vector search when available for more relevant memory retrieval.
    /// </summary>
    internal async Task LoadRecentMemoriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Loading recent memories from long-term storage...");

            // Use vector search if available and we have a current directive
            if (_memoryAgent != null && !string.IsNullOrEmpty(_currentDirective))
            {
                await LoadMemoriesWithVectorSearchAsync(cancellationToken);
            }
            else
            {
                await LoadMemoriesTraditionalAsync(cancellationToken);
            }

            _lastMemoryLoad = DateTime.UtcNow;

            // Log actual counts from the final _recentMemories list
            var learningsInContext = _recentMemories.Count(m => m.Type == MemoryType.Learning);
            var successesInContext = _recentMemories.Count(m => m.Type == MemoryType.Success);
            var reflectionsInContext = _recentMemories.Count(m => m.Type == MemoryType.Reflection);
            var decisionsInContext = _recentMemories.Count(m => m.Type == MemoryType.Decision);
            _logger.LogInformation("Loaded {Count} memories: {Learnings} learnings, {Successes} successes, {Reflections} reflections, {Decisions} decisions",
                _recentMemories.Count, learningsInContext, successesInContext, reflectionsInContext, decisionsInContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load recent memories, continuing with empty context");
            _recentMemories = [];
        }
    }

    /// <summary>
    /// Loads memories using vector search for semantic relevance to current directive,
    /// combined with recent short-term actions for immediate context continuity.
    /// </summary>
    private async Task LoadMemoriesWithVectorSearchAsync(CancellationToken cancellationToken)
    {
        if (_memoryAgent == null) return;

        _logger.LogInformation("Using vector search to find relevant memories for directive: {Directive}", _currentDirective);

        // Reserve space for recent short-term actions (top 5 most recent)
        const int shortTermReserve = 5;
        int longTermMaxResults = Math.Max(1, MAX_MEMORY_CONTEXT_SIZE - shortTermReserve);

        // Search for semantically relevant long-term memories
        var vectorResults = await _memoryAgent.SearchRelevantMemoriesAsync(
            _currentDirective,
            maxResults: longTermMaxResults,
            cancellationToken: cancellationToken);

        // Check for similar completed tasks to avoid duplication
        var similarTask = await _memoryAgent.FindSimilarCompletedTaskAsync(
            _currentDirective,
            cancellationToken: cancellationToken);

        // Build long-term memory context from vector search
        List<MemoryEntry> longTermMemories;
        if (similarTask != null)
        {
            _logger.LogInformation("Found similar completed task (score: {Score}): {Summary}",
                similarTask.Score, similarTask.Memory.Summary);

            // Add the similar task at the top of the context
            longTermMemories = [similarTask.Memory];
            longTermMemories.AddRange(vectorResults
                .Where(r => r.Memory.Id != similarTask.Memory.Id)
                .Select(r => r.Memory)
                .Take(longTermMaxResults - 1));
        }
        else
        {
            longTermMemories = vectorResults.Select(r => r.Memory).ToList();
        }

        // Load recent short-term actions for immediate context
        var actionKeys = await _shortTermMemory.GetKeysAsync("action:*", cancellationToken);
        var actionTasks = actionKeys.Select(key => _shortTermMemory.GetAsync<AgentAction>(key, cancellationToken));
        var allActions = await Task.WhenAll(actionTasks);
        var shortTermActions = allActions
            .Where(a => a != null)
            .Select(a => a!)
            .OrderByDescending(a => a.Timestamp)
            .Take(10) // Get more than needed to filter completed ones
            .ToList();

        // Convert recent successful actions to memory entries
        var recentActionMemories = shortTermActions
            .Where(a => a.Status == ActionStatus.Completed)
            .Take(shortTermReserve)
            .Select(a => new MemoryEntry
            {
                Type = MemoryType.Success,
                Content = a.Output ?? a.Input,
                Summary = a.Output ?? a.Description ?? string.Empty,
                Timestamp = a.Timestamp,
                Importance = 0.5,
                Tags = ["short-term", "action", "recent"]
            })
            .ToList();

        // Combine: recent short-term actions first (for immediate context), 
        // then semantically relevant long-term memories
        if (recentActionMemories.Count != 0)
        {
            _recentMemories = [.. recentActionMemories, .. longTermMemories];
            _logger.LogInformation("Combined {ShortTerm} recent actions with {LongTerm} relevant long-term memories",
                recentActionMemories.Count, longTermMemories.Count);
        }
        else
        {
            _recentMemories = longTermMemories;
            _logger.LogInformation("No recent short-term actions, using {Count} relevant long-term memories",
                longTermMemories.Count);
        }

        // Evaluate and log search quality for long-term memories
        var evaluation = await _memoryAgent.EvaluateSearchQualityAsync(
            _currentDirective,
            vectorResults,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Vector search quality: {Precision:P2} precision, avg score {AvgScore:F3}",
            evaluation.Precision, evaluation.AverageScore);
    }

    /// <summary>
    /// Traditional memory loading using type-based queries and importance/recency scoring.
    /// Used as fallback when vector search is not available.
    /// </summary>
    private async Task LoadMemoriesTraditionalAsync(CancellationToken cancellationToken)
    {
        // Optimize memory loading by fetching in parallel and limiting results
        var learningsTask = _longTermMemory.GetMemoriesByTypeAsync(MemoryType.Learning, maxResults: 10, cancellationToken);
        var successesTask = _longTermMemory.GetMemoriesByTypeAsync(MemoryType.Success, maxResults: 10, cancellationToken);
        var reflectionsTask = _longTermMemory.GetMemoriesByTypeAsync(MemoryType.Reflection, maxResults: 5, cancellationToken);
        var decisionsTask = _longTermMemory.GetMemoriesByTypeAsync(MemoryType.Decision, maxResults: 10, cancellationToken);
        var actionKeysTask = _shortTermMemory.GetKeysAsync("action:*", cancellationToken);

        // Wait for all tasks to complete in parallel
        await Task.WhenAll(learningsTask, successesTask, reflectionsTask, decisionsTask, actionKeysTask);

        // Combine and sort by importance and recency with gradual decay
        // Capacity = 10 (learnings) + 10 (successes) + 5 (reflections) + 10 (decisions) = 35
        var allMemories = new List<MemoryEntry>(35);
        allMemories.AddRange(learningsTask.Result);
        allMemories.AddRange(successesTask.Result);
        allMemories.AddRange(reflectionsTask.Result);
        allMemories.AddRange(decisionsTask.Result);

        // Sort and take top memories in a single pass
        _recentMemories = allMemories
            .OrderByDescending(m =>
                m.Importance * IMPORTANCE_WEIGHT +
                Math.Max(0, 1.0 - (DateTime.UtcNow - m.Timestamp).TotalDays / RECENCY_CUTOFF_DAYS) * RECENCY_WEIGHT
            )
            .Take(MAX_MEMORY_CONTEXT_SIZE)
            .ToList();

        // Load recent short-term actions in parallel (limit to 10 most recent)
        var actionKeys = actionKeysTask.Result;
        // Fetch all actions for the keys, then sort by Timestamp descending and take the 10 most recent
        var actionTasks = actionKeys.Select(key => _shortTermMemory.GetAsync<AgentAction>(key, cancellationToken));
        var allActions = await Task.WhenAll(actionTasks);
        var shortTermActions = allActions
            .Where(a => a != null)
            .Select(a => a!)
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .ToList();

        // Convert recent successful actions to memory entries efficiently
        if (shortTermActions != null && shortTermActions.Count != 0)
        {
            var recentActionMemories = shortTermActions
                .Where(a => a.Status == ActionStatus.Completed)
                .OrderByDescending(a => a.Timestamp)
                .Take(5)
                .Select(a => new MemoryEntry
                {
                    Type = MemoryType.Success,
                    Content = a.Output ?? a.Input,
                    Summary = a.Output ?? a.Description ?? string.Empty,
                    Timestamp = a.Timestamp,
                    Importance = 0.5, // Lower importance for actions (will be consolidated later)
                    Tags = ["short-term", "action", "recent"]
                })
                .ToList();

            // Prepend short-term memories and limit to MAX_MEMORY_CONTEXT_SIZE
            if (recentActionMemories.Count != 0)
            {
                _recentMemories = [.. recentActionMemories
                        .Concat(_recentMemories)
                        .Take(MAX_MEMORY_CONTEXT_SIZE)];
            }
        }
    }

    private async Task ThinkAndActAsync(CancellationToken cancellationToken)
    {
        _stateManager.UpdateStatus(AgentStatus.Thinking);

        // Store current time to short-term memory
        await _shortTermMemory.SetAsync("last-think-time", DateTime.UtcNow, TimeSpan.FromHours(1), cancellationToken);

        if (_currentDirective != _receivedDirective)
        {
            _logger.LogInformation("New directive received: {Directive}", _receivedDirective);
            _currentDirective = _receivedDirective;

            // Record observation
            var observation = new AgentThought
            {
                Type = ThoughtType.Observation,
                Content = _receivedDirective
            };
            _stateManager.AddThought(observation);
            _logger.LogInformation("Agent observed: {Content}", observation.Content);
        }

        // Get AI response - retry with reduced context if needed
        const int minMemoryContextSize = 5;
        int retryAttempt = 0;
        const int maxRetries = 3;

        while (retryAttempt <= maxRetries)
        {
            var prompt = _promptService.RenderTemplate("agent-thinking", new
            {
                directive = _receivedDirective,
                memoryCount = _recentMemories.Count,
                memoryContext = BuildMemoryContext(_currentMemoryContextSize)
            });
            _logger.LogTrace("Agent prompt: {Prompt}", prompt.Length > 500 ? string.Concat(prompt.AsSpan(0, 500), "...") : prompt);

            try
            {
                var result = await _resiliencePipeline.ExecuteAsync(async ct =>
                    await _agent.RunAsync(prompt, _thread, cancellationToken: ct),
                    cancellationToken);
                var response = result.Text ?? result.ToString();

                // Success - restore full context size for next iteration
                if (_currentMemoryContextSize < MAX_MEMORY_CONTEXT_SIZE)
                {
                    _logger.LogInformation("Context length issue resolved. Restoring memory context to {Size}", MAX_MEMORY_CONTEXT_SIZE);
                    _currentMemoryContextSize = MAX_MEMORY_CONTEXT_SIZE;
                }

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
                break; // Success - exit retry loop
            }
            catch (System.ClientModel.ClientResultException clientEx) when (clientEx.Status == 400 && clientEx.Message.Contains("context_length_exceeded"))
            {
                retryAttempt++;

                if (_currentMemoryContextSize > minMemoryContextSize && retryAttempt <= maxRetries)
                {
                    // Reduce memory context size by half
                    var newSize = Math.Max(minMemoryContextSize, _currentMemoryContextSize / 2);
                    _logger.LogWarning("Context length exceeded ({CurrentSize} memories). Reducing to {NewSize} and retrying (attempt {Attempt}/{MaxRetries})",
                        _currentMemoryContextSize, newSize, retryAttempt, maxRetries);
                    _currentMemoryContextSize = newSize;
                    continue; // Retry with reduced context
                }

                _logger.LogError(clientEx, "Context length exceeded even with minimal memory context ({Size}). Status: {Status}",
                    _currentMemoryContextSize, clientEx.Status);

                break; // Cannot retry further
            }
            catch (System.ClientModel.ClientResultException clientEx)
            {
                _logger.LogError(clientEx, "Azure OpenAI client error during thinking process. Status: {Status}",
                    clientEx.Status);

                break; // Don't retry for other errors
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during thinking process");

                break; // Don't retry on general errors
            }
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

                        var prompt = _promptService.RenderTemplate("action-execution", new
                        {
                            goal = actionPlan.Goal ?? "General exploration",
                            reasoning = singlePlan.Reasoning,
                            action = actionPlan.Action,
                            extra = actionPlan.Extra
                        });
                        // Simulate action execution
                        var result = await _resiliencePipeline.ExecuteAsync(async ct =>
                            await _agent.RunAsync(prompt, _thread, cancellationToken: ct),
                            cancellationToken);

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

                                if (toolCalls.TryGetValue(functionResult.CallId, out var toolCall))
                                {
                                    toolCall.Result = functionResult.Result != null
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

    /// <summary>
    /// Builds a formatted string containing relevant memory context for the current iteration.
    /// This provides the agent with accumulated knowledge from previous iterations.
    /// </summary>
    internal string BuildMemoryContext(int maxMemories = MAX_MEMORY_CONTEXT_SIZE)
    {
        if (!_recentMemories.Any())
        {
            return "No previous memories available yet. This may be your first iteration.";
        }

        var context = new System.Text.StringBuilder();

        // Limit the total number of memories if context size needs to be reduced
        var memoriesToUse = _recentMemories.Take(maxMemories).ToList();

        // Group memories by type for better organization
        var groupedMemories = memoriesToUse.GroupBy(m => m.Type).OrderByDescending(g => g.Key switch
        {
            MemoryType.Learning => 5,
            MemoryType.Reflection => 4,
            MemoryType.Success => 3,
            MemoryType.Decision => 2,
            _ => 1
        });

        foreach (var group in groupedMemories)
        {
            var entryWord = group.Count() == 1 ? "entry" : "entries";
            context.AppendLine($"\n### {group.Key} ({group.Count()} {entryWord}):");

            foreach (var memory in group.OrderByDescending(m => m.Importance).Take(5))
            {
                var age = (DateTime.UtcNow - memory.Timestamp).TotalHours;
                var ageStr = age < 24 ? $"{age:F0}h ago" : $"{age / 24:F0}d ago";

                context.AppendLine($"- [{ageStr}, importance: {memory.Importance:F2}] {memory.Summary}");

                // Include full content for high-importance items
                if (memory.Importance >= HIGH_IMPORTANCE_THRESHOLD && !string.IsNullOrEmpty(memory.Content) && memory.Content != memory.Summary)
                {
                    var contentPreview = memory.Content.Length > CONTENT_PREVIEW_MAX_LENGTH
                        ? memory.Content.Substring(0, CONTENT_PREVIEW_MAX_LENGTH) + "..."
                        : memory.Content;
                    context.AppendLine($"  Details: {contentPreview}");
                }
            }
        }

        return context.ToString();
    }

    public void Stop()
    {
        _isRunning = false;
    }

    // Internal methods for testing purposes
    internal void SetRecentMemoriesForTesting(List<MemoryEntry> memories)
    {
        _recentMemories = memories;
    }

    internal List<MemoryEntry> GetRecentMemoriesForTesting()
    {
        return _recentMemories;
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
