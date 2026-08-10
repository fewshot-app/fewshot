using System.Text.Json;
using Fewshot.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Fewshot.Api.Controllers;

[ApiController]
[Route("api/pack-drafts")]
public class PackDraftsController : ControllerBase
{
    private static readonly JsonSerializerOptions Camel = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly string _draftsDir;

    public PackDraftsController()
    {
        _draftsDir = Path.Combine(
            Environment.ExpandEnvironmentVariables("%PROGRAMDATA%"), "Fewshot", "drafts");
        Directory.CreateDirectory(_draftsDir);
    }

    private string DraftPath(string id) =>
        Path.Combine(_draftsDir, string.Concat(id.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_')) + ".json");

    [HttpGet]
    public IActionResult List()
    {
        var drafts = Directory.EnumerateFiles(_draftsDir, "*.json")
            .Select(f =>
            {
                try
                {
                    var pack = JsonSerializer.Deserialize<FewshotPack>(System.IO.File.ReadAllText(f), Camel);
                    if (pack is null) return null;
                    return new
                    {
                        id = Path.GetFileNameWithoutExtension(f),
                        pack.PackId,
                        pack.Name,
                        pack.Version,
                        memories = pack.Memories.Count,
                        preferences = pack.Preferences.Count,
                        antiPatterns = pack.AntiPatterns.Count,
                        pending = pack.Memories.Count(m => m.ReviewStatus is null)
                                + pack.Preferences.Count(p => p.ReviewStatus is null)
                                + pack.AntiPatterns.Count(a => a.ReviewStatus is null),
                        updatedAt = System.IO.File.GetLastWriteTimeUtc(f)
                    };
                }
                catch { return null; }
            })
            .Where(d => d is not null)
            .OrderByDescending(d => d!.updatedAt);
        return Ok(drafts);
    }

    [HttpPost]
    public async Task<IActionResult> Upload([FromBody] FewshotPack pack)
    {
        if (string.IsNullOrWhiteSpace(pack.PackId)) return BadRequest("packId is required");
        var path = DraftPath(pack.PackId);
        await System.IO.File.WriteAllTextAsync(path, JsonSerializer.Serialize(pack, Camel));
        return Ok(new { id = Path.GetFileNameWithoutExtension(path) });
    }

    [HttpGet("{id}")]
    public IActionResult Get(string id)
    {
        var path = DraftPath(id);
        if (!System.IO.File.Exists(path)) return NotFound();
        return Content(System.IO.File.ReadAllText(path), "application/json");
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Save(string id, [FromBody] FewshotPack pack)
    {
        var path = DraftPath(id);
        if (!System.IO.File.Exists(path)) return NotFound();
        await System.IO.File.WriteAllTextAsync(path, JsonSerializer.Serialize(pack, Camel));
        return Ok();
    }

    [HttpDelete("{id}")]
    public IActionResult Delete(string id)
    {
        var path = DraftPath(id);
        if (!System.IO.File.Exists(path)) return NotFound();
        System.IO.File.Delete(path);
        return Ok();
    }

    [HttpPost("{id}/finalize")]
    public IActionResult Finalize(string id)
    {
        var path = DraftPath(id);
        if (!System.IO.File.Exists(path)) return NotFound();
        var pack = JsonSerializer.Deserialize<FewshotPack>(System.IO.File.ReadAllText(path), Camel);
        if (pack is null) return UnprocessableEntity("draft is not a valid pack");

        pack.Memories = pack.Memories.Where(m => m.ReviewStatus == "approved").ToList();
        pack.Preferences = pack.Preferences.Where(p => p.ReviewStatus == "approved").ToList();
        pack.AntiPatterns = pack.AntiPatterns.Where(a => a.ReviewStatus == "approved").ToList();
        foreach (var m in pack.Memories) { m.ReviewStatus = null; m.OutcomeLabel = "success"; }
        foreach (var p in pack.Preferences) p.ReviewStatus = null;
        foreach (var a in pack.AntiPatterns) a.ReviewStatus = null;

        var baseVersion = pack.Version.Split('-')[0];
        var parts = baseVersion.Split('.');
        pack.Version = parts.Length == 3 && int.TryParse(parts[1], out var minor)
            ? $"{parts[0]}.{minor + 1}.0"
            : "1.0.0";

        return Ok(pack);
    }
}
