using ALAN.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.Web.Controllers;

[ApiController]
[Route("api")]
public class HumanInputController : ControllerBase
{
    private readonly ILogger<HumanInputController> _logger;
    // TODO: Connect to actual agent when running together
    
    public HumanInputController(ILogger<HumanInputController> logger)
    {
        _logger = logger;
    }
    
    [HttpPost("input")]
    public IActionResult SubmitInput([FromBody] HumanInput input)
    {
        if (input == null)
        {
            return BadRequest("Input cannot be null");
        }

        _logger.LogInformation("Received human input: {Type} - {Content}", input.Type, input.Content);
        
        // TODO: Forward to agent's HumanInputHandler when integration is complete
        // For now, just log and acknowledge
        
        return Ok(new
        {
            inputId = input.Id,
            message = "Input received and queued for processing",
            timestamp = DateTime.UtcNow
        });
    }
    
    [HttpPost("prompt")]
    public IActionResult UpdatePrompt([FromBody] UpdatePromptRequest request)
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
        
        // TODO: Forward to agent
        
        return Ok(new
        {
            message = "Prompt update queued",
            prompt = request.Prompt
        });
    }
    
    [HttpPost("pause")]
    public IActionResult PauseAgent()
    {
        _logger.LogInformation("Received pause command");
        
        var input = new HumanInput
        {
            Type = HumanInputType.PauseAgent,
            Content = "Pause requested"
        };
        
        // TODO: Forward to agent
        
        return Ok(new { message = "Agent pause queued" });
    }
    
    [HttpPost("resume")]
    public IActionResult ResumeAgent()
    {
        _logger.LogInformation("Received resume command");
        
        var input = new HumanInput
        {
            Type = HumanInputType.ResumeAgent,
            Content = "Resume requested"
        };
        
        // TODO: Forward to agent
        
        return Ok(new { message = "Agent resume queued" });
    }
    
    [HttpPost("batch-learning")]
    public IActionResult TriggerBatchLearning()
    {
        _logger.LogInformation("Received batch learning trigger");
        
        var input = new HumanInput
        {
            Type = HumanInputType.TriggerBatchLearning,
            Content = "Batch learning trigger"
        };
        
        // TODO: Forward to agent
        
        return Ok(new { message = "Batch learning trigger queued" });
    }
}

public class UpdatePromptRequest
{
    public string Prompt { get; set; } = string.Empty;
}
