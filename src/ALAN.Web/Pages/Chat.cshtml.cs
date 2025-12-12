using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ALAN.Web.Pages;

public class ChatModel : PageModel
{
    private readonly ILogger<ChatModel> _logger;
    private readonly IConfiguration _configuration;

    public string ChatApiBaseUrl { get; private set; } = string.Empty;

    public ChatModel(ILogger<ChatModel> logger, IConfiguration configuration)
    {
        _logger = logger;
        _configuration = configuration;
    }

    public void OnGet()
    {
        _logger.LogInformation("Chat page accessed");

        // Read Chat API base URL from configuration or environment
        ChatApiBaseUrl = _configuration["ChatApi:BaseUrl"]
            ?? Environment.GetEnvironmentVariable("ALAN_CHATAPI_BASE_URL")
            ?? "http://localhost:5041/api"; // default dev URL for ChatApi
    }
}
