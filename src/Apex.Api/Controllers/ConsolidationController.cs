using Apex.Api.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConsolidationController : ControllerBase
{
    private readonly IBackgroundJobClient _jobs;

    public ConsolidationController(IBackgroundJobClient jobs) => _jobs = jobs;

    /// <summary>
    /// Trigger consolidation for a specific session (for testing).
    /// </summary>
    [HttpPost("{sessionId}")]
    public IActionResult TriggerSingle(int sessionId)
    {
        var jobId = _jobs.Enqueue<ConsolidationJob>(job => job.ConsolidateSingleAsync(sessionId));
        return Ok(new { jobId, sessionId, status = "Enqueued" });
    }

    /// <summary>
    /// Trigger the full nightly consolidation run (processes all unconsolidated sessions).
    /// </summary>
    [HttpPost("run-all")]
    public IActionResult TriggerAll()
    {
        var jobId = _jobs.Enqueue<ConsolidationJob>(job => job.RunAsync());
        return Ok(new { jobId, status = "Enqueued" });
    }
}
