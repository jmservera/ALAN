// Example usage of GoogleSearchTool for testing
using ALAN.Agent.Tools;
using Microsoft.Extensions.Logging;

namespace ALAN.Agent.Examples;

/// <summary>
/// Example demonstrating how to use the GoogleSearchTool independently
/// </summary>
public class GoogleSearchToolExample
{
    public static async Task RunExample()
    {
        // Configuration - replace with your actual credentials
        var apiKey = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_API_KEY")
            ?? throw new InvalidOperationException("GOOGLE_SEARCH_API_KEY not set");

        var searchEngineId = Environment.GetEnvironmentVariable("GOOGLE_SEARCH_ENGINE_ID")
            ?? throw new InvalidOperationException("GOOGLE_SEARCH_ENGINE_ID not set");

        // Create logger (optional)
        using var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger<GoogleSearchTool>();

        // Create the search tool
        var searchTool = new GoogleSearchTool(apiKey, searchEngineId, logger);

        // Example 1: Simple search
        Console.WriteLine("=== Example 1: Simple Search ===");
        var result1 = await searchTool.SearchWebAsync("artificial intelligence", maxResults: 3);
        Console.WriteLine(result1);
        Console.WriteLine();

        // Example 2: Specific topic search
        Console.WriteLine("=== Example 2: Specific Topic ===");
        var result2 = await searchTool.SearchWebAsync("quantum computing applications", maxResults: 5);
        Console.WriteLine(result2);
        Console.WriteLine();

        // Example 3: Current events
        Console.WriteLine("=== Example 3: Current Events ===");
        var result3 = await searchTool.SearchWebAsync("latest technology news 2025", maxResults: 3);
        Console.WriteLine(result3);
        Console.WriteLine();

        // Example 4: Using with AI Agent Framework
        Console.WriteLine("=== Example 4: Create AIFunction ===");
        var aiFunction = GoogleSearchTool.CreateAIFunction(searchTool);
        Console.WriteLine($"Created AIFunction for agent framework");
        Console.WriteLine($"The function can be added to the agent's tools list");
        Console.WriteLine();
    }
}
