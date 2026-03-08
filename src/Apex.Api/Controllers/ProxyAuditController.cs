using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apex.Api.Controllers;

/// <summary>
/// Receives audit events from Apex.Proxy (and potentially other sources).
/// Logged separately from the main AuditLog table so proxy findings are clearly attributed.
/// </summary>
[ApiController]
[Route("api/proxy-audit")]
public class ProxyAuditController : ControllerBase
{
    private readonly ApexDbContext _db;
    private readonly ILogger<ProxyAuditController> _logger;

    public ProxyAuditController(ApexDbContext db, ILogger<ProxyAuditController> logger)
    {
        _db = db;
        _logger = logger;
    }

    [HttpPost("log")]
    public async Task<IActionResult> Log([FromBody] ProxyAuditEvent evt, CancellationToken ct)
    {
        if (evt is null) return BadRequest();

        _logger.LogInformation("[PROXY-AUDIT] {Direction} {Method} — {Count} finding(s): {Types}",
            evt.Direction, evt.Method, evt.Findings.Count,
            string.Join(", ", evt.Findings.Select(f => f.Type).Distinct()));

        _db.ProxyAuditLogs.Add(new ProxyAuditLog
        {
            Direction = evt.Direction ?? "unknown",
            Method = evt.Method ?? "unknown",
            FindingTypes = string.Join(",", evt.Findings.Select(f => f.Type).Distinct()),
            FindingCount = evt.Findings.Count,
            MaxConfidence = evt.Findings.Count > 0 ? evt.Findings.Max(f => f.Confidence) : 0,
            WasRedacted = evt.Findings.Any(f =>
                f.Confidence >= 0.9 &&
                new[] { "SSN", "PrivateKey", "ConnectionString", "BearerToken", "JwtToken", "CreditCard" }
                    .Contains(f.Type)),
            Snippet = evt.Snippet?[..Math.Min(500, evt.Snippet.Length)],
            Source = evt.Source ?? "apex-proxy",
            Timestamp = evt.Timestamp ?? DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpGet("logs")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool redactedOnly = false,
        CancellationToken ct = default)
    {
        var query = _db.ProxyAuditLogs.AsQueryable();
        if (redactedOnly) query = query.Where(x => x.WasRedacted);

        var total = await query.CountAsync(ct);
        var logs = await query
            .OrderByDescending(x => x.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, logs });
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var logs = _db.ProxyAuditLogs;
        var stats = new
        {
            Total = await logs.CountAsync(ct),
            Redacted = await logs.CountAsync(x => x.WasRedacted, ct),
            LastDay = await logs.CountAsync(x => x.Timestamp >= DateTime.UtcNow.AddDays(-1), ct),
            TopTypes = await logs
                .GroupBy(x => x.FindingTypes)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync(ct)
        };
        return Ok(stats);
    }
}

public class ProxyAuditEvent
{
    public string? Source { get; set; }
    public string? Direction { get; set; }
    public string? Method { get; set; }
    public List<ProxyFindingDto> Findings { get; set; } = [];
    public string? Snippet { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class ProxyFindingDto
{
    public string Type { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public double Confidence { get; set; }
}
