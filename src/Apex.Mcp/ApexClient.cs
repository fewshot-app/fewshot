using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Apex.Mcp;

public class ApexClient
{
    private readonly IHttpClientFactory _factory;
    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public ApexClient(IHttpClientFactory factory) => _factory = factory;

    private HttpClient Http => _factory.CreateClient("Apex");

    // ── Projects ──────────────────────────────────────────────────────────────

    public async Task<string> ResolveProjectAsync(string hint)
    {
        var resp = await Http.PostAsJsonAsync("/api/projects/resolve", new { hint });
        if (!resp.IsSuccessStatusCode) return "general";
        var raw = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<string>(raw, _json) ?? "general";
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
        if (!resp.IsSuccessStatusCode) return $"Failed to add project: {resp.StatusCode}";
        var p = await resp.Content.ReadFromJsonAsync<ProjectDto>(_json);
        return p is null ? "Created but could not read response" : $"Created project '{p.Name}' (ID {p.ProjectId})";
    }

    public async Task<string> RemoveProjectAsync(string name)
    {
        var projects = await GetProjectsAsync();
        var match = projects.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match is null) return $"No project found with name '{name}'. Use apex_list_projects to see available names.";
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
        await Http.PostAsJsonAsync("/api/projects/session/close", new { project });
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
            return $"[APEX] Context unavailable — {resp.StatusCode}";

        var result = await resp.Content.ReadFromJsonAsync<ContextResult>(_json);
        return result?.AssembledContext ?? "[APEX] Empty context returned";
    }

    // ── Messages ──────────────────────────────────────────────────────────────

    public async Task RecordMessageAsync(int sessionId, string role, string content)
    {
        await Http.PostAsJsonAsync("/api/messages", new { sessionId, role, content });
    }

    // ── Audit / Scan ──────────────────────────────────────────────────────────

    public async Task<ScanResult> ScanContentAsync(string content)
    {
        var resp = await Http.PostAsJsonAsync("/api/audit/analyze", new { content, sessionId = (int?)null });
        if (!resp.IsSuccessStatusCode)
            return new ScanResult(true, false, "Scan unavailable — APEX API returned " + resp.StatusCode, []);

        var result = await resp.Content.ReadFromJsonAsync<AuditResponse>(_json);
        if (result is null)
            return new ScanResult(true, false, "Scan returned empty response.", []);

        return new ScanResult(result.IsSafe, result.RequiresReview, result.RedactedContent, result.Findings);
    }

    // ── Memory ────────────────────────────────────────────────────────────────

    public async Task<string> SearchMemoryAsync(string query, int topK = 5)
    {
        var resp = await Http.PostAsJsonAsync("/api/memory/search", new { query, topK, minScore = 0.55 });
        if (!resp.IsSuccessStatusCode) return "No memories found.";
        var memories = await resp.Content.ReadFromJsonAsync<List<MemoryResult>>(_json);
        if (memories is null || memories.Count == 0) return "No relevant memories found.";
        return string.Join("\n\n", memories.Select((m, i) =>
            $"[{i + 1}] ({m.RelevanceScore:F2}) {m.Summary}" +
            (m.Solution != null ? $"\nSolution: {m.Solution}" : "") +
            (m.Tags != null ? $"\nTags: {m.Tags}" : "")));
    }
}

// DTOs
public record ProjectDto(int ProjectId, string Name, string DisplayName, string Keywords);
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
