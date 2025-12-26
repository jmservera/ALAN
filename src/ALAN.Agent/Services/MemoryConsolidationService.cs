using ALAN.Shared.Models;
using ALAN.Shared.Services;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ALAN.Agent.Services;

/// <summary>
/// Service for consolidating memories and extracting learnings.
/// Uses AI to analyze patterns and create higher-level insights.
/// </summary>
public class MemoryConsolidationService : IMemoryConsolidationService
{
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly IShortTermMemoryService _shortTermMemory;
    private readonly StateManager _stateManager;
    private readonly AIAgent _agent;
    private readonly ILogger<MemoryConsolidationService> _logger;
    private readonly IPromptService _promptService;
    private readonly List<ConsolidatedLearning> _learnings = [];

    public MemoryConsolidationService(
        ILongTermMemoryService longTermMemory,
        IShortTermMemoryService shortTermMemory,
        StateManager stateManager,
        AIAgent agent,
        ILogger<MemoryConsolidationService> logger,
        IPromptService promptService)
    {
        _longTermMemory = longTermMemory;
        _shortTermMemory = shortTermMemory;
        _stateManager = stateManager;
        _agent = agent;
        _logger = logger;
        _promptService = promptService;
    }

    public async Task<ConsolidatedLearning> ConsolidateMemoriesAsync(List<MemoryEntry> memories, CancellationToken cancellationToken = default)
    {
        if (!memories.Any())
        {
            throw new ArgumentException("No memories to consolidate", nameof(memories));
        }

        _logger.LogInformation("Consolidating {Count} memories", memories.Count);

        // Prepare memory summaries for AI analysis
        var memorySummaries = memories.Select(m => new
        {
            m.Type,
            m.Summary,
            m.Content,
            m.Timestamp,
            m.Tags
        }).ToList();

        var prompt = _promptService.RenderTemplate("memory-consolidation", new
        {
            memoryCount = memories.Count,
            memoriesJson = JsonSerializer.Serialize(memorySummaries, new JsonSerializerOptions { WriteIndented = true })
        });

        try
        {
            var thread = _agent.GetNewThread();
            var result = await _agent.RunAsync(prompt, thread, cancellationToken: cancellationToken);
            var response = result.Text ?? result.ToString();

            // Parse AI response
            var learning = ParseLearningResponse(response, memories);

            _logger.LogInformation("Created consolidated learning on topic: {Topic}", learning.Topic);
            return learning;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to consolidate memories with AI, creating basic learning");

            // Fallback: create basic learning without AI
            return new ConsolidatedLearning
            {
                Topic = "General",
                Summary = $"Consolidated {memories.Count} memories",
                SourceMemoryIds = memories.Select(m => m.Id).ToList(),
                Confidence = 0.5
            };
        }
    }

    public async Task<List<ConsolidatedLearning>> ExtractLearningsAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting learnings since {Since}", since);

        var recentMemories = await _longTermMemory.GetRecentMemoriesAsync(500, cancellationToken) ?? [];
        var memoriesSince = recentMemories.Where(m => m.Timestamp >= since).ToList();

        if (!memoriesSince.Any())
        {
            _logger.LogInformation("No memories found since {Since}", since);
            return new List<ConsolidatedLearning>();
        }

        // Group memories by type and time period
        var groupedMemories = memoriesSince
            .GroupBy(m => m.Type)
            .Where(g => g.Count() >= 3) // Only consolidate if we have at least 3 memories
            .ToList();

        var learnings = new List<ConsolidatedLearning>();

        foreach (var group in groupedMemories)
        {
            try
            {
                var learning = await ConsolidateMemoriesAsync(group.ToList(), cancellationToken);
                learnings.Add(learning);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create learning from {Type} memories", group.Key);
            }
        }

