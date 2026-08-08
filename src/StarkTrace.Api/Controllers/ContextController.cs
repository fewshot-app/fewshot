using StarkTrace.Core.Interfaces;
using StarkTrace.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace StarkTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContextController : ControllerBase
{
    private readonly IContextInjector _injector;

    public ContextController(IContextInjector injector) => _injector = injector;

    /// <summary>
    /// Build context from fully pre-populated inputs (for testing/comparison).
    /// </summary>
    [HttpPost("build")]
    public async Task<ActionResult<ContextInjectionResult>> Build([FromBody] BuildContextRequest req)
    {
        var result = await _injector.BuildContextAsync(req.SessionId, req.Inputs);
        return Ok(result);
    }

    /// <summary>
    /// Build context by auto-hydrating P2-P4 from Qdrant + SQL.
    /// Only requires session state and optional project facts.
    /// This is the primary endpoint for real usage.
    /// </summary>
    [HttpPost("auto")]
    public async Task<ActionResult<ContextInjectionResult>> Auto([FromBody] AutoContextRequest req)
    {
        try
        {
            var result = await _injector.BuildContextAutoAsync(req.SessionId, req.State, req.Facts);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace, inner = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Preview context build (same as build, for dashboard inspection).
    /// </summary>
    [HttpPost("preview")]
    public async Task<ActionResult<ContextInjectionResult>> Preview([FromBody] BuildContextRequest req)
    {
        var result = await _injector.BuildContextAsync(req.SessionId, req.Inputs);
        return Ok(result);
    }
}

public record BuildContextRequest(int SessionId, ContextInputs Inputs);

public record AutoContextRequest(
    int SessionId,
    CurrentStateContext State,
    ProjectFacts? Facts = null);
