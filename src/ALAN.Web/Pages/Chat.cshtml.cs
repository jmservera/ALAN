using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ALAN.Web.Pages;

public class ChatModel : PageModel
{
    private readonly ILogger<ChatModel> _logger;

    public ChatModel(ILogger<ChatModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        _logger.LogInformation("Chat page accessed");
    }
}
