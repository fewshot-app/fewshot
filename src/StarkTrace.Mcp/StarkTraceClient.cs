using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace StarkTrace.Mcp;

public class StarkTraceClient
{
    private readonly IHttpClientFactory _factory;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public StarkTraceClient(IHttpClientFactory factory) => _factory = factory;

    private HttpClient Http => _factory.CreateClient("StarkTrace");

    // ── Projects ──────────────────────────────────────────────────────────────

    public async Task<string> ResolveProjectAsync(string hint)
    {
        var resp = await Http.PostAsJsonAsync("/api/projects/resolve", new { hint });
        if (!resp.IsSuccessStatusCode) return "general";
        var raw = await resp.Content.ReadAsStringAsync();
        var trimmed = raw.Trim('"', ' ', '\t', '\r', '\n');
        return string.IsNullOrEmpty(trimmed) ? "general" : trimmed;
    }

    public async Task<List<ProjectDto>> GetProjectsAsync()
    {
        var resp = await Http.GetFromJsonAsync<List<ProjectDto>>("/api/projects", _json);
        return resp ?? [];
    }

    public async Task<string> AddProjectAsync(string name, string displayName, string keywords)
    {
        var resp = await Http.PostAsJsonAsync("/api/projects", new
        {
            name = name.ToLowerInvariant().Trim(),
            displayName,
            keywords,
            facts = (string?)null,
            isActive = true
        });
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) return $"Failed to add project ({resp.StatusCode}): {body}";
        try
        {
            var p = JsonSerializer.Deserialize<ProjectDto>(body, _json);
            return p is null ? "Created but could not read response" : $"Created project '{p.Name}' (ID {p.ProjectId})";
        }
        catch (Exception ex)
        {
            return $"Project created but response parse failed: {ex.Message}. Raw: {body[..Math.Min(200, body.Length)]}";
        }
    }

    public async Task<string> UpdateProjectAsync(string name, string? displayName, string? keywords, string? facts, bool? isActive)
    {
        var key = name.ToLowerInvariant().Trim();
        var projects = await GetProjectsAsync();
        var existing = projects.FirstOrDefault(p => p.Name == key);
        if (existing is null) return $"Project '{key}' not found.";

        var resp = await Http.PutAsJsonAsync($"/api/projects/{existing.ProjectId}", new
        {
            name = existing.Name,
            displayName = displayName ?? existing.DisplayName,
            keywords = keywords ?? existing.Keywords,
            facts = facts ?? existing.Facts,
            isActive = isActive ?? existing.IsActive
        });
        if (!resp.IsSuccessStatusCode) return $"Failed to update project: {resp.StatusCode}";
        var p = await resp.Content.ReadFromJsonAsync<ProjectDto>(_json);
        return p is null
            ? $"Updated project '{key}' but could not read response."
            : $"Updated project '{p.Name}' (ID {p.ProjectId}). Keywords: {p.Keywords}";
    }


    public async Task<string> RemoveProjectAsync(string name)
    {
        var projects = await GetProjectsAsync();
        var match = projects.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match is null) return $"No project found with name '{name}'. Use starktrace_list_projects to see available names.";
        var resp = await Http.DeleteAsync($"/api/projects/{match.ProjectId}");
        return resp.IsSuccessStatusCode
            ? $"Removed project '{match.Name}' (ID {match.ProjectId})"
            : $"Failed to remove: {resp.StatusCode}";
    }

    // ── Sessions ──────────────────────────────────────────────────────────────

    public async Task<ProjectSessionDto> GetOrCreateSessionAsync(string project)
    {
        var resp = await Http.PostAsJsonAsync("/api/projects/session", new { project });
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<ProjectSessionDto>(_json))!;
    }

    public async Task CloseSessionAsync(string project)
    {
        var resp = await Http.PostAsJsonAsync("/api/projects/session/close", new { project });
        resp.EnsureSuccessStatusCode();
    }

    public async Task<string> TriggerConsolidationAsync()
    {
        var resp = await Http.PostAsync("/api/consolidation/run-all", null);
        if (!resp.IsSuccessStatusCode)
            return $"Failed to trigger consolidation: {resp.StatusCode}";
        var result = await resp.Content.ReadAsStringAsync();
        return result;
    }

    // ── Context ───────────────────────────────────────────────────────────────

    public async Task<string> GetContextAsync(int sessionId, string project, string? topic)
    {
        var state = new
        {
            project,
            environment = "development",
            sprintItems = Array.Empty<object>(),
            recentErrors = Array.Empty<object>()
        };

        var resp = await Http.PostAsJsonAsync("/api/context/auto", new
        {
            sessionId,
            state,
            facts = (object?)null
        });

        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync();
            return $"[StarkTrace] Context unavailable — {resp.StatusCode}: {Truncate(detail, 500)}";
        }

        var result = await resp.Content.ReadFromJsonAsync<ContextResult>(_json);
        return result?.AssembledContext ?? "[StarkTrace] Empty context returned";
    }

    // ── Messages ──────────────────────────────────────────────────────────────

    public async Task RecordMessageAsync(int sessionId, string role, string content)
    {
        var resp = await Http.PostAsJsonAsync("/api/messages", new { sessionId, role, content });
        resp.EnsureSuccessStatusCode();
    }

    // ── Audit / Scan ──────────────────────────────────────────────────────────

    public async Task<ScanResult> ScanContentAsync(string content)
    {
        var resp = await Http.PostAsJsonAsync("/api/audit/analyze", new { content, sessionId = (int?)null });
        if (!resp.IsSuccessStatusCode)
            return new ScanResult(true, false, null, []);

        var result = await resp.Content.ReadFromJsonAsync<AuditResponse>(_json);
        if (result is null)
            return new ScanResult(true, false, null, []);

        return new ScanResult(result.IsSafe, result.RequiresReview, result.RedactedContent, result.Findings);
    }

    // ── Memory ────────────────────────────────────────────────────────────────

    public async Task<string> SearchMemoryAsync(string query, int topK = 5)
    {
        var resp = await Http.PostAsJsonAsync("/api/memory/search", new { query, topK, minScore = 0.55 });
        if (!resp.IsSuccessStatusCode)
        {
            var detail = await resp.Content.ReadAsStringAsync();
            return $"[StarkTrace] Memory search failed — {resp.StatusCode}: {Truncate(detail, 500)}";
        }
        var memories = await resp.Content.ReadFromJsonAsync<List<MemoryResult>>(_json);
        if (memories is null || memories.Count == 0) return "No relevant memories found.";
        return string.Join("\n\n", memories.Select((m, i) =>
            $"[{i + 1}] ({m.RelevanceScore:F2}) {m.Summary}" +
            (m.Solution != null ? $"\nSolution: {m.Solution}" : "") +
            (m.Tags != null ? $"\nTags: {m.Tags}" : "")));
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}

// DTOs
public record ProjectDto(int ProjectId, string Name, string DisplayName, string Keywords, string? Facts = null, bool IsActive = true);
public record ProjectSessionDto(int SessionId, bool IsNew, string Project);
public record ContextResult(string AssembledContext, int TotalTokens);
public record MemoryResult(string Summary, string? Solution, string? Tags, double RelevanceScore);

// Audit / Scan DTOs
public record ScanResult(bool IsSafe, bool RequiresReview, string? RedactedContent, List<ScanFinding> Findings);
public record ScanFinding(string DetectedType, string Stage, double Confidence);
public record AuditResponse
{
    public bool IsSafe { get; set; }
    public bool RequiresReview { get; set; }
    public string? RedactedContent { get; set; }
    public List<ScanFinding> Findings { get; set; } = [];
}
