using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ALAN.Core.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ALAN.Core.BatchProcessing;

/// <summary>
/// Batch learning processor that analyzes long-term memory and creates new learnings
/// </summary>
public class BatchLearningProcessor
{
    private readonly IMemoryService _longTermMemory;
    private readonly IMemoryService _shortTermMemory;
    private readonly Kernel _kernel;
    private readonly ILogger<BatchLearningProcessor> _logger;

    public BatchLearningProcessor(
        IMemoryService longTermMemory,
        IMemoryService shortTermMemory,
        Kernel kernel,
        ILogger<BatchLearningProcessor> logger)
    {
        _longTermMemory = longTermMemory;
        _shortTermMemory = shortTermMemory;
        _kernel = kernel;
        _logger = logger;
    }

    /// <summary>
    /// Run the batch learning process to analyze and summarize long-term memory
    /// </summary>
    public async Task ProcessAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting batch learning process at {Time}", DateTime.UtcNow);

        try
        {
            // Retrieve recent long-term memory entries
            var recentMemories = await _longTermMemory.RetrieveAsync(MemoryType.LongTerm, limit: 100, cancellationToken);
            var memoriesList = recentMemories.ToList();

            if (!memoriesList.Any())
            {
                _logger.LogInformation("No long-term memories to process");
                return;
            }

            _logger.LogInformation("Processing {Count} memory entries", memoriesList.Count);

            // Group memories by date for batched processing
            var groupedMemories = memoriesList
                .GroupBy(m => m.Timestamp.Date)
                .OrderByDescending(g => g.Key)
                .Take(7); // Process last 7 days

            foreach (var dayGroup in groupedMemories)
            {
                await ProcessDailyMemoriesAsync(dayGroup.ToList(), cancellationToken);
            }

            _logger.LogInformation("Batch learning process completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch learning process");
            throw;
        }
    }

    private async Task ProcessDailyMemoriesAsync(List<MemoryEntry> memories, CancellationToken cancellationToken)
    {
        if (!memories.Any()) return;

        var date = memories.First().Timestamp.Date;
        _logger.LogInformation("Processing memories for {Date}", date);

        try
        {
            // Prepare memory content for analysis
            var memoryContent = string.Join("\n\n", memories.Select(m => 
                $"[{m.Timestamp:HH:mm}] {m.Content}"));

            // Use Semantic Kernel to analyze and summarize
            var chatService = _kernel.GetRequiredService<IChatCompletionService>();
            
            var prompt = $@"You are an AI learning system analyzing your own historical activity logs.
Review the following activity logs from {date:yyyy-MM-dd} and extract key learnings, patterns, and insights.

Activity Logs:
{memoryContent}

Please provide:
1. Key insights and patterns observed
2. Important decisions or actions taken
3. Areas for improvement or optimization
4. Learnings that should be retained for future decision-making

Format your response as a concise summary focusing on actionable learnings.";

            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var response = await chatService.GetChatMessageContentAsync(chatHistory, cancellationToken: cancellationToken);
            var summary = response.Content ?? "No summary generated";

            // Store the learning
            var learningEntry = new MemoryEntry
            {
                Type = MemoryType.Learning,
                Content = summary,
                Metadata = new Dictionary<string, string>
                {
                    { "Date", date.ToString("yyyy-MM-dd") },
                    { "MemoryCount", memories.Count.ToString() },
                    { "ProcessedAt", DateTime.UtcNow.ToString("o") }
                }
            };

            await _longTermMemory.StoreAsync(learningEntry, cancellationToken);
            _logger.LogInformation("Created learning entry for {Date}", date);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process memories for {Date}", date);
        }
    }
}
