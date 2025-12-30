using ALAN.Shared.Models;
using ALAN.Shared.Services;
using ALAN.Shared.Services.Memory;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ALAN.Agent.Services;

/// <summary>
/// Specialized agent for managing memories using vector search.
/// Handles memory storage, retrieval, consolidation, and task deduplication.
/// Uses native Agent Framework constructs (AIAgent/AgentThread).
/// </summary>
public class MemoryAgent
{
    private readonly AIAgent _agent;
    private readonly AgentThread _thread;
    private readonly IVectorMemoryService _vectorMemory;
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly ILogger<MemoryAgent> _logger;
    private readonly IPromptService _promptService;

    public MemoryAgent(
        AIAgent agent,
        IVectorMemoryService vectorMemory,
        ILongTermMemoryService longTermMemory,
        ILogger<MemoryAgent> logger,
        IPromptService promptService)
    {
        _agent = agent;
        _thread = agent.GetNewThread();
        _vectorMemory = vectorMemory;
        _longTermMemory = longTermMemory;
        _logger = logger;
        _promptService = promptService;
    }

    /// <summary>
    /// Searches for memories relevant to a specific task or question.
    /// Uses semantic vector search to find the most relevant past experiences.
    /// </summary>
    public async Task<List<MemorySearchResult>> SearchRelevantMemoriesAsync(
        string taskDescription,
        int maxResults = 10,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Searching for memories relevant to: {Task}", taskDescription);

        try
        {
            // Use vector search to find semantically similar memories
            var results = await _vectorMemory.SearchMemoriesAsync(
                query: taskDescription,
                maxResults: maxResults,
                minScore: 0.7,
                filters: null,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Found {Count} relevant memories", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search relevant memories");
            return [];
        }
    }

    /// <summary>
    /// Retrieves all recent short-term memories (immediate context).
    /// Used to provide the agent with recent thoughts and actions without semantic search.
    /// </summary>
    public async Task<List<MemoryEntry>> GetShortTermContextAsync(
        int maxResults = 50,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Retrieving short-term memory context (max: {Max})", maxResults);

        try
        {
            var memories = await _vectorMemory.GetAllRecentMemoriesAsync(
                maxResults: maxResults,
                collection: "short-term",
                cancellationToken: cancellationToken);

            _logger.LogInformation("Retrieved {Count} short-term memories", memories.Count);
            return memories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve short-term context");
            return [];
        }
    }

    /// <summary>
    /// Searches long-term consolidated memories for relevant context.
    /// Used to find task-relevant knowledge from past experiences.
    /// </summary>
    public async Task<List<MemorySearchResult>> SearchLongTermContextAsync(
        string taskDescription,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Searching long-term memory context for: {Task}", taskDescription);

        try
        {
            var results = await _vectorMemory.SearchMemoriesAsync(
                query: taskDescription,
                maxResults: maxResults,
                minScore: 0.7,
                filters: null,
                collection: "long-term",
                cancellationToken: cancellationToken);

            _logger.LogInformation("Found {Count} relevant long-term memories", results.Count);
            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search long-term context");
            return [];
        }
    }

