# ALAN Cost Control Mechanism

## Overview

To prevent excessive API costs, ALAN implements a multi-layered usage tracking and throttling system that enforces daily limits on autonomous agent operations.

## Cost Calculations

### Target Budget

- **Daily Budget**: $2.00/day
- **Model**: gpt-4o-mini
- **Cost Rate**: $0.15 per 1M tokens

### Token Budget

```
Maximum tokens/day = $2.00 / $0.15 per 1M tokens = 13.33M tokens/day
```

### Safety Margin

To account for variability in token usage and ensure we stay well under budget:

- **Applied margin**: 40% buffer
- **Effective token limit**: 8M tokens/day (~$1.20/day)

### Loop Calculations

Each agent loop (think-act cycle) consists of:

- Prompt construction: ~500 tokens
- AI response: ~1000-1500 tokens
- **Average per loop**: ~2000 tokens

```
Maximum loops/day = 8M tokens / 2K tokens per loop = 4,000 loops/day
```

## Configuration

### Environment Variables

```bash
# Maximum number of agent loops per day
AGENT_MAX_LOOPS_PER_DAY="4000"

# Maximum tokens consumed per day
AGENT_MAX_TOKENS_PER_DAY="8000000"
```

These defaults ensure:

- ✅ Daily cost stays under $2.00
- ✅ 40% safety buffer for token variance
- ✅ ~4,000 agent thoughts/actions per day
- ✅ Estimated actual cost: ~$1.20/day

## Implementation

### UsageTracker Service

The `UsageTracker` class (`Services/UsageTracker.cs`) provides:

1. **Pre-execution checks**: Validates if another loop can run
2. **Loop recording**: Tracks each execution with estimated tokens
3. **Daily statistics**: Provides real-time usage metrics
4. **Automatic cleanup**: Maintains 7-day history

### Throttling Behavior

When limits are reached:

1. Agent status changes to `Throttled`
2. Exponential backoff applied (1min → 2min → 4min → ... up to 60min)
3. Automatic retry after wait period
4. Detailed logging of throttle reasons

### Warning System

Proactive warnings at:

- 90% of loop limit
- 90% of token limit

## Monitoring

### Log Output

The agent logs comprehensive usage information:

```
[Information] UsageTracker initialized - Max loops/day: 4000, Max tokens/day: 8000000 (~$1.2/day)
[Information] Today's usage: Usage for 2025-12-08: 150/4000 loops (3.8%), ~300000/8000000 tokens (3.8%), ~$0.0450 cost
[Warning] Approaching loop limit: 3600/4000 (90.0%)
[Warning] Agent throttled: Daily loop limit reached (4000/4000)
```

### Usage Stats API

Access current usage via the `UsageTracker.GetTodayStats()` method:

```csharp
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
}
```

## Adjusting Limits

### Increase Budget

To increase to $5/day:

```bash
AGENT_MAX_LOOPS_PER_DAY="10000"
AGENT_MAX_TOKENS_PER_DAY="20000000"
```

### Decrease Budget

To decrease to $1/day:

```bash
AGENT_MAX_LOOPS_PER_DAY="2000"
AGENT_MAX_TOKENS_PER_DAY="4000000"
```

### Conservative Mode

For minimal testing ($0.50/day):

```bash
AGENT_MAX_LOOPS_PER_DAY="500"
AGENT_MAX_TOKENS_PER_DAY="1000000"
```

## Best Practices

1. **Monitor logs**: Watch for warning messages about approaching limits
2. **Adjust think interval**: Increase `AGENT_THINK_INTERVAL` to reduce frequency
3. **Test conservatively**: Start with lower limits when testing new features
4. **Review actual usage**: Check Azure OpenAI usage metrics to validate estimates
5. **Update token estimates**: If actual usage differs significantly, adjust the 2K token estimate in `RecordLoop()`

## Future Enhancements

Potential improvements to the cost control system:

- [ ] Real-time token counting via API response metadata
- [ ] Per-hour rate limiting
- [ ] Web UI dashboard for usage visualization
- [ ] Alerts/notifications when approaching limits
- [ ] Dynamic adjustment based on actual measured costs
- [ ] Integration with Azure Cost Management API
