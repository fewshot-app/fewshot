using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Apex.Mcp;

[McpServerToolType]
public class ApexTools
{
    private readonly ApexClient _apex;
    public ApexTools(ApexClient apex) => _apex = apex;

    [McpServerTool, Description(
        "Gets personalized context from APEX for the current project. " +
        "Call at the start of every conversation with a short hint describing what you're working on. " +
        "Returns preferences, relevant past solutions, anti-patterns to avoid, and project facts.")]
    public async Task<string> apex_get_context(
        [Description("A short description of what you're working on, e.g. 'wordpress divi modules', 'apex mcp integration', 'peakhealth pdf generation'")]
        string hint = "general")
    {
        var project = await _apex.ResolveProjectAsync(hint);
        var session = await _apex.GetOrCreateSessionAsync(project);
        var context = await _apex.GetContextAsync(session.SessionId, project, hint);
        var header = session.IsNew
            ? $"[APEX] New session #{session.SessionId} — Project: {project}"
            : $"[APEX] Resuming session #{session.SessionId} — Project: {project}";
        return $"{header}\n\n{context}";
    }

    [McpServerTool, Description(
        "Records a message to the active APEX session for later consolidation. " +
        "Use role 'user' or 'assistant'. The nightly Ollama job will extract memories from these.")]
    public async Task<string> apex_record_message(
        [Description("Project name (e.g. 'wordpress', 'apex', 'peakhealth'). Use apex_get_context first to resolve this.")]
        string project,
        [Description("Message role: 'user' or 'assistant'")]
        string role,
        [Description("The message content to record")]
        string content)
    {
        var session = await _apex.GetOrCreateSessionAsync(project);
        await _apex.RecordMessageAsync(session.SessionId, role, content);
        return $"[APEX] Recorded {role} message to session #{session.SessionId}";
    }

    [McpServerTool, Description(
        "Searches APEX semantic memory for past solutions and learnings. " +
        "Use when you need to recall how a specific problem was solved before.")]
    public async Task<string> apex_search_memory(
        [Description("What to search for, e.g. 'Algolia cost optimization', 'Redis caching pattern', 'Divi module rendering'")]
        string query,
        [Description("Maximum number of results to return (default: 5)")]
        int top_k = 5)
    {
        return await _apex.SearchMemoryAsync(query, top_k);
    }

    [McpServerTool, Description(
        "Closes today's session for a project and triggers Ollama consolidation. " +
        "Optional — the nightly 2AM job does this automatically. " +
        "Use if you want memories extracted immediately after a productive session.")]
    public async Task<string> apex_end_day(
        [Description("Project name to close the session for")]
        string project)
    {
        await _apex.CloseSessionAsync(project);
        return $"[APEX] Session closed for '{project}'. Consolidation queued for tonight.";
    }

    [McpServerTool, Description(
        "Lists all active projects registered in APEX. " +
        "Useful for knowing what project names are available for other tools.")]
    public async Task<string> apex_list_projects()
    {
        var projects = await _apex.GetProjectsAsync();
        if (projects.Count == 0) return "[APEX] No projects registered yet.";
        return "[APEX] Active projects:\n" + string.Join("\n", projects.Select(p =>
            $"  • {p.Name} — {p.DisplayName} (keywords: {p.Keywords})"));
    }

    [McpServerTool, Description(
        "Adds a new project to APEX. " +
        "Keywords are comma-separated and used to auto-detect the project from conversation hints.")]
    public async Task<string> apex_add_project(
        [Description("Short lowercase name, e.g. 'connect', 'oliver', 'roblox'")]
        string name,
        [Description("Human-readable display name, e.g. 'Connect Intranet'")]
        string display_name,
        [Description("Comma-separated keywords for auto-detection, e.g. 'connect,intranet,wvu'")]
        string keywords)
    {
        return await _apex.AddProjectAsync(name, display_name, keywords);
    }

    [McpServerTool, Description(
        "Scans text content for PII, secrets, and sensitive data using APEX's three-stage audit pipeline " +
        "(regex patterns, Presidio NLP, Shannon entropy). Use before sending content that may contain " +
        "connection strings, API keys, SSNs, bearer tokens, private keys, or other sensitive information. " +
        "Returns safe/blocked verdict with specific findings.")]
    public async Task<string> apex_scan(
        [Description("The text content to scan for sensitive data")]
        string content)
    {
        var result = await _apex.ScanContentAsync(content);

        if (result.Findings.Count == 0)
            return "[APEX SCAN] \u2705 Clean — no sensitive data detected.";

        var verdict = result.IsSafe
            ? (result.RequiresReview ? "\u26a0\ufe0f Review recommended" : "\u2705 Safe")
            : "\ud83d\uded1 BLOCKED — sensitive data detected";

        var findingLines = result.Findings.Select(f =>
            $"  \u2022 {f.DetectedType} ({f.Stage}, confidence: {f.Confidence:F2})");

        var output = $"[APEX SCAN] {verdict}\nFindings:\n{string.Join("\n", findingLines)}";

        if (!result.IsSafe && result.RedactedContent != null)
            output += $"\n\nRedacted version available — {result.RedactedContent.Length} chars. " +
                      "Consider using the redacted version instead.";

        return output;
    }

    [McpServerTool, Description(
        "Removes a project from APEX by name. Does not delete associated sessions or memories. " +
        "Use apex_list_projects first to confirm the exact name.")]
    public async Task<string> apex_remove_project(
        [Description("Exact project name to remove, e.g. 'wordpress'")]
        string name)
    {
        return await _apex.RemoveProjectAsync(name);
    }
}