    /// <summary>
    /// Combines short-term (immediate) and long-term (relevant) memories for agent context.
    /// </summary>
    public async Task<string> BuildCombinedMemoryContextAsync(
        string taskDescription,
        int maxShortTerm = 30,
        int maxLongTerm = 15,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get recent short-term memories (immediate context)
            var shortTermMemories = await GetShortTermContextAsync(maxShortTerm, cancellationToken);

            // Search long-term memories for relevance (learned knowledge)
            var longTermResults = await SearchLongTermContextAsync(taskDescription, maxLongTerm, cancellationToken);

            // Build formatted context
            var context = new System.Text.StringBuilder();

            if (shortTermMemories.Count > 0)
            {
                context.AppendLine("## Recent Context (Short-term Memory)");
                context.AppendLine();
                foreach (var memory in shortTermMemories.Take(maxShortTerm))
                {
                    context.AppendLine($"- [{memory.Type}] {memory.Summary}");
                    if (memory.Importance >= 0.8)
                    {
                        context.AppendLine($"  {memory.Content}");
                    }
                }
                context.AppendLine();
            }

            if (longTermResults.Count > 0)
            {
                context.AppendLine("## Relevant Past Experience (Long-term Memory)");
                context.AppendLine();
                foreach (var result in longTermResults.Take(maxLongTerm))
                {
                    var memory = result.Memory;
                    context.AppendLine($"- [{memory.Type}] {memory.Summary} (relevance: {result.Score:F2})");
                    if (memory.Importance >= 0.7)
                    {
                        context.AppendLine($"  {memory.Content}");
                    }
                }
            }

            return context.ToString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build combined memory context");
            return string.Empty;
        }
    }

    /// <summary>
    /// Checks if a similar task has been completed before to avoid duplication.
    /// Returns the memory of the completed task if found.
    /// </summary>
    public async Task<MemorySearchResult?> FindSimilarCompletedTaskAsync(
        string taskDescription,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking for similar completed tasks: {Task}", taskDescription);

        try
        {
            // Search for similar successes and decisions
            var filters = new MemorySearchFilters
            {
                Types = [MemoryType.Success, MemoryType.CodeChange],
                MinImportance = 0.5
            };

            var results = await _vectorMemory.SearchMemoriesAsync(
                query: taskDescription,
                maxResults: 5,
                minScore: 0.85, // High threshold for task similarity
                filters: filters,
                cancellationToken: cancellationToken);

            if (results.Count > 0)
            {
                var match = results.First();
                _logger.LogInformation("Found similar completed task (score: {Score}): {Summary}",
                    match.Score, match.Memory.Summary);
                return match;
            }

            _logger.LogInformation("No similar completed tasks found");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check for similar tasks");
            return null;
        }
    }

    /// <summary>
    /// Consolidates a set of memories and extracts learnings using AI.
    /// </summary>
    public async Task<ConsolidatedLearning?> ConsolidateMemoriesWithAIAsync(
        List<MemoryEntry> memories,
        CancellationToken cancellationToken = default)
    {
        if (memories.Count == 0)
        {
            return null;
        }

        _logger.LogInformation("Consolidating {Count} memories with AI", memories.Count);

        try
        {
            var memorySummaries = memories.Select(m => new
            {
                m.Type,
                m.Summary,
                m.Timestamp,
                m.Tags,
                m.Importance
            }).ToList();

            var prompt = _promptService.RenderTemplate("memory-agent-consolidation", new
            {
                memoryCount = memories.Count,
                memoriesJson = JsonSerializer.Serialize(memorySummaries, new JsonSerializerOptions { WriteIndented = true })
            });

            var result = await _agent.RunAsync(prompt, _thread, cancellationToken: cancellationToken);
            var response = result.Text ?? result.ToString();

            // Parse the AI response into a consolidated learning
            var learning = ParseConsolidatedLearning(response, memories);

            _logger.LogInformation("Consolidated learning on topic: {Topic}", learning.Topic);
            return learning;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to consolidate memories with AI");
            return null;
        }
    }

    /// <summary>
    /// Evaluates how well memories match a given query.
    /// Returns relevance scores and suggestions for improvement.
    /// </summary>
    public async Task<MemorySearchEvaluation> EvaluateSearchQualityAsync(
        string query,
        List<MemorySearchResult> results,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Evaluating search quality for query: {Query}", query);

        try
        {
            var evaluation = new MemorySearchEvaluation
            {
                Query = query,
                ResultCount = results.Count,
                AverageScore = results.Any() ? results.Average(r => r.Score) : 0.0,
                MinScore = results.Any() ? results.Min(r => r.Score) : 0.0,
                MaxScore = results.Any() ? results.Max(r => r.Score) : 0.0
            };

            // Calculate precision (how many results are actually relevant)
            // For now, use a simple heuristic: score > 0.8 is relevant
            var relevantCount = results.Count(r => r.Score > 0.8);
            evaluation.Precision = results.Count > 0 ? (double)relevantCount / results.Count : 0.0;

            _logger.LogInformation("Search quality: {Precision:P2} precision, avg score {AvgScore:F3}",
                evaluation.Precision, evaluation.AverageScore);

            return evaluation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to evaluate search quality");
            return new MemorySearchEvaluation { Query = query, ResultCount = 0 };
        }
    }

    /// <summary>
    /// Migrates an existing memory from blob storage to vector search.
    /// </summary>
    public async Task<bool> MigrateMemoryToVectorSearchAsync(
        MemoryEntry memory,
        CancellationToken cancellationToken = default,
        string collection = "long-term")
    {
        try
        {
            _logger.LogDebug("Migrating memory {Id} to vector search collection {Collection}", memory.Id, collection);
            await _vectorMemory.StoreMemoryAsync(memory, collection, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to migrate memory {Id} to vector search collection {Collection}", memory.Id, collection);
            return false;
        }
    }

    /// <summary>
    /// Performs batch migration of memories from blob storage to vector search.
    /// </summary>
    public async Task<int> BatchMigrateMemoriesAsync(
        int batchSize = 100,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting batch migration of memories to vector search");

        try
        {
            var memories = await _longTermMemory.GetRecentMemoriesAsync(batchSize, cancellationToken);
            int migratedCount = 0;

            foreach (var memory in memories)
            {
                if (await MigrateMemoryToVectorSearchAsync(memory, cancellationToken))
                {
                    migratedCount++;
                }
            }

            _logger.LogInformation("Migrated {Count}/{Total} memories to vector search", migratedCount, memories.Count);
            return migratedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to batch migrate memories");
            return 0;
        }
    }

    private ConsolidatedLearning ParseConsolidatedLearning(string response, List<MemoryEntry> sourceMemories)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<LearningResponse>(jsonStr,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (parsed != null)
                {
                    return new ConsolidatedLearning
                    {
                        Topic = parsed.Topic ?? "General",
                        Summary = parsed.Summary ?? "Learning extracted",
                        SourceMemoryIds = sourceMemories.Select(m => m.Id).ToList(),
                        Insights = parsed.Insights ?? [],
                        Confidence = parsed.Confidence ?? 0.7
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse learning response, using fallback");
        }

        // Fallback
        return new ConsolidatedLearning
        {
            Topic = "General",
            Summary = response.Length > 200 ? response.Substring(0, 200) : response,
            SourceMemoryIds = sourceMemories.Select(m => m.Id).ToList(),
            Confidence = 0.6
        };
    }

    private class LearningResponse
    {
        public string? Topic { get; set; }
        public string? Summary { get; set; }
        public Dictionary<string, object>? Insights { get; set; }
        public double? Confidence { get; set; }
    }
}

/// <summary>
/// Evaluation metrics for memory search quality.
/// </summary>
public class MemorySearchEvaluation
{
    public string Query { get; set; } = string.Empty;
    public int ResultCount { get; set; }
    public double AverageScore { get; set; }
    public double MinScore { get; set; }
    public double MaxScore { get; set; }
    public double Precision { get; set; }
    public List<string> Suggestions { get; set; } = [];
}
