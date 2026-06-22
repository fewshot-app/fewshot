using Apex.Api.Jobs;
using Apex.Core.Interfaces;
using Apex.Core.Models;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SessionsController : ControllerBase
{
    private readonly ISessionService _sessions;
    private readonly IBackgroundJobClient _backgroundJobs;

    public SessionsController(ISessionService sessions, IBackgroundJobClient backgroundJobs)
    {
        _sessions = sessions;
        _backgroundJobs = backgroundJobs;
    }

    [HttpPost]
    public async Task<ActionResult<Session>> Start()
        => Ok(await _sessions.StartSessionAsync());

    [HttpPost("{id}/end")]
    public async Task<IActionResult> End(int id)
    {
        await _sessions.EndSessionAsync(id);

        // Fire-and-forget: collect experiment metrics for this session
        _backgroundJobs.Enqueue<ExperimentMetricsJob>(job => job.CollectAsync(id));

        return NoContent();
    }

    [HttpGet]
    public async Task<ActionResult<List<Session>>> GetAll()
        => Ok(await _sessions.GetAllSessionsAsync());

    [HttpGet("{id}")]
    public async Task<ActionResult<Session>> Get(int id)
    {
        var session = await _sessions.GetSessionAsync(id);
        return session is null ? NotFound() : Ok(session);
    }

    [HttpGet("active")]
    public async Task<ActionResult<Session>> GetActive()
    {
        var session = await _sessions.GetActiveSessionAsync();
        return session is null ? NotFound() : Ok(session);
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<Session>> Patch(int id, [FromBody] SessionPatchRequest req)
    {
        var session = await _sessions.GetSessionAsync(id);
        if (session is null) return NotFound();

        if (req.Project is not null) session.Project = req.Project;
        if (req.EndTime.HasValue) session.EndTime = req.EndTime.Value;
        if (req.ClearError) session.ConsolidationError = null;
        await _sessions.UpdateSessionAsync(session);
        return Ok(session);
    }
}

public record SessionPatchRequest(string? Project = null, DateTime? EndTime = null, bool ClearError = false);
