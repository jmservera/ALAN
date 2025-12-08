using ALAN.Shared.Models;
using ALAN.Web.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace ALAN.Web.Services;

public class AgentStateService : BackgroundService
{
    private readonly IHubContext<AgentHub> _hubContext;
    private readonly ILogger<AgentStateService> _logger;
    private readonly AgentState _state = new();
    private readonly Random _random = new();
    
    public AgentStateService(
        IHubContext<AgentHub> hubContext,
        ILogger<AgentStateService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent State Service starting...");
        
        // Initial state
        _state.CurrentGoal = "Initializing autonomous learning";
        _state.Status = AgentStatus.Idle;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Simulate agent activity for demo
                await SimulateAgentActivityAsync(stoppingToken);
                
                // Broadcast state update
                await _hubContext.Clients.All.SendAsync("ReceiveStateUpdate", _state, stoppingToken);
                
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in agent state service");
            }
        }
        
        _logger.LogInformation("Agent State Service stopped");
    }
    
    private async Task SimulateAgentActivityAsync(CancellationToken cancellationToken)
    {
        // Simulate different agent states
        var statuses = new[] { AgentStatus.Thinking, AgentStatus.Acting, AgentStatus.Idle };
        _state.Status = statuses[_random.Next(statuses.Length)];
        _state.LastUpdated = DateTime.UtcNow;
        
        // Add a thought
        if (_random.Next(100) < 30)
        {
            var thoughtTypes = Enum.GetValues<ThoughtType>();
            var thought = new AgentThought
            {
                Type = thoughtTypes[_random.Next(thoughtTypes.Length)],
                Content = GenerateThoughtContent(_state.Status)
            };
            
            _state.RecentThoughts.Add(thought);
            
            // Keep only recent thoughts
            if (_state.RecentThoughts.Count > 20)
            {
                _state.RecentThoughts.RemoveAt(0);
            }
            
            await _hubContext.Clients.All.SendAsync("ReceiveThought", thought, cancellationToken);
        }
        
        // Add an action
        if (_random.Next(100) < 20)
        {
            var action = new AgentAction
            {
                Name = "ExploreKnowledge",
                Description = GenerateActionDescription(),
                Input = "Exploring new concepts",
                Status = ActionStatus.Completed,
                Output = "Successfully explored"
            };
            
            _state.RecentActions.Add(action);
            
            // Keep only recent actions
            if (_state.RecentActions.Count > 10)
            {
                _state.RecentActions.RemoveAt(0);
            }
            
            await _hubContext.Clients.All.SendAsync("ReceiveAction", action, cancellationToken);
        }
    }
    
    private string GenerateThoughtContent(AgentStatus status)
    {
        return status switch
        {
            AgentStatus.Thinking => GetRandomItem(new[]
            {
                "Analyzing current situation and planning next steps",
                "Considering multiple approaches to solve this problem",
                "Reflecting on previous actions and their outcomes",
                "Evaluating the best course of action"
            }),
            AgentStatus.Acting => GetRandomItem(new[]
            {
                "Executing planned action sequence",
                "Processing information from external sources",
                "Updating internal knowledge base",
                "Performing scheduled tasks"
            }),
            _ => GetRandomItem(new[]
            {
                "Waiting for new input or directives",
                "Monitoring environment for changes",
                "Ready to process new information",
                "Idle state - awaiting next task"
            })
        };
    }
    
    private string GenerateActionDescription()
    {
        return GetRandomItem(new[]
        {
            "Learning about machine learning algorithms",
            "Exploring natural language processing techniques",
            "Studying autonomous systems design",
            "Researching cognitive architectures",
            "Analyzing decision-making frameworks",
            "Investigating knowledge representation methods"
        });
    }
    
    private T GetRandomItem<T>(T[] items)
    {
        return items[_random.Next(items.Length)];
    }
    
    public AgentState GetCurrentState()
    {
        return _state;
    }
}
