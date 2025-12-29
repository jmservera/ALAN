using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ALAN.Agent.Services;

public class StateManager
{
    private readonly ConcurrentQueue<AgentThought> _thoughts = new();
    private readonly ConcurrentQueue<AgentAction> _actions = new();
    private readonly ConcurrentDictionary<string, AgentAction> _actionDict = new();
    private AgentState _currentState = new();
    private readonly object _lock = new();
    private readonly IShortTermMemoryService _shortTermMemory;
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly MemoryAgent? _memoryAgent;
    private readonly ILogger<StateManager> _logger;

    public event EventHandler<AgentState>? StateChanged;

    public StateManager(
        IShortTermMemoryService shortTermMemory,
        ILongTermMemoryService longTermMemory,
        ILogger<StateManager> logger,
        MemoryAgent? memoryAgent = null)
    {
        _shortTermMemory = shortTermMemory;
        _longTermMemory = longTermMemory;
        _logger = logger;
        _memoryAgent = memoryAgent;

        if (_memoryAgent != null)
        {
            _logger.LogInformation("MemoryAgent available - short-term memories will be immediately searchable via vector search");
        }
    }

    public void AddThought(AgentThought thought)
    {
        _thoughts.Enqueue(thought);

        // Keep only recent thoughts
        while (_thoughts.Count > 100)
        {
            _thoughts.TryDequeue(out _);
        }

        UpdateState();

        // Store thought in short-term memory only
        // Memory consolidation service will promote important thoughts to long-term
        _ = _shortTermMemory.SetAsync($"thought:{thought.Id}", thought, TimeSpan.FromHours(8));

        // Also store in vector memory for immediate semantic search
        if (_memoryAgent != null)
        {
            _ = Task.Run(async () =>
            {
                try
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
                        Importance = CalculateThoughtImportance(thought),
                        Tags = new List<string> { "short-term", "thought", thought.Type.ToString().ToLower() },
                        Timestamp = thought.Timestamp
                    };

                    await _memoryAgent.MigrateMemoryToVectorSearchAsync(memory, default);
                    _logger.LogTrace("Thought {Id} stored in vector search", thought.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store thought {Id} in vector search", thought.Id);
                }
            });
        }
    }

    public void AddAction(AgentAction action)
    {
        _actions.Enqueue(action);
        _actionDict[action.Id] = action;

        // Keep only recent actions
        while (_actions.Count > 50)
        {
            if (_actions.TryDequeue(out var old))
            {
                _actionDict.TryRemove(old.Id, out _);
            }
        }

        UpdateState();

        // Store action in short-term memory only
        // Memory consolidation service will promote important actions to long-term
        _ = _shortTermMemory.SetAsync($"action:{action.Id}", action, TimeSpan.FromHours(8));

        // Also store in vector memory for immediate semantic search
        if (_memoryAgent != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var memory = new MemoryEntry
                    {
                        Id = action.Id,
                        Type = action.Status == ActionStatus.Completed ? MemoryType.Success : MemoryType.Decision,
                        Content = $"Action: {action.Name}\nDescription: {action.Description}\nInput: {action.Input}\nOutput: {action.Output}",
                        Summary = $"{action.Name}: {action.Description}",
                        Importance = CalculateActionImportance(action),
                        Tags = new List<string> { "short-term", "action", action.Status.ToString().ToLower(), action.Name.ToLower() },
                        Timestamp = action.Timestamp
                    };

                    await _memoryAgent.MigrateMemoryToVectorSearchAsync(memory, default);
                    _logger.LogTrace("Action {Id} stored in vector search", action.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store action {Id} in vector search", action.Id);
                }
            });
        }
    }

    public void UpdateAction(AgentAction action)
    {
        _actionDict[action.Id] = action;
        UpdateState();

        // Update action in short-term memory only
        _ = _shortTermMemory.SetAsync($"action:{action.Id}", action, TimeSpan.FromHours(8));

        // Also update in vector memory for immediate semantic search
        if (_memoryAgent != null)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var memory = new MemoryEntry
                    {
                        Id = action.Id,
                        Type = action.Status == ActionStatus.Completed ? MemoryType.Success : MemoryType.Decision,
                        Content = $"Action: {action.Name}\nDescription: {action.Description}\nInput: {action.Input}\nOutput: {action.Output}",
                        Summary = $"{action.Name}: {action.Description}",
                        Importance = CalculateActionImportance(action),
                        Tags = new List<string> { "short-term", "action", action.Status.ToString().ToLower(), action.Name.ToLower() },
                        Timestamp = action.Timestamp
                    };

                    await _memoryAgent.MigrateMemoryToVectorSearchAsync(memory, default);
                    _logger.LogTrace("Action {Id} updated in vector search", action.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to update action {Id} in vector search", action.Id);
                }
            });
        }
    }

    public void UpdateStatus(AgentStatus status)
    {
        lock (_lock)
        {
            _currentState.Status = status;
            _currentState.LastUpdated = DateTime.UtcNow;
        }

        NotifyStateChanged();
        PersistState();
    }

    public void UpdateGoal(string goal)
    {
        lock (_lock)
        {
            _currentState.CurrentGoal = goal;
            _currentState.LastUpdated = DateTime.UtcNow;
        }

        NotifyStateChanged();
        PersistState();
    }

    public void UpdatePrompt(string prompt)
    {
        lock (_lock)
        {
            _currentState.CurrentPrompt = prompt;
            _currentState.LastUpdated = DateTime.UtcNow;
        }

        NotifyStateChanged();
        PersistState();
    }

    public AgentState GetCurrentState()
    {
        lock (_lock)
        {
            var state = new AgentState
            {
                Id = _currentState.Id,
                Status = _currentState.Status,
                CurrentGoal = _currentState.CurrentGoal,
                CurrentPrompt = _currentState.CurrentPrompt,
                LastUpdated = DateTime.UtcNow,
                RecentThoughts = _thoughts.TakeLast(20).ToList(),
                RecentActions = _actions.TakeLast(10).ToList()
            };

            return state;
        }
    }

    private void UpdateState()
    {
        lock (_lock)
        {
            _currentState.RecentThoughts = _thoughts.TakeLast(20).ToList();
            _currentState.RecentActions = _actions.TakeLast(10).ToList();
            _currentState.LastUpdated = DateTime.UtcNow;
        }

        NotifyStateChanged();
        PersistState();
    }

    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(this, GetCurrentState());
    }

    private void PersistState()
    {
        // Store current state in short-term memory only
        // The web UI will read from short-term for real-time updates
        var state = GetCurrentState();
        _ = _shortTermMemory.SetAsync("agent:current-state", state, TimeSpan.FromHours(1));
    }

    // Helper method to get all thoughts from short-term memory
    // Used by MemoryConsolidationService to retrieve thoughts for consolidation
    public Task<List<AgentThought>> GetAllThoughtsFromMemoryAsync(CancellationToken cancellationToken = default)
    {
        var thoughts = new List<AgentThought>();

        // Return in-memory thoughts for now
        // In future, could query short-term memory if needed
        lock (_lock)
        {
            thoughts = _thoughts.ToList();
        }

        return Task.FromResult(thoughts);
    }

    // Helper method to get all actions from short-term memory
    // Used by MemoryConsolidationService to retrieve actions for consolidation
    public Task<List<AgentAction>> GetAllActionsFromMemoryAsync(CancellationToken cancellationToken = default)
    {
        var actions = new List<AgentAction>();

        // Return in-memory actions for now
        // In future, could query short-term memory if needed
        lock (_lock)
        {
            actions = _actions.ToList();
        }

        return Task.FromResult(actions);
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
}
