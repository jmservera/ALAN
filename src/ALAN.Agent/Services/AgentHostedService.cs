using ALAN.Agent.Services;
using ALAN.Shared.Services.Memory;
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
    private readonly IMemoryConsolidationService _memoryConsolidation;
    private readonly IPromptService _promptService;
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
        HumanInputHandler humanInputHandler,
        IMemoryConsolidationService memoryConsolidation,
        IPromptService promptService)
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
        _memoryConsolidation = memoryConsolidation;
        _promptService = promptService;
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
            _humanInputHandler,
            _promptService);

        // Start memory consolidation background task
        var consolidationTask = RunMemoryConsolidationAsync(stoppingToken);

        try
        {
            await _agent.RunAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in agent");
        }

        // Wait for consolidation task to complete
        await consolidationTask;
    }

    private async Task RunMemoryConsolidationAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Memory consolidation task starting...");

        // Wait before first consolidation to allow some memories to accumulate
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            bool flowControl = await ExecuteMemoryConsolidationOnceAsync(stoppingToken);
            if (!flowControl)
            {
                break;
            }
        }

        _logger.LogInformation("Memory consolidation task stopped");
    }

    public async Task<bool> ExecuteMemoryConsolidationOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Running memory consolidation...");
            await _memoryConsolidation.ConsolidateShortTermMemoryAsync(stoppingToken);
            _logger.LogInformation("Memory consolidation completed");

            // Run consolidation every 6 hours (well before 8-hour short-term TTL expires)
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Memory consolidation cancelled");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during memory consolidation");
            // Wait a bit before retrying
            await Task.Delay(TimeSpan.FromMinutes(30), stoppingToken);
        }

        return true;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Agent Hosted Service stopping...");
        _agent?.Stop();
        return base.StopAsync(cancellationToken);
    }
}
