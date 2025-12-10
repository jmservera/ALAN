using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using ALAN.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ALAN.Web.Services;

public class AgentStateService : BackgroundService
{
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly ILogger<AgentStateService> _logger;
    private readonly IShortTermMemoryService _shortTermMemory;
    private readonly ILongTermMemoryService _longTermMemory;
    private AgentState _state = new();
    private readonly HashSet<string> _seenThoughtIds = new();
    private readonly HashSet<string> _seenActionIds = new();

    public AgentStateService(
        IHubContext<AgentHub> hubContext,
        ILogger<AgentStateService> logger,
        IShortTermMemoryService shortTermMemory,
        ILongTermMemoryService longTermMemory)
    {
        _hubContext = hubContext;
        _logger = logger;
        _shortTermMemory = shortTermMemory;
        _longTermMemory = longTermMemory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent State Service starting (pull mode - reading from shared memory)...");

        // Initial state
        _state.CurrentGoal = "Waiting for autonomous agent to start";
        _state.Status = AgentStatus.Idle;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PullStateFromMemoryAsync(stoppingToken);
                await Task.Delay(TimeSpan.FromMilliseconds(500), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in agent state service");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        _logger.LogInformation("Agent State Service stopped");
    }

    private async Task PullStateFromMemoryAsync(CancellationToken cancellationToken)
    {
        // Pull current state from short-term memory (agent stores it there)
        var currentState = await _shortTermMemory.GetAsync<AgentState>("agent:current-state", cancellationToken);

        // Pull recent thoughts from short-term memory
        var thoughtKeys = await _shortTermMemory.GetKeysAsync("thought:*", cancellationToken);
        var thoughts = new List<AgentThought>();

        foreach (var key in thoughtKeys)
        {
            var thought = await _shortTermMemory.GetAsync<AgentThought>(key, cancellationToken);
            if (thought != null)
            {
                thoughts.Add(thought);
            }
        }

        // Pull recent actions from short-term memory
        var actionKeys = await _shortTermMemory.GetKeysAsync("action:*", cancellationToken);
        var actions = new List<AgentAction>();

        foreach (var key in actionKeys)
        {
            var action = await _shortTermMemory.GetAsync<AgentAction>(key, cancellationToken);
            if (action != null)
            {
                actions.Add(action);
            }
        }

        // Sort by timestamp
        thoughts = thoughts.OrderBy(t => t.Timestamp).ToList();
        actions = actions.OrderBy(a => a.Timestamp).ToList();

        if (currentState != null)
        {
            var stateChanged = _state.Status != currentState.Status ||
                              _state.CurrentGoal != currentState.CurrentGoal ||
                              _state.CurrentPrompt != currentState.CurrentPrompt;

            _state = currentState;

            if (stateChanged)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveStateUpdate", _state, cancellationToken);
                _logger.LogDebug("Broadcasted state update: {Status}", _state.Status);
            }
        }
        else
        {
            // No state in memory yet, create default
            _state = new AgentState
            {
                CurrentGoal = "Waiting for autonomous agent to start",
                Status = AgentStatus.Idle
            };
        }

        // Populate state with recent thoughts and actions for API endpoint
        _state.RecentThoughts = thoughts.TakeLast(20).ToList();
        _state.RecentActions = actions.TakeLast(15).ToList();

        // Broadcast new thoughts
        foreach (var thought in thoughts.Where(t => !_seenThoughtIds.Contains(t.Id)))
        {
            await _hubContext.Clients.All.SendAsync("ReceiveThought", thought, cancellationToken);
            _seenThoughtIds.Add(thought.Id);
            _logger.LogDebug("Broadcasted thought: {Type}", thought.Type);
        }

        // Broadcast new actions  
        foreach (var action in actions.Where(a => !_seenActionIds.Contains(a.Id)))
        {
            await _hubContext.Clients.All.SendAsync("ReceiveAction", action, cancellationToken);
            _seenActionIds.Add(action.Id);
            _logger.LogDebug("Broadcasted action: {Name}", action.Name);
        }

        // Cleanup old IDs to prevent memory bloat
        if (_seenThoughtIds.Count > 1000)
        {
            var toRemove = _seenThoughtIds.Take(_seenThoughtIds.Count - 500).ToList();
            foreach (var id in toRemove)
                _seenThoughtIds.Remove(id);
        }

        if (_seenActionIds.Count > 500)
        {
            var toRemove = _seenActionIds.Take(_seenActionIds.Count - 250).ToList();
            foreach (var id in toRemove)
                _seenActionIds.Remove(id);
        }
    }

    public AgentState GetCurrentState()
    {
        return _state;
    }
}
