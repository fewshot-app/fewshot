using Apex.Core.Enums;
using Apex.Core.Interfaces;
using Apex.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Apex.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExperimentsController : ControllerBase
{
    private readonly IExperimentService _experiments;

    public ExperimentsController(IExperimentService experiments) => _experiments = experiments;

    [HttpPost]
    public async Task<ActionResult<Experiment>> Create([FromBody] CreateExperimentRequest req)
        => Ok(await _experiments.CreateAsync(req.Name, req.Tier, req.TargetSessions));

    [HttpGet("results")]
    public async Task<ActionResult<List<ExperimentResultSummary>>> GetResults()
        => Ok(await _experiments.GetResultsAsync());

    [HttpGet("tokens")]
    public async Task<ActionResult<List<ExperimentTokenSummary>>> GetTokenResults()
        => Ok(await _experiments.GetTokenResultsAsync());

    [HttpGet("verdicts")]
    public async Task<ActionResult<List<ExperimentVerdict>>> GetVerdicts()
        => Ok(await _experiments.GetVerdictsAsync());

    [HttpPost("{id}/conclude")]
    public async Task<IActionResult> Conclude(int id, [FromBody] ConcludeRequest req)
    {
        await _experiments.ConcludeAsync(id, req.Winner, req.Conclusion);
        return NoContent();
    }

    [HttpPost("{id}/pause")]
    public async Task<IActionResult> Pause(int id)
    {
        await _experiments.PauseAsync(id);
        return NoContent();
    }

    [HttpPost("{id}/resume")]
    public async Task<IActionResult> Resume(int id)
    {
        await _experiments.ResumeAsync(id);
        return NoContent();
    }
}

public record CreateExperimentRequest(string Name, ContextTier Tier, int TargetSessions = 60);
public record ConcludeRequest(ContextFormat Winner, string Conclusion);
