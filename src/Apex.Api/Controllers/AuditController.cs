using Apex.Core.Interfaces;
using Apex.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly IAuditService _audit;

    public AuditController(IAuditService audit) => _audit = audit;

    /// <summary>
    /// Test the three-stage audit pipeline against arbitrary content.
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<AuditPipelineResult>> Analyze([FromBody] AuditRequest req)
    {
        var result = await _audit.AnalyzeAsync(req.Content, req.SessionId ?? 0);
        return Ok(result);
    }
}

public record AuditRequest(string Content, int? SessionId = null);
