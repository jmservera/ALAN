using ALAN.Core.Loop;
using ALAN.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.API.Controllers;

/// <summary>
/// Controller for human steering and control of the autonomous agent
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentController : ControllerBase
{
    private readonly AgentOrchestrator _orchestrator;
    private readonly AutonomousLoop _loop;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        AgentOrchestrator orchestrator,
        AutonomousLoop loop,
        ILogger<AgentController> logger)
    {
        _orchestrator = orchestrator;
        _loop = loop;
        _logger = logger;
    }

    /// <summary>
    /// Get the current status of the agent
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            IsRunning = _loop.IsRunning,
            IsPaused = _loop.IsPaused,
            IterationCount = _loop.IterationCount,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Send human input to guide the agent
    /// </summary>
    [HttpPost("input")]
    public async Task<IActionResult> SendInput([FromBody] HumanInputRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Input))
        {
            return BadRequest(new { error = "Input cannot be empty" });
        }

        try
        {
            await _orchestrator.AddHumanInputAsync(request.Input);
            _logger.LogInformation("Human input received: {Input}", request.Input);

            return Ok(new
            {
                message = "Input queued successfully",
                input = request.Input,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing human input");
            return StatusCode(500, new { error = "Failed to process input" });
        }
    }

    /// <summary>
    /// Pause the autonomous loop
    /// </summary>
    [HttpPost("pause")]
    public async Task<IActionResult> Pause()
    {
        try
        {
            await _loop.PauseAsync();
            return Ok(new { message = "Agent paused", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing agent");
            return StatusCode(500, new { error = "Failed to pause agent" });
        }
    }

    /// <summary>
    /// Resume the autonomous loop
    /// </summary>
    [HttpPost("resume")]
    public async Task<IActionResult> Resume()
    {
        try
        {
            await _loop.ResumeAsync();
            return Ok(new { message = "Agent resumed", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming agent");
            return StatusCode(500, new { error = "Failed to resume agent" });
        }
    }

    /// <summary>
    /// Stop the autonomous loop
    /// </summary>
    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        try
        {
            await _loop.StopAsync();
            return Ok(new { message = "Agent stopped", timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping agent");
            return StatusCode(500, new { error = "Failed to stop agent" });
        }
    }
}

/// <summary>
/// Request model for human input
/// </summary>
public class HumanInputRequest
{
    public string Input { get; set; } = string.Empty;
}
