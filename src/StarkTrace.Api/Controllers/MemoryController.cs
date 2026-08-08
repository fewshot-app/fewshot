using StarkTrace.Core.Interfaces;
using StarkTrace.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace StarkTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MemoryController : ControllerBase
{
    private readonly IMemoryService _memory;

    public MemoryController(IMemoryService memory) => _memory = memory;

    /// <summary>
    /// Store a new semantic memory. Returns null if quality gate rejects it.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<StoredMemory>> Store([FromBody] MemoryStoreRequest req)
    {
        var result = await _memory.StoreAsync(req);
        if (result == null)
            return UnprocessableEntity(new { error = "Memory rejected by quality gate" });
        return CreatedAtAction(nameof(Get), new { pointId = result.PointId }, result);
    }

    /// <summary>
    /// Search for relevant memories given a query string.
    /// </summary>
    [HttpPost("search")]
    public async Task<ActionResult<List<SemanticMemory>>> Search([FromBody] MemorySearchRequest req)
    {
        var results = await _memory.SearchAsync(
            req.Query,
            req.SessionId,
            req.Limit ?? 5,
            req.MinScore ?? 0.72);
        return Ok(results);
    }

    /// <summary>
    /// Retrieve a specific memory by its Qdrant point ID.
    /// </summary>
    [HttpGet("{pointId}")]
    public async Task<ActionResult<StoredMemory>> Get(string pointId)
    {
        var result = await _memory.GetAsync(pointId);
        return result is null ? NotFound() : Ok(result);
    }

    /// <summary>
    /// Delete a memory by its Qdrant point ID.
    /// </summary>
    [HttpDelete("{pointId}")]
    public async Task<IActionResult> Delete(string pointId)
    {
        var success = await _memory.DeleteAsync(pointId);
        return success ? NoContent() : NotFound();
    }

    /// <summary>
    /// Get all memories for a given session.
    /// </summary>
    [HttpGet("session/{sessionId}")]
    public async Task<ActionResult<List<StoredMemory>>> GetBySession(int sessionId)
    {
        var results = await _memory.GetBySessionAsync(sessionId);
        return Ok(results);
    }

    /// <summary>
    /// Check if a summary would be considered a duplicate.
    /// </summary>
    [HttpPost("check-duplicate")]
    public async Task<ActionResult<object>> CheckDuplicate([FromBody] DuplicateCheckRequest req)
    {
        var isDuplicate = await _memory.IsDuplicateAsync(req.Summary);
        return Ok(new { isDuplicate });
    }
}

public record MemorySearchRequest(string Query, int SessionId, int? Limit = 5, double? MinScore = 0.55);
public record DuplicateCheckRequest(string Summary);
