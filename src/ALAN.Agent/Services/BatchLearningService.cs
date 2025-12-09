using ALAN.Shared.Services.Memory;
using Microsoft.Extensions.Logging;

namespace ALAN.Agent.Services;

/// <summary>
/// Manages batch learning processes that run periodically to consolidate memories
/// and extract learnings from the agent's experiences.
/// </summary>
public class BatchLearningService
{
    private readonly IMemoryConsolidationService _consolidationService;
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly ILogger<BatchLearningService> _logger;
    private DateTime _lastBatchRun = DateTime.UtcNow;
    private bool _isRunning = false;

    public BatchLearningService(
        IMemoryConsolidationService consolidationService,
        ILongTermMemoryService longTermMemory,
        ILogger<BatchLearningService> logger)
    {
        _consolidationService = consolidationService;
        _longTermMemory = longTermMemory;
        _logger = logger;
    }

    /// <summary>
    /// Check if it's time to run the batch learning process.
    /// </summary>
    public bool ShouldRunBatch(int iterationsSinceLastBatch, int batchInterval = 100)
    {
        if (_isRunning) return false;
        
        // Run batch every N iterations or every 4 hours (whichever comes first)
        var timeSinceLastBatch = DateTime.UtcNow - _lastBatchRun;
        return iterationsSinceLastBatch >= batchInterval || timeSinceLastBatch > TimeSpan.FromHours(4);
    }

    /// <summary>
    /// Run the batch learning process.
    /// </summary>
    public async Task RunBatchLearningAsync(CancellationToken cancellationToken = default)
    {
        if (_isRunning)
        {
            _logger.LogWarning("Batch learning already running, skipping");
            return;
        }

        try
        {
            _isRunning = true;
            _logger.LogInformation("Starting batch learning process");

            var startTime = DateTime.UtcNow;

            // Step 1: Extract learnings from recent memories
            var learnings = await _consolidationService.ExtractLearningsAsync(_lastBatchRun, cancellationToken);
            _logger.LogInformation("Extracted {Count} learnings", learnings.Count);

            // Step 2: Store consolidated learnings
            foreach (var learning in learnings)
            {
                await _consolidationService.StoreLearningAsync(learning, cancellationToken);
            }

            // Step 3: Cleanup outdated memories (optional, run less frequently)
            var timeSinceStartup = DateTime.UtcNow - _lastBatchRun;
            if (timeSinceStartup > TimeSpan.FromHours(24))
            {
                _logger.LogInformation("Running memory cleanup");
                var deletedCount = await _consolidationService.CleanupOutdatedMemoriesAsync(cancellationToken);
                _logger.LogInformation("Cleaned up {Count} outdated memories", deletedCount);
            }

            // Step 4: Log statistics
            var memoryCount = await _longTermMemory.GetMemoryCountAsync(cancellationToken);
            _logger.LogInformation("Batch learning completed. Total memories: {Count}, Learnings extracted: {Learnings}",
                memoryCount, learnings.Count);

            _lastBatchRun = startTime;
            
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation("Batch learning took {Duration}ms", duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch learning process");
        }
        finally
        {
            _isRunning = false;
        }
    }

    /// <summary>
    /// Get statistics about batch learning.
    /// </summary>
    public BatchLearningStats GetStats()
    {
        return new BatchLearningStats
        {
            LastBatchRun = _lastBatchRun,
            IsRunning = _isRunning,
            TimeSinceLastBatch = DateTime.UtcNow - _lastBatchRun
        };
    }
}

public class BatchLearningStats
{
    public DateTime LastBatchRun { get; set; }
    public bool IsRunning { get; set; }
    public TimeSpan TimeSinceLastBatch { get; set; }
}
