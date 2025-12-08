using System;
using System.Threading;
using System.Threading.Tasks;
using ALAN.Core.BatchProcessing;
using ALAN.Core.Memory;
using ALAN.Core.Services;
using Microsoft.Extensions.Logging;

namespace ALAN.Core.Loop;

/// <summary>
/// Configuration for the autonomous loop
/// </summary>
public class LoopConfig
{
    public int IterationDelaySeconds { get; set; } = 30;
    public int BatchProcessingIntervalIterations { get; set; } = 100;
    public bool EnableBatchProcessing { get; set; } = true;
}

/// <summary>
/// Main autonomous agent loop with pause/resume capability
/// </summary>
public class AutonomousLoop
{
    private readonly IMemoryService _shortTermMemory;
    private readonly IMemoryService _longTermMemory;
    private readonly BatchLearningProcessor _batchProcessor;
    private readonly AgentOrchestrator _orchestrator;
    private readonly ILogger<AutonomousLoop> _logger;
    private readonly LoopConfig _config;

    private CancellationTokenSource? _loopCancellation;
    private Task? _loopTask;
    private bool _isPaused;
    private readonly SemaphoreSlim _pauseLock = new(1, 1);
    private int _iterationCount;

    public bool IsRunning => _loopTask != null && !_loopTask.IsCompleted;
    public bool IsPaused => _isPaused;
    public int IterationCount => _iterationCount;

    public AutonomousLoop(
        IMemoryService shortTermMemory,
        IMemoryService longTermMemory,
        BatchLearningProcessor batchProcessor,
        AgentOrchestrator orchestrator,
        LoopConfig config,
        ILogger<AutonomousLoop> logger)
    {
        _shortTermMemory = shortTermMemory;
        _longTermMemory = longTermMemory;
        _batchProcessor = batchProcessor;
        _orchestrator = orchestrator;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Start the autonomous loop
    /// </summary>
    public Task StartAsync()
    {
        if (IsRunning)
        {
            _logger.LogWarning("Loop is already running");
            return Task.CompletedTask;
        }

        _loopCancellation = new CancellationTokenSource();
        _loopTask = RunLoopAsync(_loopCancellation.Token);
        _logger.LogInformation("Autonomous loop started");
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stop the autonomous loop gracefully
    /// </summary>
    public async Task StopAsync()
    {
        if (!IsRunning)
        {
            _logger.LogWarning("Loop is not running");
            return;
        }

        _logger.LogInformation("Stopping autonomous loop...");
        _loopCancellation?.Cancel();

        if (_loopTask != null)
        {
            await _loopTask;
        }

        _logger.LogInformation("Autonomous loop stopped");
    }

    /// <summary>
    /// Pause the loop (for batch processing or human intervention)
    /// </summary>
    public async Task PauseAsync()
    {
        await _pauseLock.WaitAsync();
        try
        {
            if (_isPaused)
            {
                _logger.LogWarning("Loop is already paused");
                return;
            }

            _isPaused = true;
            _logger.LogInformation("Autonomous loop paused");
        }
        finally
        {
            _pauseLock.Release();
        }
    }

    /// <summary>
    /// Resume the loop after pause
    /// </summary>
    public async Task ResumeAsync()
    {
        await _pauseLock.WaitAsync();
        try
        {
            if (!_isPaused)
            {
                _logger.LogWarning("Loop is not paused");
                return;
            }

            _isPaused = false;
            _logger.LogInformation("Autonomous loop resumed");
        }
        finally
        {
            _pauseLock.Release();
        }
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Autonomous loop is running");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Check if paused
                while (_isPaused && !cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, cancellationToken);
                }

                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // Execute one iteration
                    await ExecuteIterationAsync(cancellationToken);
                    _iterationCount++;

                    // Check if batch processing is needed
                    if (_config.EnableBatchProcessing && 
                        _iterationCount % _config.BatchProcessingIntervalIterations == 0)
                    {
                        await RunBatchProcessingAsync(cancellationToken);
                    }

                    // Delay before next iteration
                    await Task.Delay(TimeSpan.FromSeconds(_config.IterationDelaySeconds), cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in loop iteration {Iteration}", _iterationCount);
                    
                    // Store error in memory
                    await _longTermMemory.StoreAsync(new MemoryEntry
                    {
                        Type = MemoryType.LongTerm,
                        Content = $"Error in iteration {_iterationCount}: {ex.Message}",
                        Metadata = new() { { "ErrorType", "LoopIteration" } }
                    }, cancellationToken);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Autonomous loop cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in autonomous loop");
            throw;
        }
    }

    private async Task ExecuteIterationAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Executing iteration {Iteration}", _iterationCount);

        // Execute agent orchestrator task
        await _orchestrator.ExecuteTaskAsync(cancellationToken);

        // Log iteration in memory
        await _shortTermMemory.StoreAsync(new MemoryEntry
        {
            Type = MemoryType.ShortTerm,
            Content = $"Completed iteration {_iterationCount}",
            Metadata = new() 
            { 
                { "Iteration", _iterationCount.ToString() },
                { "Timestamp", DateTime.UtcNow.ToString("o") }
            }
        }, cancellationToken);
    }

    private async Task RunBatchProcessingAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting batch processing at iteration {Iteration}", _iterationCount);

        try
        {
            // Pause the loop
            await PauseAsync();

            // Run batch learning process
            await _batchProcessor.ProcessAsync(cancellationToken);

            // Resume the loop
            await ResumeAsync();

            _logger.LogInformation("Batch processing completed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch processing");
            await ResumeAsync(); // Ensure we resume even on error
        }
    }
}
