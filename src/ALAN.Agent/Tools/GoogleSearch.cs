namespace ALAN.Agent.Tools;

using Google.Apis.CustomSearchAPI.v1;
using Google.Apis.CustomSearchAPI.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text.Json;

/// <summary>
/// AI Tool that provides web search capabilities using Google Custom Search API.
/// </summary>
public class GoogleSearchTool
{
    private readonly CustomSearchAPIService _searchService;
    private readonly string _searchEngineId;
    private readonly ILogger<GoogleSearchTool>? _logger;

    public GoogleSearchTool(string apiKey, string searchEngineId, ILogger<GoogleSearchTool>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("API key cannot be null or empty", nameof(apiKey));

        if (string.IsNullOrWhiteSpace(searchEngineId))
            throw new ArgumentException("Search engine ID cannot be null or empty", nameof(searchEngineId));

        _searchEngineId = searchEngineId;
        _logger = logger;

        _searchService = new CustomSearchAPIService(new BaseClientService.Initializer
        {
            ApiKey = apiKey,
            ApplicationName = "ALAN Agent"
        });
    }

    /// <summary>
    /// Searches the web for information using Google Custom Search.
    /// </summary>
    /// <param name="query">The search query to execute.</param>
    /// <param name="maxResults">Maximum number of results to return (1-10).</param>
    /// <returns>A formatted string containing search results with titles, links, and snippets.</returns>
    [Description("Searches the web for information using Google. Use this when you need to find current information, facts, or learn about topics.")]
    public async Task<string> SearchWebAsync(
        [Description("The search query to execute")] string query,
        [Description("Maximum number of results to return (1-10)")] int maxResults = 5)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return "Error: Search query cannot be empty.";
            }

            // Clamp maxResults between 1 and 10
            maxResults = Math.Max(1, Math.Min(10, maxResults));

            _logger?.LogInformation("Executing search: {Query} (max results: {MaxResults})", query, maxResults);

            var listRequest = _searchService.Cse.List();
            listRequest.Cx = _searchEngineId;
            listRequest.Q = query;
            listRequest.Num = maxResults;

            var search = await listRequest.ExecuteAsync();

            if (search.Items == null || search.Items.Count == 0)
            {
                _logger?.LogWarning("No search results found for query: {Query}", query);
                return $"No results found for: {query}";
            }

            var results = new List<SearchResult>();
            foreach (var item in search.Items)
            {
                results.Add(new SearchResult
                {
                    Title = item.Title ?? "No title",
                    Link = item.Link ?? "",
                    Snippet = item.Snippet ?? "No description available"
                });
            }

            // Format results for AI consumption
            var formattedResults = FormatSearchResults(query, results);

            _logger?.LogInformation("Search completed: Found {Count} results for '{Query}'", results.Count, query);

            return formattedResults;
        }
        catch (Google.GoogleApiException ex)
        {
            _logger?.LogError(ex, "Google API error during search for query: {Query}", query);
            return $"Search error: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during search for query: {Query}", query);
            return $"Unexpected error during search: {ex.Message}";
        }
    }

    private static string FormatSearchResults(string query, List<SearchResult> results)
    {
        var output = new System.Text.StringBuilder();
        output.AppendLine($"Search results for: {query}");
        output.AppendLine($"Found {results.Count} result(s)");
        output.AppendLine();

        for (int i = 0; i < results.Count; i++)
        {
            var result = results[i];
            output.AppendLine($"Result {i + 1}:");
            output.AppendLine($"Title: {result.Title}");
            output.AppendLine($"URL: {result.Link}");
            output.AppendLine($"Summary: {result.Snippet}");
            output.AppendLine();
        }

        return output.ToString();
    }

    /// <summary>
    /// Creates an AIFunction from this tool's SearchWebAsync method.
    /// </summary>
    public static AIFunction CreateAIFunction(GoogleSearchTool tool)
    {
        return AIFunctionFactory.Create(
            tool.SearchWebAsync,
            name: "search_web",
            description: "Searches the web for information using Google. Use this when you need to find current information, facts, or learn about topics."
        );
    }
}

internal class SearchResult
{
    public string Title { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Snippet { get; set; } = string.Empty;
}