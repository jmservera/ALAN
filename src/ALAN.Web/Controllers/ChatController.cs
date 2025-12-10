using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.Web.Controllers;

[ApiController]
[Route("api")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly IShortTermMemoryService _shortTermMemory;

    public ChatController(
        ILogger<ChatController> logger,
        IShortTermMemoryService shortTermMemory)
    {
        _logger = logger;
        _shortTermMemory = shortTermMemory;
    }

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest("Message cannot be empty");
        }

        _logger.LogInformation("Received chat message: {Message}", request.Message);

        try
        {
            // Create a chat message
            var chatMessage = new ChatMessage
            {
                Role = ChatMessageRole.Human,
                Content = request.Message
            };

            // Store chat request in memory for agent to pick up
            var humanInput = new HumanInput
            {
                Type = HumanInputType.ChatWithAgent,
                Content = request.Message,
                Timestamp = DateTime.UtcNow
            };

            // Store the input for the agent to process
            await _shortTermMemory.SetAsync(
                $"chat-request:{humanInput.Id}",
                humanInput,
                TimeSpan.FromMinutes(5),
                cancellationToken);

            // Wait for response (polling with timeout)
            var maxWaitTime = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromMilliseconds(500);

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                var response = await _shortTermMemory.GetAsync<ChatResponse>(
                    $"chat-response:{humanInput.Id}",
                    cancellationToken);

                if (response != null)
                {
                    // Clean up the request and response from memory
                    await _shortTermMemory.DeleteAsync($"chat-request:{humanInput.Id}", cancellationToken);
                    await _shortTermMemory.DeleteAsync($"chat-response:{humanInput.Id}", cancellationToken);

                    return Ok(response);
                }

                await Task.Delay(pollInterval, cancellationToken);
            }

            // Timeout - no response received
            _logger.LogWarning("Chat request timed out for message: {Message}", request.Message);
            return Ok(new ChatResponse
            {
                MessageId = humanInput.Id,
                Response = "I'm currently busy with other tasks. Please try again in a moment.",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat message");
            return StatusCode(500, new ChatResponse
            {
                Response = "I encountered an error processing your message. Please try again.",
                Timestamp = DateTime.UtcNow
            });
        }
    }
}
