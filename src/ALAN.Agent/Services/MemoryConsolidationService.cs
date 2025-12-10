using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.AI;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace ALAN.Agent.Services;

/// <summary>
/// Service for consolidating memories and extracting learnings.
/// Uses AI to analyze patterns and create higher-level insights.
/// </summary>
public class MemoryConsolidationService : IMemoryConsolidationService
{
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly AIAgent _agent;
    private readonly ILogger<MemoryConsolidationService> _logger;
    private readonly List<ConsolidatedLearning> _learnings = new();

    public MemoryConsolidationService(
        ILongTermMemoryService longTermMemory,
        AIAgent agent,
        ILogger<MemoryConsolidationService> logger)
    {
        _longTermMemory = longTermMemory;
        _agent = agent;
        _logger = logger;
    }

    public async Task<ConsolidatedLearning> ConsolidateMemoriesAsync(List<MemoryEntry> memories, CancellationToken cancellationToken = default)
    {
        if (!memories.Any())
        {
            throw new ArgumentException("No memories to consolidate", nameof(memories));
        }

        _logger.LogInformation("Consolidating {Count} memories", memories.Count);

        // Prepare memory summaries for AI analysis
        var memorySummaries = memories.Select(m => new
        {
            m.Type,
            m.Summary,
            m.Content,
            m.Timestamp,
            m.Tags
        }).ToList();

        var prompt = $@"You are analyzing {memories.Count} memory entries to extract key learnings and patterns.

Memories to analyze:
{JsonSerializer.Serialize(memorySummaries, new JsonSerializerOptions { WriteIndented = true })}

Please analyze these memories and provide:
1. A topic that these memories relate to
2. A summary of the key learning or pattern
3. Specific insights in JSON format
4. A confidence score (0.0 to 1.0)

Respond with ONLY a JSON object in this format:
{{
  ""topic"": ""the main topic"",
  ""summary"": ""concise summary of the learning"",
  ""insights"": {{
    ""pattern"": ""description of any pattern found"",
    ""actionable"": ""actionable insight if any"",
    ""related_concepts"": [""concept1"", ""concept2""]
  }},
  ""confidence"": 0.8
}}";

        try
        {
            var thread = _agent.GetNewThread();
            var result = await _agent.RunAsync(prompt, thread, cancellationToken: cancellationToken);
            var response = result.Text ?? result.ToString();

            // Parse AI response
            var learning = ParseLearningResponse(response, memories);
            
            _logger.LogInformation("Created consolidated learning on topic: {Topic}", learning.Topic);
            return learning;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to consolidate memories with AI, creating basic learning");
            
            // Fallback: create basic learning without AI
            return new ConsolidatedLearning
            {
                Topic = "General",
                Summary = $"Consolidated {memories.Count} memories",
                SourceMemoryIds = memories.Select(m => m.Id).ToList(),
                Confidence = 0.5
            };
        }
    }

    public async Task<List<ConsolidatedLearning>> ExtractLearningsAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting learnings since {Since}", since);

        var recentMemories = await _longTermMemory.GetRecentMemoriesAsync(500, cancellationToken);
        var memoriesSince = recentMemories.Where(m => m.Timestamp >= since).ToList();

        if (!memoriesSince.Any())
        {
            _logger.LogInformation("No memories found since {Since}", since);
            return new List<ConsolidatedLearning>();
        }

        // Group memories by type and time period
        var groupedMemories = memoriesSince
            .GroupBy(m => m.Type)
            .Where(g => g.Count() >= 3) // Only consolidate if we have at least 3 memories
            .ToList();

        var learnings = new List<ConsolidatedLearning>();

        foreach (var group in groupedMemories)
        {
            try
            {
                var learning = await ConsolidateMemoriesAsync(group.ToList(), cancellationToken);
                learnings.Add(learning);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create learning from {Type} memories", group.Key);
            }
        }

        _logger.LogInformation("Extracted {Count} learnings", learnings.Count);
        return learnings;
    }

    public async Task<bool> StoreLearningAsync(ConsolidatedLearning learning, CancellationToken cancellationToken = default)
    {
        _learnings.Add(learning);
        _logger.LogInformation("Stored learning {Id} on topic {Topic}", learning.Id, learning.Topic);
        
        // Also store as a memory for future reference
        var memory = new MemoryEntry
        {
            Type = MemoryType.Learning,
            Content = learning.Summary,
            Summary = learning.Topic,
            Metadata = new Dictionary<string, string>
            {
                ["LearningId"] = learning.Id,
                ["Confidence"] = learning.Confidence.ToString()
            },
            Importance = learning.Confidence,
            Tags = new List<string> { "learning", learning.Topic }
        };

        // Store memory asynchronously with error handling
        try
        {
            await _longTermMemory.StoreMemoryAsync(memory, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store learning memory for {LearningId}", learning.Id);
        }
        
        return true;
    }

    public async Task<List<MemoryEntry>> IdentifyOutdatedMemoriesAsync(CancellationToken cancellationToken = default)
    {
        var allMemories = await _longTermMemory.GetRecentMemoriesAsync(1000, cancellationToken);
        
        // Identify memories that are:
        // 1. Older than 30 days with low access count
        // 2. Have low importance score
        // 3. Are errors that have been resolved
        
        var cutoffDate = DateTime.UtcNow.AddDays(-30);
        var outdatedMemories = allMemories.Where(m =>
            (m.Timestamp < cutoffDate && m.AccessCount < 3) ||
            (m.Importance < 0.3) ||
            (m.Type == MemoryType.Error && m.Timestamp < DateTime.UtcNow.AddDays(-7))
        ).ToList();

        _logger.LogInformation("Identified {Count} outdated memories", outdatedMemories.Count);
        return outdatedMemories;
    }

    public async Task<int> CleanupOutdatedMemoriesAsync(CancellationToken cancellationToken = default)
    {
        var outdatedMemories = await IdentifyOutdatedMemoriesAsync(cancellationToken);
        
        int deletedCount = 0;
        foreach (var memory in outdatedMemories)
        {
            if (await _longTermMemory.DeleteMemoryAsync(memory.Id, cancellationToken))
            {
                deletedCount++;
            }
        }

        _logger.LogInformation("Cleaned up {Count} outdated memories", deletedCount);
        return deletedCount;
    }

    private ConsolidatedLearning ParseLearningResponse(string response, List<MemoryEntry> sourceMemories)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var parsed = JsonSerializer.Deserialize<LearningResponse>(jsonStr);

                if (parsed != null)
                {
                    return new ConsolidatedLearning
                    {
                        Topic = parsed.Topic ?? "General",
                        Summary = parsed.Summary ?? "Learning extracted",
                        SourceMemoryIds = sourceMemories.Select(m => m.Id).ToList(),
                        Insights = parsed.Insights ?? new Dictionary<string, object>(),
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
