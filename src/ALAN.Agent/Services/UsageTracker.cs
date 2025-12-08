using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace ALAN.Agent.Services;

/// <summary>
/// Tracks API usage to enforce daily limits and prevent excessive costs.
/// Target: Keep costs under $2/day for gpt-4o-mini ($0.15 per 1M tokens).
/// </summary>
public class UsageTracker
{
    private readonly ILogger<UsageTracker> _logger;
    private readonly int _maxLoopsPerDay;
    private readonly int _maxTokensPerDay;
    private readonly ConcurrentDictionary<DateTime, DailyUsage> _dailyUsage = new();
    private readonly object _lock = new();

    public UsageTracker(ILogger<UsageTracker> logger, int maxLoopsPerDay = 4000, int maxTokensPerDay = 8_000_000)
    {
        _logger = logger;
        _maxLoopsPerDay = maxLoopsPerDay;
        _maxTokensPerDay = maxTokensPerDay;

        _logger.LogInformation(
            "UsageTracker initialized - Max loops/day: {MaxLoops}, Max tokens/day: {MaxTokens} (~${Cost}/day)",
            _maxLoopsPerDay,
            _maxTokensPerDay,
            (_maxTokensPerDay / 1_000_000.0) * 0.15);
    }

    /// <summary>
    /// Check if we can execute another loop today.
    /// </summary>
    public bool CanExecuteLoop(out string? reason)
    {
        var today = DateTime.UtcNow.Date;
        var usage = GetOrCreateDailyUsage(today);

        if (usage.LoopCount >= _maxLoopsPerDay)
        {
            reason = $"Daily loop limit reached ({usage.LoopCount}/{_maxLoopsPerDay})";
            _logger.LogWarning("Loop limit reached for {Date}: {Count}/{Max}",
                today, usage.LoopCount, _maxLoopsPerDay);
            return false;
        }

        if (usage.EstimatedTokens >= _maxTokensPerDay)
        {
            reason = $"Daily token limit reached ({usage.EstimatedTokens:N0}/{_maxTokensPerDay:N0})";
            _logger.LogWarning("Token limit reached for {Date}: {Count:N0}/{Max:N0}",
                today, usage.EstimatedTokens, _maxTokensPerDay);
            return false;
        }

        reason = null;
        return true;
    }

    /// <summary>
    /// Record a completed loop with estimated token usage.
    /// </summary>
    public void RecordLoop(int estimatedTokens = 2000)
    {
        var today = DateTime.UtcNow.Date;
        var usage = GetOrCreateDailyUsage(today);

        lock (_lock)
        {
            usage.LoopCount++;
            usage.EstimatedTokens += estimatedTokens;
            usage.LastUpdate = DateTime.UtcNow;
        }

        var estimatedCost = (usage.EstimatedTokens / 1_000_000.0) * 0.15;

        _logger.LogDebug(
            "Loop recorded - Today: {Loops} loops, ~{Tokens:N0} tokens, ~${Cost:F4}",
            usage.LoopCount,
            usage.EstimatedTokens,
            estimatedCost);

        // Log warning when approaching limits
        if (usage.LoopCount >= _maxLoopsPerDay * 0.9)
        {
            _logger.LogWarning(
                "Approaching loop limit: {Current}/{Max} ({Percent:F1}%)",
                usage.LoopCount,
                _maxLoopsPerDay,
                (usage.LoopCount * 100.0 / _maxLoopsPerDay));
        }

        if (usage.EstimatedTokens >= _maxTokensPerDay * 0.9)
        {
            _logger.LogWarning(
                "Approaching token limit: {Current:N0}/{Max:N0} ({Percent:F1}%)",
                usage.EstimatedTokens,
                _maxTokensPerDay,
                (usage.EstimatedTokens * 100.0 / _maxTokensPerDay));
        }
    }

    /// <summary>
    /// Get current usage statistics for today.
    /// </summary>
    public UsageStats GetTodayStats()
    {
        var today = DateTime.UtcNow.Date;
        var usage = GetOrCreateDailyUsage(today);

        return new UsageStats
        {
            Date = today,
            LoopCount = usage.LoopCount,
            EstimatedTokens = usage.EstimatedTokens,
            EstimatedCost = (usage.EstimatedTokens / 1_000_000.0) * 0.15,
            MaxLoops = _maxLoopsPerDay,
            MaxTokens = _maxTokensPerDay,
            LoopPercentage = (usage.LoopCount * 100.0 / _maxLoopsPerDay),
            TokenPercentage = (usage.EstimatedTokens * 100.0 / _maxTokensPerDay)
        };
    }

    /// <summary>
    /// Reset usage for testing purposes.
    /// </summary>
    public void ResetToday()
    {
        var today = DateTime.UtcNow.Date;
        _dailyUsage.TryRemove(today, out _);
        _logger.LogWarning("Usage reset for {Date}", today);
    }

    private DailyUsage GetOrCreateDailyUsage(DateTime date)
    {
        // Clean up old entries (keep last 7 days)
        CleanupOldEntries(date);

        return _dailyUsage.GetOrAdd(date, _ => new DailyUsage { Date = date });
    }

    private void CleanupOldEntries(DateTime currentDate)
    {
        var cutoffDate = currentDate.AddDays(-7);
        var oldKeys = _dailyUsage.Keys.Where(k => k < cutoffDate).ToList();

        foreach (var key in oldKeys)
        {
            _dailyUsage.TryRemove(key, out _);
        }
    }

    private class DailyUsage
    {
        public DateTime Date { get; set; }
        public int LoopCount { get; set; }
        public int EstimatedTokens { get; set; }
        public DateTime LastUpdate { get; set; }
    }
}

public class UsageStats
{
    public DateTime Date { get; set; }
    public int LoopCount { get; set; }
    public int EstimatedTokens { get; set; }
    public double EstimatedCost { get; set; }
    public int MaxLoops { get; set; }
    public int MaxTokens { get; set; }
    public double LoopPercentage { get; set; }
    public double TokenPercentage { get; set; }

    public override string ToString()
    {
        return $"Usage for {Date:yyyy-MM-dd}: {LoopCount}/{MaxLoops} loops ({LoopPercentage:F1}%), " +
               $"~{EstimatedTokens:N0}/{MaxTokens:N0} tokens ({TokenPercentage:F1}%), " +
               $"~${EstimatedCost:F4} cost";
    }
}
