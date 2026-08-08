using Fewshot.Core.Models;
using Fewshot.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Fewshot.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly FewshotDbContext _db;
    public ProjectsController(FewshotDbContext db) => _db = db;

    [HttpGet]
    public async Task<List<Project>> GetAll() =>
        await _db.Projects.OrderBy(p => p.DisplayName).ToListAsync();

    [HttpGet("{id}")]
    public async Task<ActionResult<Project>> Get(int id)
    {
        var p = await _db.Projects.FindAsync(id);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost]
    public async Task<ActionResult<Project>> Create([FromBody] ProjectUpsertRequest req)
    {
        var project = new Project
        {
            Name = req.Name.ToLowerInvariant().Trim(),
            DisplayName = req.DisplayName,
            Keywords = req.Keywords,
            Facts = req.Facts,
            IsActive = true
        };
        _db.Projects.Add(project);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = project.ProjectId }, project);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Project>> Update(int id, [FromBody] ProjectUpsertRequest req)
    {
        var project = await _db.Projects.FindAsync(id);
        if (project is null) return NotFound();

        project.DisplayName = req.DisplayName;
        project.Keywords = req.Keywords;
        project.Facts = req.Facts;
        project.IsActive = req.IsActive;
        await _db.SaveChangesAsync();
        return Ok(project);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _db.Projects.Where(p => p.ProjectId == id).ExecuteDeleteAsync();
        return NoContent();
    }

    [HttpPost("session")]
    public async Task<ActionResult<object>> GetOrCreateSession(
        [FromBody] SessionRequest req,
        [FromServices] Core.Interfaces.IProjectSessionService svc)
    {
        var project = await svc.ResolveProjectAsync(req.Project);
        var (sessionId, isNew) = await svc.GetOrCreateAsync(project);
        return Ok(new { sessionId, isNew, project });
    }

    [HttpPost("session/close")]
    public async Task<IActionResult> CloseSession(
        [FromBody] SessionRequest req,
        [FromServices] Core.Interfaces.IProjectSessionService svc)
    {
        await svc.CloseSessionAsync(req.Project);
        return NoContent();
    }

    [HttpPost("resolve")]
    public async Task<ActionResult<string>> Resolve([FromBody] ResolveRequest req,
        [FromServices] Core.Interfaces.IProjectSessionService svc) =>
        Ok(await svc.ResolveProjectAsync(req.Hint));
}

public record ProjectUpsertRequest(string Name, string DisplayName, string Keywords, string? Facts, bool IsActive = true);
public record ResolveRequest(string Hint);
public record SessionRequest(string Project);
