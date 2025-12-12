using ALAN.ChatApi.Services;
using ALAN.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.ChatApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ChatService chatService, ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest("Message cannot be empty");
        }

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString()
            : request.SessionId!;

        _logger.LogInformation("Processing chat request for session {SessionId}", sessionId);

        // Non-streaming path for HTTP clients: collect full response
        var fullResponse = await _chatService.ProcessChatAsync(
            sessionId,
            request.Message,
            onTokenReceived: _ => Task.CompletedTask,
            cancellationToken: cancellationToken);

        var response = new ChatResponse
        {
            MessageId = sessionId,
            Response = fullResponse,
            Timestamp = DateTime.UtcNow
        };

        return Ok(response);
    }
}
