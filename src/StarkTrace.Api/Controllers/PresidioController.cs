using StarkTrace.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace StarkTrace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PresidioController : ControllerBase
{
    private readonly PresidioProcessManager _presidio;

    public PresidioController(PresidioProcessManager presidio) => _presidio = presidio;

    [HttpGet("status")]
    public ActionResult<PresidioStatusDto> GetStatus() => Ok(new PresidioStatusDto(
        Status: _presidio.Status.ToString(),
        Pid: _presidio.Pid,
        RestartCount: _presidio.RestartCount,
        UptimeSeconds: _presidio.StartedAt.HasValue
            ? (int)(DateTime.UtcNow - _presidio.StartedAt.Value).TotalSeconds
            : null
    ));

    [HttpPost("start")]
    public async Task<IActionResult> Start()
    {
        await _presidio.StartAsync();
        return Ok();
    }

    [HttpPost("stop")]
    public async Task<IActionResult> Stop()
    {
        await _presidio.StopAsync();
        return Ok();
    }

    [HttpPost("restart")]
    public async Task<IActionResult> Restart()
    {
        await _presidio.RestartAsync();
        return Ok();
    }
}

public record PresidioStatusDto(string Status, int? Pid, int RestartCount, int? UptimeSeconds);
