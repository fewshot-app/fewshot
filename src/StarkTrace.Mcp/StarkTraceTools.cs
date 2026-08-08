using System.ComponentModel;
using ModelContextProtocol.Server;

namespace StarkTrace.Mcp;

[McpServerToolType]
public class StarkTraceTools
{
    private readonly StarkTraceClient _starktrace;
    public StarkTraceTools(StarkTraceClient starktrace) => _starktrace = starktrace;

    [McpServerTool, Description(
        "Gets personalized context from StarkTrace for the current project. " +
        "Call at the start of every conversation with a short hint describing what you're working on. " +
        "Returns preferences, relevant past solutions, anti-patterns to avoid, and project facts.")]
    public async Task<string> starktrace_get_context(
        [Description("A short description of what you're working on, e.g. 'wordpress divi modules', 'starktrace mcp integration', 'peakhealth pdf generation'")]
        string hint = "general")
    {
        try
        {
            var project = await _starktrace.ResolveProjectAsync(hint);
            var session = await _starktrace.GetOrCreateSessionAsync(project);
            var context = await _starktrace.GetContextAsync(session.SessionId, project, hint);
            var header = session.IsNew
                ? $"[StarkTrace] New session #{session.SessionId} — Project: {project}"
                : $"[StarkTrace] Resuming session #{session.SessionId} — Project: {project}";
            return $"{header}\n\n{context}";
        }
        catch (Exception ex)
        {
            return $"[StarkTrace ERROR] {ex.GetType().Name}: {ex.Message}\n{ex.InnerException?.Message}";
        }
    }

    [McpServerTool, Description(
        "Records a message to the active StarkTrace session for later consolidation. " +
        "Use role 'user' or 'assistant'. The nightly Ollama job will extract memories from these.")]
    public async Task<string> starktrace_record_message(
        [Description("Project name (e.g. 'wordpress', 'starktrace', 'peakhealth'). Use starktrace_get_context first to resolve this.")]
        string project,
        [Description("Message role: 'user' or 'assistant'")]
        string role,
        [Description("The message content to record")]
        string content)
    {
        try
        {
            var session = await _starktrace.GetOrCreateSessionAsync(project);
            await _starktrace.RecordMessageAsync(session.SessionId, role, content);
            return $"[StarkTrace] Recorded {role} message to session #{session.SessionId}";
        }
        catch (Exception ex)
        {
            return $"[StarkTrace ERROR] Failed to record message: {ex.GetType().Name}: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Searches StarkTrace semantic memory for past solutions and learnings. " +
        "Use when you need to recall how a specific problem was solved before.")]
    public async Task<string> starktrace_search_memory(
        [Description("What to search for, e.g. 'Algolia cost optimization', 'Redis caching pattern', 'Divi module rendering'")]
        string query,
        [Description("Maximum number of results to return (default: 5)")]
        int top_k = 5)
    {
        return await _starktrace.SearchMemoryAsync(query, top_k);
    }

    [McpServerTool, Description(
        "Closes today's session for a project and triggers Ollama consolidation. " +
        "Optional — the nightly 2AM job does this automatically. " +
        "Use if you want memories extracted immediately after a productive session.")]
    public async Task<string> starktrace_end_day(
        [Description("Project name to close the session for")]
        string project)
    {
        try
        {
            await _starktrace.CloseSessionAsync(project);
            var result = await _starktrace.TriggerConsolidationAsync();
            return $"[StarkTrace] Session closed for '{project}'. Consolidation triggered.\n{result}";
        }
        catch (Exception ex)
        {
            return $"[StarkTrace ERROR] {ex.GetType().Name}: {ex.Message}";
        }
    }

    [McpServerTool, Description(
        "Lists all active projects registered in StarkTrace. " +
        "Useful for knowing what project names are available for other tools.")]
    public async Task<string> starktrace_list_projects()
    {
        var projects = await _starktrace.GetProjectsAsync();
        if (projects.Count == 0) return "[StarkTrace] No projects registered yet.";
        return "[StarkTrace] Active projects:\n" + string.Join("\n", projects.Select(p =>
            $"  • {p.Name} — {p.DisplayName} (keywords: {p.Keywords})"));
    }

