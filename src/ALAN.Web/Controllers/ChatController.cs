using ALAN.Shared.Models;
using ALAN.Shared.Services.Queue;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.Web.Controllers;

[ApiController]
[Route("api")]
public class ChatController : ControllerBase
{
    private readonly ILogger<ChatController> _logger;
    private readonly IMessageQueue<HumanInput> _chatRequestQueue;
    private readonly IMessageQueue<ChatResponse> _chatResponseQueue;

    public ChatController(
        ILogger<ChatController> logger,
        IMessageQueue<HumanInput> chatRequestQueue,
        IMessageQueue<ChatResponse> chatResponseQueue)
    {
        _logger = logger;
        _chatRequestQueue = chatRequestQueue;
        _chatResponseQueue = chatResponseQueue;
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
            // Create a human input for chat
            var humanInput = new HumanInput
            {
                Type = HumanInputType.ChatWithAgent,
                Content = request.Message,
                Timestamp = DateTime.UtcNow
            };

            // Send to chat request queue
            await _chatRequestQueue.SendAsync(humanInput, cancellationToken);

            // Poll for response with timeout
            var maxWaitTime = TimeSpan.FromSeconds(30);
            var startTime = DateTime.UtcNow;
            var pollInterval = TimeSpan.FromMilliseconds(500);

            while (DateTime.UtcNow - startTime < maxWaitTime)
            {
                // Check response queue for matching response
                var messages = await _chatResponseQueue.ReceiveAsync(
                    maxMessages: 10,
                    visibilityTimeout: TimeSpan.FromSeconds(5),
                    cancellationToken: cancellationToken);

                foreach (var msg in messages)
                {
                    if (msg.Content.MessageId == humanInput.Id)
                    {
                        // Found our response, delete it and return
                        await _chatResponseQueue.DeleteAsync(msg.MessageId, msg.PopReceipt, cancellationToken);
                        return Ok(msg.Content);
                    }
                    // Not our message, let it become visible again
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
