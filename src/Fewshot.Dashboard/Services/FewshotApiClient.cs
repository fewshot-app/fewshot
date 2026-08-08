using System.Net.Http.Json;

namespace Fewshot.Dashboard.Services;

public class FewshotApiClient
{
    private readonly HttpClient _http;

    public FewshotApiClient(HttpClient http) => _http = http;

    // Health
    public async Task<HealthStatus?> GetHealthAsync() =>
        await _http.GetFromJsonAsync<HealthStatus>("/health");

    // Sessions
    public async Task<List<SessionDto>> GetSessionsAsync() =>
        await _http.GetFromJsonAsync<List<SessionDto>>("/api/sessions") ?? [];

    public async Task<SessionDto?> GetSessionAsync(int id) =>
        await _http.GetFromJsonAsync<SessionDto>($"/api/sessions/{id}");

    // Memory
    public async Task<List<MemoryDto>> GetMemoriesBySessionAsync(int sessionId) =>
        await _http.GetFromJsonAsync<List<MemoryDto>>($"/api/memory/session/{sessionId}") ?? [];

    public async Task<List<MemoryDto>> SearchMemoriesAsync(string query)
    {
        var resp = await _http.PostAsJsonAsync("/api/memory/search", new { query, sessionId = 0, minScore = 0.45, limit = 20 });
        return await resp.Content.ReadFromJsonAsync<List<MemoryDto>>() ?? [];
    }

    public async Task DeleteMemoryAsync(string pointId) =>
        await _http.DeleteAsync($"/api/memory/{pointId}");

    public async Task<List<MemoryDto>> GetAllMemoriesAsync(List<SessionDto> sessions)
    {
        var all = new List<MemoryDto>();
        foreach (var s in sessions.Take(20))
        {
            try { all.AddRange(await GetMemoriesBySessionAsync(s.SessionId)); }
            catch { }
        }
        return all.DistinctBy(m => m.PointId).OrderByDescending(m => m.CreatedAt).ToList();
    }

    // Preferences
    public async Task<List<PreferenceDto>> GetPreferencesAsync() =>
        await _http.GetFromJsonAsync<List<PreferenceDto>>("/api/preferences") ?? [];

    public async Task<PreferenceDto?> UpdatePreferenceAsync(int prefId, string value, double confidence, bool isExplicit)
    {
        var resp = await _http.PutAsJsonAsync($"/api/preferences/{prefId}", new { value, confidenceScore = confidence, isExplicit });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PreferenceDto>();
    }

    public async Task DeletePreferenceAsync(int prefId) =>
        await _http.DeleteAsync($"/api/preferences/{prefId}");

    // Anti-Patterns
    public async Task<List<AntiPatternDto>> GetAntiPatternsAsync() =>
        await _http.GetFromJsonAsync<List<AntiPatternDto>>("/api/antipatterns") ?? [];

    public async Task DeleteAntiPatternAsync(int id) =>
        await _http.DeleteAsync($"/api/antipatterns/{id}");

    // Experiments
    public async Task<List<TokenResultDto>> GetTokenResultsAsync() =>
        await _http.GetFromJsonAsync<List<TokenResultDto>>("/api/experiments/tokens") ?? [];

    // Projects
    public async Task<List<ProjectDto>> GetProjectsAsync() =>
        await _http.GetFromJsonAsync<List<ProjectDto>>("/api/projects") ?? [];

    public async Task<ProjectDto?> CreateProjectAsync(ProjectUpsertDto req)
    {
        var resp = await _http.PostAsJsonAsync("/api/projects", req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ProjectDto>();
    }

    public async Task<ProjectDto?> UpdateProjectAsync(int id, ProjectUpsertDto req)
    {
        var resp = await _http.PutAsJsonAsync($"/api/projects/{id}", req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ProjectDto>();
    }

    public async Task DeleteProjectAsync(int id) =>
        await _http.DeleteAsync($"/api/projects/{id}");

    // Packs
    public async Task<PackExportDto?> ExportPackAsync(string project) =>
        await _http.GetFromJsonAsync<PackExportDto>($"/api/packs/export/{project}");

    public async Task<PackImportResultDto?> ImportPackAsync(string packJson, string? decryptionKey = null, string? targetProject = null)
    {
        var resp = await _http.PostAsJsonAsync("/api/packs/import", new { packJson, decryptionKey, targetProject });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PackImportResultDto>();
    }

    public async Task<PackValidationResultDto?> ValidatePackAsync(string packJson)
    {
        var resp = await _http.PostAsJsonAsync("/api/packs/validate", new { packJson });
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<PackValidationResultDto>();
    }

    public async Task<string?> GetMachineIdAsync()
    {
        var result = await _http.GetFromJsonAsync<MachineIdDto>("/api/packs/machine-id");
        return result?.MachineId;
    }

    public async Task<string?> GenerateKeyAsync()
    {
        var result = await _http.GetFromJsonAsync<KeygenDto>("/api/packs/keygen");
        return result?.Key;
    }


    // Audit
    public async Task<AuditResultDto?> AnalyzeAsync(string content)
    {
        var resp = await _http.PostAsJsonAsync("/api/audit/analyze", new { content, sessionId = 0 });
        resp.EnsureSuccessStatusCode();
        var options = new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };
        var json = await resp.Content.ReadAsStringAsync();
        return System.Text.Json.JsonSerializer.Deserialize<AuditResultDto>(json, options);
    }

    // Audit allowlist
    public async Task<List<string>> GetAuditAllowlistAsync() =>
        await _http.GetFromJsonAsync<List<string>>("/api/audit/allowlist") ?? [];

    public async Task<(List<string>? Patterns, string? Error)> UpdateAuditAllowlistAsync(List<string> patterns)
    {
        var resp = await _http.PutAsJsonAsync("/api/audit/allowlist", new { patterns });
        if (resp.IsSuccessStatusCode)
            return (await resp.Content.ReadFromJsonAsync<List<string>>() ?? [], null);

        var body = await resp.Content.ReadAsStringAsync();
        return (null, string.IsNullOrWhiteSpace(body) ? resp.StatusCode.ToString() : body);
    }
}

// DTOs
public class HealthStatus
{
    public string Status { get; set; } = "";
    public Dictionary<string, System.Text.Json.JsonElement>? Services { get; set; }

