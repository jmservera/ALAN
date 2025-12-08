using ALAN.Agent.Services;
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
    private AutonomousAgent? _agent;

    public AgentHostedService(
        ILogger<AgentHostedService> logger,
        ILoggerFactory loggerFactory,
        AIAgent aiAgent,
        StateManager stateManager)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _aiAgent = aiAgent;
        _stateManager = stateManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Agent Hosted Service starting...");

        _agent = new AutonomousAgent(_aiAgent,
            _loggerFactory.CreateLogger<AutonomousAgent>(),
            _stateManager);

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
