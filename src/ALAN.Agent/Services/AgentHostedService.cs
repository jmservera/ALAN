using ALAN.Agent.Services;
using ALAN.Agent.Services.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;

namespace ALAN.Agent.Services;

public class AgentHostedService : BackgroundService
{
    private readonly ILogger<AgentHostedService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly AIAgent _aiAgent;
    private readonly StateManager _stateManager;
    private readonly UsageTracker _usageTracker;
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly IShortTermMemoryService _shortTermMemory;
    private readonly BatchLearningService _batchLearningService;
    private readonly HumanInputHandler _humanInputHandler;
    private AutonomousAgent? _agent;

    public AgentHostedService(
        ILogger<AgentHostedService> logger,
        ILoggerFactory loggerFactory,
        AIAgent aiAgent,
        StateManager stateManager,
        UsageTracker usageTracker,
        ILongTermMemoryService longTermMemory,
        IShortTermMemoryService shortTermMemory,
        BatchLearningService batchLearningService,
        HumanInputHandler humanInputHandler)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _aiAgent = aiAgent;
        _stateManager = stateManager;
        _usageTracker = usageTracker;
        _longTermMemory = longTermMemory;
        _shortTermMemory = shortTermMemory;
        _batchLearningService = batchLearningService;
        _humanInputHandler = humanInputHandler;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Hosted Service starting...");

        _agent = new AutonomousAgent(
            _aiAgent,
            _loggerFactory.CreateLogger<AutonomousAgent>(),
            _stateManager,
            _usageTracker,
            _longTermMemory,
            _shortTermMemory,
            _batchLearningService,
            _humanInputHandler);

        try
        {
            await _agent.RunAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in agent");
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent Hosted Service stopping...");
        _agent?.Stop();
        return base.StopAsync(cancellationToken);
    }
}
