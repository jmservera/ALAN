using ALAN.Shared.Models;
using System.Collections.Concurrent;

namespace ALAN.Agent.Services;

public class StateManager
{
    private readonly ConcurrentQueue<AgentThought> _thoughts = new();
    private readonly ConcurrentQueue<AgentAction> _actions = new();
    private readonly ConcurrentDictionary<string, AgentAction> _actionDict = new();
    private AgentState _currentState = new();
    private readonly object _lock = new();
    
    public event EventHandler<AgentState>? StateChanged;
    
    public void AddThought(AgentThought thought)
    {
        _thoughts.Enqueue(thought);
        
        // Keep only recent thoughts
        while (_thoughts.Count > 100)
        {
            _thoughts.TryDequeue(out _);
        }
        
        UpdateState();
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
    }
    
    public void UpdateAction(AgentAction action)
    {
        _actionDict[action.Id] = action;
        UpdateState();
    }
    
    public void UpdateStatus(AgentStatus status)
    {
        lock (_lock)
        {
            _currentState.Status = status;
            _currentState.LastUpdated = DateTime.UtcNow;
        }
        
        NotifyStateChanged();
    }
    
    public void UpdateGoal(string goal)
    {
        lock (_lock)
        {
            _currentState.CurrentGoal = goal;
            _currentState.LastUpdated = DateTime.UtcNow;
        }
        
        NotifyStateChanged();
    }
    
    public void UpdatePrompt(string prompt)
    {
        lock (_lock)
        {
            _currentState.CurrentPrompt = prompt;
            _currentState.LastUpdated = DateTime.UtcNow;
        }
        
        NotifyStateChanged();
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
    }
    
    private void NotifyStateChanged()
    {
        StateChanged?.Invoke(this, GetCurrentState());
    }
}