    public Dictionary<string, string> GetServiceStatuses()
    {
        if (Services == null) return new();
        var result = new Dictionary<string, string>();
        foreach (var kvp in Services)
        {
            try { result[kvp.Key] = kvp.Value.GetProperty("status").GetString() ?? "unknown"; }
            catch { result[kvp.Key] = "unknown"; }
        }
        return result;
    }
}

public record SessionDto(
    int SessionId, string? Project, DateTime StartTime, DateTime? EndTime, bool IsConsolidated,
    DateTime? ConsolidatedAt, string? ConsolidationSummary, string? ConsolidationError);

public record MemoryDto(
    string PointId, int SessionId, string Summary, string? Solution,
    string? OutcomeLabel, double? RelevanceScore, string? Tags,
    string? Language, string? Project, DateTime CreatedAt);

public record PreferenceDto(
    int PrefId, string Category, string Key, string Value,
    double ConfidenceScore, int ReinforcementCount, bool IsExplicit,
    int? SourceSessionId, DateTime LastUpdated);

public record AntiPatternDto(
    int AntiPatternId, int SessionId, string Pattern, string Reason,
    string? Language, string? ErrorCode, DateTime CreatedAt);

public record TokenResultDto(string Tier, string Format, int AvgTokens, int SessionCount);

public class AuditResultDto
{
    public bool IsSafe { get; set; }
    public bool RequiresReview { get; set; }
    public string Verdict { get; set; } = "";
    public string? RedactedContent { get; set; }
    public List<AuditFindingDto> Findings { get; set; } = [];
}

public class AuditFindingDto
{
    public string DetectedType { get; set; } = "";
    public string Stage { get; set; } = "";
    public double Confidence { get; set; }
    public int StartIndex { get; set; }
    public int Length { get; set; }
}

public record ProjectDto(
    int ProjectId, string Name, string DisplayName,
    string Keywords, string? Facts, bool IsActive, DateTime CreatedAt);

public record ProjectUpsertDto(string Name, string DisplayName, string Keywords, string? Facts, bool IsActive = true);

// Pack DTOs
public class PackExportDto
{
    public string PackId { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Version { get; set; } = "";
    public string Author { get; set; } = "";
    public string TargetProject { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<PackMemoryDto> Memories { get; set; } = [];
    public List<PackPreferenceDto> Preferences { get; set; } = [];
    public List<PackAntiPatternDto> AntiPatterns { get; set; } = [];
}

public record PackMemoryDto(string Summary, string? Solution, string? Approach, string? OutcomeLabel, string? Tags, string? Language);
public record PackPreferenceDto(string Category, string Key, string Value, double ConfidenceScore);
public record PackAntiPatternDto(string Pattern, string Reason, string? Language, string? ErrorCode);

public class PackImportResultDto
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string PackId { get; set; } = "";
    public string PackName { get; set; } = "";
    public int MemoriesImported { get; set; }
    public int PreferencesImported { get; set; }
    public int AntiPatternsImported { get; set; }
    public int DuplicatesSkipped { get; set; }
}

public class PackValidationResultDto
{
    public bool IsValid { get; set; }
    public string? Error { get; set; }
    public string PackId { get; set; } = "";
    public string PackName { get; set; } = "";
    public int MemoryCount { get; set; }
    public int PreferenceCount { get; set; }
    public int AntiPatternCount { get; set; }
}

public record MachineIdDto(string MachineId);
public record KeygenDto(string Key);

