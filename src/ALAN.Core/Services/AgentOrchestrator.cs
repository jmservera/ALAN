using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ALAN.Core.GitHub;
using ALAN.Core.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ALAN.Core.Services;

/// <summary>
/// Orchestrates agent tasks including reasoning, planning, and execution
/// </summary>
public class AgentOrchestrator
{
    private readonly Kernel _kernel;
    private readonly IMemoryService _shortTermMemory;
    private readonly IMemoryService _longTermMemory;
    private readonly GitHubService? _gitHubService;
    private readonly ILogger<AgentOrchestrator> _logger;
    private readonly Queue<string> _humanInputs = new();
    private readonly SemaphoreSlim _inputLock = new(1, 1);

    public AgentOrchestrator(
        Kernel kernel,
        IMemoryService shortTermMemory,
        IMemoryService longTermMemory,
        ILogger<AgentOrchestrator> logger,
        GitHubService? gitHubService = null)
    {
        _kernel = kernel;
        _shortTermMemory = shortTermMemory;
        _longTermMemory = longTermMemory;
        _gitHubService = gitHubService;
        _logger = logger;
    }

    /// <summary>
    /// Add human input to influence the next iteration
    /// </summary>
    public async Task AddHumanInputAsync(string input)
    {
        await _inputLock.WaitAsync();
        try
        {
            _humanInputs.Enqueue(input);
            _logger.LogInformation("Human input queued: {Input}", input);

            // Store in memory
            await _longTermMemory.StoreAsync(new MemoryEntry
            {
                Type = MemoryType.LongTerm,
                Content = $"Human input: {input}",
                Metadata = new() { { "Source", "HumanSteering" } }
            });
        }
        finally
        {
            _inputLock.Release();
        }
    }

    /// <summary>
    /// Execute the main agent task for one iteration
    /// </summary>
    public async Task ExecuteTaskAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Executing agent task");

            // Check for human input
            string? humanInput = null;
            await _inputLock.WaitAsync(cancellationToken);
            try
            {
                if (_humanInputs.TryDequeue(out var input))
                {
                    humanInput = input;
                    _logger.LogInformation("Processing human input: {Input}", humanInput);
                }
            }
            finally
            {
                _inputLock.Release();
            }

            // Retrieve recent context from short-term memory
            var recentMemories = await _shortTermMemory.RetrieveAsync(MemoryType.ShortTerm, limit: 10, cancellationToken);
            var learnings = await _longTermMemory.RetrieveAsync(MemoryType.Learning, limit: 5, cancellationToken);

            // Build context
            var context = BuildContext(recentMemories, learnings, humanInput);

            // Plan and reason about next action
            var action = await PlanNextActionAsync(context, cancellationToken);

            // Execute the action
            await ExecuteActionAsync(action, cancellationToken);

            // Log the action
            await _longTermMemory.StoreAsync(new MemoryEntry
            {
                Type = MemoryType.LongTerm,
                Content = $"Action: {action.Type} - {action.Description}",
                Metadata = new() 
                { 
                    { "ActionType", action.Type.ToString() },
                    { "Success", action.WasExecuted.ToString() }
                }
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing agent task");
            throw;
        }
    }

    private string BuildContext(IEnumerable<MemoryEntry> recentMemories, IEnumerable<MemoryEntry> learnings, string? humanInput)
    {
        var contextParts = new List<string>();

        if (humanInput != null)
        {
            contextParts.Add($"**Human Guidance:** {humanInput}\n");
        }

        if (learnings.Any())
        {
            contextParts.Add("**Recent Learnings:**");
            contextParts.AddRange(learnings.Select(l => $"- {l.Content}"));
            contextParts.Add("");
        }

        if (recentMemories.Any())
        {
            contextParts.Add("**Recent Activity:**");
            contextParts.AddRange(recentMemories.Take(5).Select(m => $"- {m.Content}"));
        }

        return string.Join("\n", contextParts);
    }

