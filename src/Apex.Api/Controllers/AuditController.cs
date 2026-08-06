using Apex.Core.Interfaces;
using Apex.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _audit;
    private readonly IAuditAllowlistProvider _allowlist;

    public AuditController(IAuditService audit, IAuditAllowlistProvider allowlist)
    {
        _audit = audit;
        _allowlist = allowlist;
    }

    /// <summary>
    /// Test the three-stage audit pipeline against arbitrary content.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<AuditPipelineResult>> Analyze([FromBody] AuditRequest req)
    {
        var result = await _audit.AnalyzeAsync(req.Content, req.SessionId ?? 0);
        return Ok(result);
    }

    /// <summary>
    /// Current allowlist regex patterns.
    /// </summary>
    [HttpGet("allowlist")]
    public async Task<ActionResult<List<string>>> GetAllowlist()
    {
        var patterns = await _allowlist.GetPatternsAsync();
        return Ok(patterns);
    }

    /// <summary>
    /// Replace the full allowlist. Every pattern must be a valid regex; nothing is
    /// persisted if any pattern is invalid.
    /// </summary>
    [HttpPut("allowlist")]
    public async Task<ActionResult<List<string>>> UpdateAllowlist([FromBody] AllowlistUpdateRequest req)
    {
        try
        {
            await _allowlist.SetPatternsAsync(req.Patterns ?? []);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var patterns = await _allowlist.GetPatternsAsync();
        return Ok(patterns);
    }
}

public record AuditRequest(string Content, int? SessionId = null);
public record AllowlistUpdateRequest(List<string>? Patterns);
