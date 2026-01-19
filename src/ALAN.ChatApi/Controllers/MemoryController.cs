using ALAN.Shared.Models;
using ALAN.Shared.Services.Memory;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.ChatApi.Controllers;

/// <summary>
/// API endpoints for searching and querying the agent's memory.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IVectorMemoryService? _vectorMemory;
    private readonly ILongTermMemoryService _longTermMemory;
    private readonly ILogger<MemoryController> _logger;

    public MemoryController(
        ILongTermMemoryService longTermMemory,
        ILogger<MemoryController> logger,
        IVectorMemoryService? vectorMemory = null)
    {
        _longTermMemory = longTermMemory;
        _logger = logger;
        _vectorMemory = vectorMemory;
    }

    /// <summary>
    /// Searches for memories semantically similar to the query.
    /// Uses vector search if available, otherwise falls back to text search.
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchMemories(
        [FromQuery] string query,
        [FromQuery] int maxResults = 10,
        [FromQuery] double minScore = 0.7,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Query parameter is required" });
        }

        try
        {
            if (_vectorMemory != null)
            {
                // Use vector search for semantic similarity
                var results = await _vectorMemory.SearchMemoriesAsync(
                    query,
                    maxResults,
                    minScore,
                    cancellationToken: cancellationToken);

                return Ok(new
                {
                    query,
                    method = "vector",
                    count = results.Count,
                    results = results.Select(r => new
                    {
                        r.Memory.Id,
                        r.Memory.Type,
                        r.Memory.Summary,
                        r.Memory.Timestamp,
                        r.Memory.Importance,
                        r.Memory.Tags,
                        r.Score
                    })
                });
            }
            else
            {
                // Fallback to text search
                var results = await _longTermMemory.SearchMemoriesAsync(query, maxResults, cancellationToken);

                return Ok(new
                {
                    query,
                    method = "text",
                    count = results.Count,
                    results = results.Select(m => new
                    {
                        m.Id,
                        m.Type,
                        m.Summary,
                        m.Timestamp,
                        m.Importance,
                        m.Tags,
                        Score = 0.0 // Text search doesn't provide scores
                    })
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to search memories for query: {Query}", query);
            return StatusCode(500, new { error = "Failed to search memories", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets statistics about the memory system.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetMemoryStats(CancellationToken cancellationToken = default)
    {
        try
        {
            var longTermCount = await _longTermMemory.GetMemoryCountAsync(cancellationToken);
            var vectorCount = _vectorMemory != null ? await _vectorMemory.GetMemoryCountAsync(cancellationToken) : 0;

            return Ok(new
            {
                longTermMemoryCount = longTermCount,
                vectorMemoryCount = vectorCount,
                vectorSearchEnabled = _vectorMemory != null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory stats");
            return StatusCode(500, new { error = "Failed to get memory stats", message = ex.Message });
        }
    }

    /// <summary>
    /// Gets a specific memory by ID.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetMemory(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            MemoryEntry? memory = null;

            if (_vectorMemory != null)
            {
                // Try both collections
                memory = await _vectorMemory.GetMemoryAsync(id, "short-term", cancellationToken)
                    ?? await _vectorMemory.GetMemoryAsync(id, "long-term", cancellationToken);
            }
            else
            {
                memory = await _longTermMemory.GetMemoryAsync(id, cancellationToken);
            }

            if (memory == null)
            {
                return NotFound(new { error = "Memory not found", id });
            }

            return Ok(memory);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get memory {Id}", id);
            return StatusCode(500, new { error = "Failed to get memory", message = ex.Message });
        }
    }
}
