using System.Net.Http.Json;

namespace Apex.Dashboard.Services;

public class ApexApiClient
{
    private readonly HttpClient _http;

    public ApexApiClient(HttpClient http) => _http = http;

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

    // Tasks
    public async Task<AgencyReadinessDto?> GetReadinessAsync() =>
        await _http.GetFromJsonAsync<AgencyReadinessDto>("/api/tasks/readiness");

    public async Task<List<TaskDto>> GetTasksBySessionAsync(int sessionId) =>
        await _http.GetFromJsonAsync<List<TaskDto>>($"/api/tasks/session/{sessionId}") ?? [];

    public async Task<List<TaskDto>> GetPendingApprovalAsync() =>
        await _http.GetFromJsonAsync<List<TaskDto>>("/api/tasks/pending-approval") ?? [];

    public async Task ApproveTaskAsync(int taskId) =>
        await _http.PostAsync($"/api/tasks/{taskId}/approve", null);

    public async Task RejectTaskAsync(int taskId, string reason) =>
        await _http.PostAsJsonAsync($"/api/tasks/{taskId}/reject", new { reason });

    // Experiments
    public async Task<List<TokenResultDto>> GetTokenResultsAsync() =>
        await _http.GetFromJsonAsync<List<TokenResultDto>>("/api/experiments/tokens") ?? [];

    // Thresholds
    public async Task<ThresholdsDto?> GetThresholdsAsync() =>
        await _http.GetFromJsonAsync<ThresholdsDto>("/api/tasks/thresholds");

    public async Task<ThresholdsDto?> UpdateThresholdsAsync(UpdateThresholdsDto req)
    {
        var resp = await _http.PutAsJsonAsync("/api/tasks/thresholds", req);
        return await resp.Content.ReadFromJsonAsync<ThresholdsDto>();
    }

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
    int SessionId, DateTime StartTime, DateTime? EndTime, bool IsConsolidated,
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

public record TaskDto(
    int TaskId, int SessionId, string TaskType, string Status,
    string? Payload, string? Result, int AttemptCount, int MaxAttempts,
    string? LastError, bool RequiresApproval, DateTime CreatedAt, DateTime? CompletedAt);

public record AgencyReadinessDto(
    bool IsReady, int SuggestionsLogged, int SuggestionsRequired,
    double ExplicitFeedbackRate, double FeedbackRateRequired,
    int AntiPatternSuppressions, int SuppressionsRequired,
    int ConsolidatedSessions, int ConsolidatedSessionsRequired,
    List<string> BlockingReasons, List<string>? Warnings = null);

public record TokenResultDto(string Tier, string Format, int AvgTokens, int SessionCount);

public class ThresholdsDto
{
    public int MinSuggestions { get; set; }
    public double MinFeedbackRate { get; set; }
    public int MinAntiPatternSuppressions { get; set; }
    public int MinConsolidatedSessions { get; set; }
    public int RecommendedSuggestions { get; set; }
    public double RecommendedFeedbackRate { get; set; }
    public int RecommendedSuppressions { get; set; }
    public int RecommendedConsolidatedSessions { get; set; }
}

public class UpdateThresholdsDto
{
    public int? MinSuggestions { get; set; }
    public double? MinFeedbackRate { get; set; }
    public int? MinAntiPatternSuppressions { get; set; }
    public int? MinConsolidatedSessions { get; set; }
}

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
