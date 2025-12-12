using ALAN.Shared.Models;
using ALAN.Shared.Services.Queue;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.Web.Controllers;

[ApiController]
[Route("api")]
public class HumanInputController : ControllerBase
{
    private readonly ILogger<HumanInputController> _logger;
    private readonly IMessageQueue<HumanInput> _humanInputQueue;
    
    public HumanInputController(
        ILogger<HumanInputController> logger,
        IMessageQueue<HumanInput> humanInputQueue)
    {
        _logger = logger;
        _humanInputQueue = humanInputQueue;
    }
    
    [HttpPost("input")]
    public async Task<IActionResult> SubmitInput([FromBody] HumanInput input, CancellationToken cancellationToken)
    {
        if (input == null)
        {
            return BadRequest("Input cannot be null");
        }

        _logger.LogInformation("Received human input: {Type} - {Content}", input.Type, input.Content);
        
        // Send to human input queue
        await _humanInputQueue.SendAsync(input, cancellationToken);
        
        return Ok(new
        {
            inputId = input.Id,
            message = "Input received and queued for processing",
            timestamp = DateTime.UtcNow
        });
    }
    
    [HttpPost("prompt")]
    public async Task<IActionResult> UpdatePrompt([FromBody] UpdatePromptRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Prompt))
        {
            return BadRequest("Prompt cannot be empty");
        }

        _logger.LogInformation("Received prompt update: {Prompt}", request.Prompt);
        
        var input = new HumanInput
        {
            Type = HumanInputType.UpdatePrompt,
            Content = request.Prompt
        };
        
        // Send to human input queue
        await _humanInputQueue.SendAsync(input, cancellationToken);
        
        return Ok(new
        {
            message = "Prompt update queued",
            prompt = request.Prompt
        });
    }
    
    [HttpPost("pause")]
    public async Task<IActionResult> PauseAgent(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received pause command");
        
        var input = new HumanInput
        {
            Type = HumanInputType.PauseAgent,
            Content = "Pause requested"
        };
        
        // Send to human input queue
        await _humanInputQueue.SendAsync(input, cancellationToken);
        
        return Ok(new { message = "Agent pause queued" });
    }
    
    [HttpPost("resume")]
    public async Task<IActionResult> ResumeAgent(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received resume command");
        
        var input = new HumanInput
        {
            Type = HumanInputType.ResumeAgent,
            Content = "Resume requested"
        };
        
        // Send to human input queue
        await _humanInputQueue.SendAsync(input, cancellationToken);
        
        return Ok(new { message = "Agent resume queued" });
    }
    
    [HttpPost("batch-learning")]
    public async Task<IActionResult> TriggerBatchLearning(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received batch learning trigger");
        
        var input = new HumanInput
        {
            Type = HumanInputType.TriggerBatchLearning,
            Content = "Batch learning trigger"
        };
        
        // Send to human input queue
        await _humanInputQueue.SendAsync(input, cancellationToken);
        
        return Ok(new { message = "Batch learning trigger queued" });
    }

    [HttpPost("memory-consolidation")]
    public async Task<IActionResult> TriggerMemoryConsolidation(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received memory consolidation trigger");
        
        var input = new HumanInput
        {
            Type = HumanInputType.TriggerMemoryConsolidation,
            Content = "Memory consolidation trigger"
        };

        await _humanInputQueue.SendAsync(input, cancellationToken);

        return Ok(new { message = "Memory consolidation trigger queued" });
    }
}

public class UpdatePromptRequest
{
    public string Prompt { get; set; } = string.Empty;
}