    private async Task<AgentAction> PlanNextActionAsync(string context, CancellationToken cancellationToken)
    {
        try
        {
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();

            var prompt = $@"You are ALAN (Autonomous Learning Agent Network), an AI system designed to improve over time.

Context:
{context}

Based on this context, decide on the next action to take. Your options are:
1. ANALYZE_CODE - Analyze repository code for potential improvements
2. MONITOR - Monitor system health and performance
3. REFLECT - Reflect on recent activities and learnings
4. IDLE - Take no action this iteration

Respond with only the action name and a brief reason (one line).
Format: ACTION_NAME: reason";

            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("You are a helpful AI assistant that helps plan actions for an autonomous agent.");
            chatHistory.AddUserMessage(prompt);

            var response = await chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            var content = response.Content ?? "IDLE: No action planned";

            // Parse response
            var parts = content.Split(':', 2);
            var actionTypeStr = parts[0].Trim();
            var description = parts.Length > 1 ? parts[1].Trim() : "No description";

            if (Enum.TryParse<ActionType>(actionTypeStr, true, out var actionType))
            {
                return new AgentAction
                {
                    Type = actionType,
                    Description = description
                };
            }

            return new AgentAction
            {
                Type = ActionType.IDLE,
                Description = "Unable to parse action"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error planning next action");
            return new AgentAction
            {
                Type = ActionType.IDLE,
                Description = $"Error: {ex.Message}"
            };
        }
    }

    private async Task ExecuteActionAsync(AgentAction action, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Executing action: {Type} - {Description}", action.Type, action.Description);

        try
        {
            switch (action.Type)
            {
                case ActionType.ANALYZE_CODE:
                    await AnalyzeCodeAsync(cancellationToken);
                    break;

                case ActionType.MONITOR:
                    await MonitorSystemAsync(cancellationToken);
                    break;

                case ActionType.REFLECT:
                    await ReflectOnActivitiesAsync(cancellationToken);
                    break;

                case ActionType.IDLE:
                default:
                    _logger.LogDebug("Idle iteration");
                    break;
            }

            action.WasExecuted = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing action {Type}", action.Type);
            action.WasExecuted = false;
        }
    }

    private async Task AnalyzeCodeAsync(CancellationToken cancellationToken)
    {
        if (_gitHubService == null)
        {
            _logger.LogWarning("GitHub service not configured, skipping code analysis");
            return;
        }

        _logger.LogInformation("Analyzing repository code");

        // Get repository contents
        var contents = await _gitHubService.GetRepositoryContentsAsync("", cancellationToken);
        
        // Log findings
        await _shortTermMemory.StoreAsync(new MemoryEntry
        {
            Type = MemoryType.ShortTerm,
            Content = $"Analyzed {contents.Count()} files in repository",
            Metadata = new() { { "Action", "CodeAnalysis" } }
        }, cancellationToken);
    }

    private async Task MonitorSystemAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monitoring system health");

        var status = new
        {
            Timestamp = DateTime.UtcNow,
            MemoryUsage = GC.GetTotalMemory(false) / 1024 / 1024, // MB
            Status = "Healthy"
        };

        await _shortTermMemory.StoreAsync(new MemoryEntry
        {
            Type = MemoryType.ShortTerm,
            Content = $"System health: {status.Status}, Memory: {status.MemoryUsage}MB",
            Metadata = new() { { "Action", "SystemMonitor" } }
        }, cancellationToken);
    }

    private async Task ReflectOnActivitiesAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Reflecting on recent activities");

        var recentMemories = await _shortTermMemory.RetrieveAsync(MemoryType.ShortTerm, limit: 20, cancellationToken);
        var memoryCount = recentMemories.Count();

        await _shortTermMemory.StoreAsync(new MemoryEntry
        {
            Type = MemoryType.ShortTerm,
            Content = $"Reflection: Processed {memoryCount} recent activities",
            Metadata = new() { { "Action", "Reflection" } }
        }, cancellationToken);
    }
}

/// <summary>
/// Represents an action the agent can take
/// </summary>
public class AgentAction
{
    public ActionType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool WasExecuted { get; set; }
}

/// <summary>
/// Types of actions the agent can perform
/// </summary>
public enum ActionType
{
    IDLE,
    ANALYZE_CODE,
    MONITOR,
    REFLECT
}
