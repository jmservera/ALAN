using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
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

    public event EventHandler<AgentState>? StateChanged;

    public StateManager(IShortTermMemoryService shortTermMemory, ILongTermMemoryService longTermMemory)
    {
        _shortTermMemory = shortTermMemory;
        _longTermMemory = longTermMemory;
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
    }

    public void UpdateAction(AgentAction action)
    {
        _actionDict[action.Id] = action;
        UpdateState();

        // Update action in short-term memory only
        _ = _shortTermMemory.SetAsync($"action:{action.Id}", action, TimeSpan.FromHours(8));
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
}
