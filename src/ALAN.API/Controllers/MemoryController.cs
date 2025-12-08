using ALAN.Core.Memory;
using Microsoft.AspNetCore.Mvc;

namespace ALAN.API.Controllers;

/// <summary>
/// Controller for querying agent memory
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IMemoryService _shortTermMemory;
    private readonly IMemoryService _longTermMemory;
    private readonly ILogger<MemoryController> _logger;

    public MemoryController(
        IEnumerable<IMemoryService> memoryServices,
        ILogger<MemoryController> logger)
    {
        var services = memoryServices.ToList();
        _shortTermMemory = services.First();
        _longTermMemory = services.Skip(1).FirstOrDefault() ?? services.First();
        _logger = logger;
    }

    /// <summary>
    /// Get recent short-term memories
    /// </summary>
    [HttpGet("short-term")]
    public async Task<IActionResult> GetShortTerm([FromQuery] int limit = 20)
    {
        try
        {
            var memories = await _shortTermMemory.RetrieveAsync(MemoryType.ShortTerm, limit);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving short-term memory");
            return StatusCode(500, new { error = "Failed to retrieve memories" });
        }
    }

    /// <summary>
    /// Get long-term memories
    /// </summary>
    [HttpGet("long-term")]
    public async Task<IActionResult> GetLongTerm([FromQuery] int limit = 50)
    {
        try
        {
            var memories = await _longTermMemory.RetrieveAsync(MemoryType.LongTerm, limit);
            return Ok(memories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving long-term memory");
            return StatusCode(500, new { error = "Failed to retrieve memories" });
        }
    }

    /// <summary>
    /// Get learnings
    /// </summary>
    [HttpGet("learnings")]
    public async Task<IActionResult> GetLearnings([FromQuery] int limit = 10)
    {
        try
        {
            var learnings = await _longTermMemory.RetrieveAsync(MemoryType.Learning, limit);
            return Ok(learnings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving learnings");
            return StatusCode(500, new { error = "Failed to retrieve learnings" });
        }
    }

    /// <summary>
    /// Search memories
    /// </summary>
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string query, [FromQuery] int limit = 20)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return BadRequest(new { error = "Query cannot be empty" });
        }

        try
        {
            var results = await _longTermMemory.SearchAsync(query, limit: limit);
            return Ok(results);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching memories");
            return StatusCode(500, new { error = "Failed to search memories" });
        }
    }
}