        _logger.LogInformation("Extracted {Count} learnings", learnings.Count);
        return learnings;
    }

    /// <summary>
    /// Consolidate short-term thoughts and actions into long-term memory.
    /// This is the main method called daily to promote important items from short-term to long-term storage.
    /// </summary>
    public async Task ConsolidateShortTermMemoryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting short-term memory consolidation");

        // Retrieve all thoughts and actions from persisted short-term memory (fallback to in-memory state)
        var thoughts = await GetPersistedThoughtsAsync(cancellationToken);
        var actions = await GetPersistedActionsAsync(cancellationToken);

        _logger.LogInformation("Retrieved {ThoughtCount} thoughts and {ActionCount} actions from short-term memory",
            thoughts.Count, actions.Count);

        // Convert thoughts to memory entries and store important ones
        int thoughtsStored = 0;
        foreach (var thought in thoughts)
        {
            var importance = CalculateThoughtImportance(thought);

            // Only store thoughts with sufficient importance
            if (importance >= 0.5)
            {
                var memory = new MemoryEntry
                {
                    Id = thought.Id,
                    Type = thought.Type switch
                    {
                        ThoughtType.Observation => MemoryType.Observation,
                        ThoughtType.Reasoning => MemoryType.Decision,
                        ThoughtType.Reflection => MemoryType.Reflection,
                        _ => MemoryType.Observation
                    },
                    Content = thought.Content,
                    Summary = $"{thought.Type}: {thought.Content.Substring(0, Math.Min(100, thought.Content.Length))}...",
                    Importance = importance,
                    Tags = new List<string> { "consolidated", "thought", thought.Type.ToString().ToLower() },
                    Timestamp = thought.Timestamp
                };

                await _longTermMemory.StoreMemoryAsync(memory, cancellationToken);
                await _shortTermMemory.SetAsync($"memory:{memory.Id}", memory, TimeSpan.FromHours(24), cancellationToken);
                thoughtsStored++;
            }
        }

        // Convert actions to memory entries and store important ones
        int actionsStored = 0;
        foreach (var action in actions)
        {
            var importance = CalculateActionImportance(action);

            // Only store actions with sufficient importance
            if (importance >= 0.5)
            {
                var memory = new MemoryEntry
                {
                    Id = action.Id,
                    Type = action.Status == ActionStatus.Completed ? MemoryType.Success : MemoryType.Decision,
                    Content = $"Action: {action.Name}\nDescription: {action.Description}\nInput: {action.Input}\nOutput: {action.Output}",
                    Summary = $"{action.Name}: {action.Description}",
                    Importance = importance,
                    Tags = new List<string> { "consolidated", "action", action.Status.ToString().ToLower(), action.Name.ToLower() },
                    Timestamp = action.Timestamp
                };

                await _longTermMemory.StoreMemoryAsync(memory, cancellationToken);
                await _shortTermMemory.SetAsync($"memory:{memory.Id}", memory, TimeSpan.FromHours(24), cancellationToken);
                actionsStored++;
            }
        }

        _logger.LogInformation("Consolidated {ThoughtCount}/{TotalThoughts} thoughts and {ActionCount}/{TotalActions} actions to long-term memory",
            thoughtsStored, thoughts.Count, actionsStored, actions.Count);

        // Extract learnings from the consolidated memories
        var learnings = await ExtractLearningsAsync(DateTime.UtcNow.AddHours(-24), cancellationToken);

        foreach (var learning in learnings)
        {
            await StoreLearningAsync(learning, cancellationToken);
        }
    }

    private double CalculateThoughtImportance(AgentThought thought)
    {
        // Base importance by type
        double importance = thought.Type switch
        {
            ThoughtType.Observation => 0.3,
            ThoughtType.Reasoning => 0.7,
            ThoughtType.Reflection => 0.8,
            _ => 0.5
        };

        // Increase importance for longer, more detailed thoughts
        if (thought.Content.Length > 200)
        {
            importance += 0.1;
        }

        // Cap at 1.0
        return Math.Min(1.0, importance);
    }

    private double CalculateActionImportance(AgentAction action)
    {
        // Base importance by status
        double importance = action.Status switch
        {
            ActionStatus.Completed => 0.7,
            ActionStatus.Failed => 0.6,
            ActionStatus.Running => 0.4,
            _ => 0.3
        };

        // Increase importance for actions with output (they produced results)
        if (!string.IsNullOrEmpty(action.Output))
        {
            importance += 0.2;
        }

        // Cap at 1.0
        return Math.Min(1.0, importance);
    }

    public async Task<bool> StoreLearningAsync(ConsolidatedLearning learning, CancellationToken cancellationToken = default)
    {
        _learnings.Add(learning);
        _logger.LogInformation("Stored learning {Id} on topic {Topic}", learning.Id, learning.Topic);

        // Also store as a memory for future reference
        var memory = new MemoryEntry
        {
            Type = MemoryType.Learning,
            Content = learning.Summary,
            Summary = learning.Topic,
            Metadata = new Dictionary<string, string>
            {
                ["LearningId"] = learning.Id,
                ["Confidence"] = learning.Confidence.ToString()
            },
            Importance = learning.Confidence,
            Tags = new List<string> { "learning", learning.Topic }
        };

        // Store memory asynchronously with error handling
        try
        {
            await _longTermMemory.StoreMemoryAsync(memory, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store learning memory for {LearningId}", learning.Id);
        }

        return true;
    }

    public async Task<List<MemoryEntry>> IdentifyOutdatedMemoriesAsync(CancellationToken cancellationToken = default)
    {
        var allMemories = await _longTermMemory.GetRecentMemoriesAsync(1000, cancellationToken);

        // Identify memories that are:
        // 1. Older than 30 days with low access count
        // 2. Have low importance score
        // 3. Are errors that have been resolved

        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        var outdatedMemories = allMemories.Where(m =>
            (m.Timestamp < cutoffDate && m.AccessCount < 3) ||
            (m.Importance < 0.3) ||
            (m.Type == MemoryType.Error && m.Timestamp < DateTime.UtcNow.AddDays(-7))
        ).ToList();

        _logger.LogInformation("Identified {Count} outdated memories", outdatedMemories.Count);
        return outdatedMemories;
    }

    public async Task<int> CleanupOutdatedMemoriesAsync(CancellationToken cancellationToken = default)
    {
        var outdatedMemories = await IdentifyOutdatedMemoriesAsync(cancellationToken);

        int deletedCount = 0;
        foreach (var memory in outdatedMemories)
        {
            if (await _longTermMemory.DeleteMemoryAsync(memory.Id, cancellationToken))
            {
                deletedCount++;
            }
        }

        _logger.LogInformation("Cleaned up {Count} outdated memories", deletedCount);
        return deletedCount;
    }

    private async Task<List<AgentThought>> GetPersistedThoughtsAsync(CancellationToken cancellationToken)
    {
        var thoughts = new List<AgentThought>();

        try
        {
            var keys = await _shortTermMemory.GetKeysAsync("thought:*", cancellationToken);
            if (keys.Count > 0)
            {
                var tasks = keys.Select(async k =>
                {
                    try { return await _shortTermMemory.GetAsync<AgentThought>(k, cancellationToken); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load thought from short-term memory key {Key}", k);
                        return null;
                    }
                });

                var results = await Task.WhenAll(tasks);
                thoughts.AddRange(results.Where(t => t != null)!);
            }

            // Fallback to in-memory state if nothing persisted
            if (thoughts.Count == 0)
            {
                thoughts = await _stateManager.GetAllThoughtsFromMemoryAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading thoughts from short-term memory; falling back to state manager");
            thoughts = await _stateManager.GetAllThoughtsFromMemoryAsync(cancellationToken);
        }

        return thoughts;
    }

    private async Task<List<AgentAction>> GetPersistedActionsAsync(CancellationToken cancellationToken)
    {
        var actions = new List<AgentAction>();

        try
        {
            var keys = await _shortTermMemory.GetKeysAsync("action:*", cancellationToken);
            if (keys.Count > 0)
            {
                var tasks = keys.Select(async k =>
                {
                    try { return await _shortTermMemory.GetAsync<AgentAction>(k, cancellationToken); }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to load action from short-term memory key {Key}", k);
                        return null;
                    }
                });

                var results = await Task.WhenAll(tasks);
                actions.AddRange(results.Where(a => a != null)!);
            }

            // Fallback to in-memory state if nothing persisted
            if (actions.Count == 0)
            {
                actions = await _stateManager.GetAllActionsFromMemoryAsync(cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading actions from short-term memory; falling back to state manager");
            actions = await _stateManager.GetAllActionsFromMemoryAsync(cancellationToken);
        }

        return actions;
    }

    private ConsolidatedLearning ParseLearningResponse(string response, List<MemoryEntry> sourceMemories)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<LearningResponse>(jsonStr, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed != null)
                {
                    return new ConsolidatedLearning
                    {
                        Topic = parsed.Topic ?? "General",
                        Summary = parsed.Summary ?? "Learning extracted",
                        SourceMemoryIds = sourceMemories.Select(m => m.Id).ToList(),
                        Insights = parsed.Insights ?? new Dictionary<string, object>(),
                        Confidence = parsed.Confidence ?? 0.7
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse learning response, using fallback");
        }

        // Fallback
        return new ConsolidatedLearning
        {
            Topic = "General",
            Summary = response.Length > 200 ? response.Substring(0, 200) : response,
            SourceMemoryIds = sourceMemories.Select(m => m.Id).ToList(),
            Confidence = 0.6
        };
    }

    private class LearningResponse
    {
        public string? Topic { get; set; }
        public string? Summary { get; set; }
        public Dictionary<string, object>? Insights { get; set; }
        public double? Confidence { get; set; }
    }
}
