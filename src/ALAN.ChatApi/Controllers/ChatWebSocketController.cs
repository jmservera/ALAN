using ALAN.ChatApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ALAN.ChatApi.Controllers;

[ApiController]
[Route("api/chat")]
public class ChatWebSocketController : ControllerBase
{
    private readonly ChatService _chatService;
    private readonly ILogger<ChatWebSocketController> _logger;

    public ChatWebSocketController(ChatService chatService, ILogger<ChatWebSocketController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpGet("ws")]
    public async Task Get(CancellationToken cancellationToken)
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            await HandleWebSocketConnection(webSocket, cancellationToken);
        }
        else
        {
            HttpContext.Response.StatusCode = 400;
        }
    }

    private async Task HandleWebSocketConnection(WebSocket webSocket, CancellationToken cancellationToken)
    {
        var sessionId = Guid.NewGuid().ToString();
        _logger.LogInformation("WebSocket connection established for session {SessionId}", sessionId);

        var buffer = new byte[1024 * 4];

        try
        {
            while (webSocket.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), 
                    cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    _logger.LogInformation("WebSocket close requested for session {SessionId}", sessionId);
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Closing",
                        cancellationToken);
                    break;
                }

                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                _logger.LogDebug("Received message: {Message}", message);

                try
                {
                    var request = JsonSerializer.Deserialize<ChatWebSocketMessage>(message);
                    
                    if (request?.Action == "chat" && !string.IsNullOrWhiteSpace(request.Message))
                    {
                        // Process chat with streaming
                        await _chatService.ProcessChatAsync(
                            sessionId,
                            request.Message,
                            async (token) =>
                            {
                                // Stream tokens back to client
                                var response = new ChatWebSocketResponse
                                {
                                    Type = "token",
                                    Content = token
                                };
                                await SendWebSocketMessage(webSocket, response, cancellationToken);
                            },
                            cancellationToken);

                        // Send completion message
                        var completion = new ChatWebSocketResponse
                        {
                            Type = "complete",
                            Content = string.Empty
                        };
                        await SendWebSocketMessage(webSocket, completion, cancellationToken);
                    }
                    else if (request?.Action == "clear")
                    {
                        await _chatService.ClearHistoryAsync(sessionId, cancellationToken);
                        var response = new ChatWebSocketResponse
                        {
                            Type = "cleared",
                            Content = "Chat history cleared"
                        };
                        await SendWebSocketMessage(webSocket, response, cancellationToken);
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize WebSocket message");
                    var errorResponse = new ChatWebSocketResponse
                    {
                        Type = "error",
                        Content = "Invalid message format"
                    };
                    await SendWebSocketMessage(webSocket, errorResponse, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Operation cancelled for session {SessionId}", sessionId);
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing chat message");
                    var errorResponse = new ChatWebSocketResponse
                    {
                        Type = "error",
                        Content = "An error occurred processing your message"
                    };
                    await SendWebSocketMessage(webSocket, errorResponse, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("WebSocket connection cancelled for session {SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WebSocket error for session {SessionId}", sessionId);
        }
        finally
        {
            await _chatService.ClearHistoryAsync(sessionId, CancellationToken.None);
            _logger.LogInformation("WebSocket connection closed for session {SessionId}", sessionId);
        }
    }

    private async Task SendWebSocketMessage(
        WebSocket webSocket,
        ChatWebSocketResponse response,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response);
        var bytes = Encoding.UTF8.GetBytes(json);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }
}

public class ChatWebSocketMessage
{
    public string Action { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class ChatWebSocketResponse
{
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}