    [McpServerTool, Description(
        "Adds a new project to StarkTrace. " +
        "Keywords are comma-separated and used to auto-detect the project from conversation hints.")]
    public async Task<string> starktrace_add_project(
        [Description("Short lowercase name, e.g. 'connect', 'oliver', 'roblox'")]
        string name,
        [Description("Human-readable display name, e.g. 'Connect Intranet'")]
        string display_name,
        [Description("Comma-separated keywords for auto-detection, e.g. 'connect,intranet,wvu'")]
        string keywords)
    {
        return await _starktrace.AddProjectAsync(name, display_name, keywords);
    }

    [McpServerTool, Description(
        "Edits an existing project. " +
        "All fields except 'name' are optional — pass only the fields you want to change. " +
        "Useful for adding keywords to an existing project without re-typing them all. " +
        "To activate/deactivate a project, use starktrace_activate_project or starktrace_deactivate_project instead.")]
    public async Task<string> starktrace_edit_project(
        [Description("Exact project name to edit, e.g. 'connectapps'. Use starktrace_list_projects to confirm.")]
        string name,
        [Description("New human-readable display name. Omit to keep current.")]
        string? display_name = null,
        [Description("New comma-separated keywords. REPLACES existing keywords entirely. Omit to keep current.")]
        string? keywords = null,
        [Description("New free-form facts/notes about the project. Omit to keep current.")]
        string? facts = null)
    {
        return await _starktrace.UpdateProjectAsync(name, display_name, keywords, facts, null);
    }

    [McpServerTool, Description(
        "Activates a project so it participates in keyword auto-routing and context injection.")]
    public async Task<string> starktrace_activate_project(
        [Description("Exact project name to activate, e.g. 'connectapps'. Use starktrace_list_projects to confirm.")]
        string name)
    {
        return await _starktrace.UpdateProjectAsync(name, null, null, null, true);
    }

    [McpServerTool, Description(
        "Deactivates a project so it stops participating in keyword auto-routing and context injection. " +
        "Does not delete the project, its sessions, or its memories.")]
    public async Task<string> starktrace_deactivate_project(
        [Description("Exact project name to deactivate, e.g. 'connectapps'. Use starktrace_list_projects to confirm.")]
        string name)
    {
        return await _starktrace.UpdateProjectAsync(name, null, null, null, false);
    }

    [McpServerTool, Description(
        "Scans text content for PII, secrets, and sensitive data using StarkTrace's three-stage audit pipeline " +
        "(regex patterns, Presidio NLP, Shannon entropy). Use before sending content that may contain " +
        "connection strings, API keys, SSNs, bearer tokens, private keys, or other sensitive information. " +
        "Returns safe/blocked verdict with specific findings.")]
    public async Task<string> starktrace_scan(
        [Description("The text content to scan for sensitive data")]
        string content)
    {
        var result = await _starktrace.ScanContentAsync(content);

        if (result.Findings.Count == 0)
            return "[StarkTrace SCAN] \u2705 Clean — no sensitive data detected.";

        var verdict = result.IsSafe
            ? (result.RequiresReview ? "\u26a0\ufe0f Review recommended" : "\u2705 Safe")
            : "\ud83d\uded1 BLOCKED — sensitive data detected";

        var findingLines = result.Findings.Select(f =>
            $"  \u2022 {f.DetectedType} ({f.Stage}, confidence: {f.Confidence:F2})");

        var output = $"[StarkTrace SCAN] {verdict}\nFindings:\n{string.Join("\n", findingLines)}";

        if (!result.IsSafe && result.RedactedContent != null)
            output += $"\n\nRedacted version available — {result.RedactedContent.Length} chars. " +
                      "Consider using the redacted version instead.";

        return output;
    }

    [McpServerTool, Description(
        "Removes a project from StarkTrace by name. Does not delete associated sessions or memories. " +
        "Use starktrace_list_projects first to confirm the exact name.")]
    public async Task<string> starktrace_remove_project(
        [Description("Exact project name to remove, e.g. 'wordpress'")]
        string name)
    {
        return await _starktrace.RemoveProjectAsync(name);
    }
}
