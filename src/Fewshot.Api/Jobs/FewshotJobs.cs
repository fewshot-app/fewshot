using Fewshot.Core.Enums;
using Fewshot.Core.Interfaces;
using Fewshot.Core.Models;
using Fewshot.Api.Hubs;
using Hangfire;
using Microsoft.AspNetCore.SignalR;

namespace Fewshot.Api.Jobs;

public class ExperimentMetricsJob
{
    private readonly IExperimentService _experiments;
    private readonly ILogger<ExperimentMetricsJob> _logger;

    public ExperimentMetricsJob(IExperimentService experiments, ILogger<ExperimentMetricsJob> logger)
    {
        _experiments = experiments;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 2)]
    public async Task CollectAsync(int sessionId)
    {
        _logger.LogInformation("Collecting experiment metrics for session {SessionId}", sessionId);
        await _experiments.CollectMetricsAsync(sessionId);
    }
}

public class ConsolidationJob
{
    private readonly ISessionService _sessions;
    private readonly IMessageService _messages;
    private readonly IMemoryService _memory;
    private readonly IAntiPatternService _antiPatterns;
    private readonly IPreferenceService _preferences;
    private readonly ISuggestionService _suggestions;
    private readonly ILlmService _llm;
    private readonly IHubContext<FewshotHub> _hub;
    private readonly ILogger<ConsolidationJob> _logger;

    // Quality gate thresholds
    private const int MinMessages = 4;
    private const int MinTotalChars = 500;
    private const int MaxCorrections = 3;

    private const string ExtractionSystemPrompt = """
        You are a structured data extraction engine for a developer productivity system.
        Extract actionable knowledge from coding conversations.
        Always respond with valid JSON matching the requested schema.
        Be concise. Only extract genuinely useful information.
        Do NOT extract trivial or generic knowledge.
        """;

    public ConsolidationJob(
        ISessionService sessions,
        IMessageService messages,
        IMemoryService memory,
        IAntiPatternService antiPatterns,
        IPreferenceService preferences,
        ISuggestionService suggestions,
        ILlmService llm,
        IHubContext<FewshotHub> hub,
        ILogger<ConsolidationJob> logger)
    {
        _sessions = sessions;
        _messages = messages;
        _memory = memory;
        _antiPatterns = antiPatterns;
        _preferences = preferences;
        _suggestions = suggestions;
        _llm = llm;
        _hub = hub;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        _logger.LogInformation("Starting nightly consolidation");

        var closed = await _sessions.CloseStaleActiveSessionsAsync(TimeSpan.FromHours(12));
        if (closed > 0)
            _logger.LogInformation("Auto-closed {Count} stale active session(s)", closed);

        var unconsolidated = await _sessions.GetUnconsolidatedSessionsAsync();
        _logger.LogInformation("Found {Count} unconsolidated sessions", unconsolidated.Count);

        foreach (var session in unconsolidated)
        {
            try
            {
                await ConsolidateSessionAsync(session);
            }
            catch (Exception ex)
            {
                await _sessions.MarkConsolidationFailedAsync(session.SessionId, ex.Message);
                await _hub.SendSessionUpdate(session.SessionId, "Failed", ex.Message);
                _logger.LogError(ex, "Failed to consolidate session {SessionId}", session.SessionId);
            }
        }

        _logger.LogInformation("Consolidation complete");
    }

    /// <summary>
    /// Run consolidation for a single session (for testing/manual trigger).
    /// </summary>
    [AutomaticRetry(Attempts = 1)]
    public async Task ConsolidateSingleAsync(int sessionId)
    {
        var session = await _sessions.GetSessionAsync(sessionId);
        if (session == null)
        {
            _logger.LogWarning("Session {SessionId} not found", sessionId);
            return;
        }
        await ConsolidateSessionAsync(session);
    }

    private async Task ConsolidateSessionAsync(Session session)
    {
        _logger.LogInformation("Consolidating session {SessionId}", session.SessionId);

        // Step 1: Quality gate
        var quality = await CheckQualityAsync(session.SessionId);
        if (!quality.ShouldConsolidate)
        {
            _logger.LogInformation("Session {SessionId} skipped: {Reason}",
                session.SessionId, quality.SkipReason);
            await _sessions.MarkConsolidatedAsync(session.SessionId,
                $"Skipped: {quality.SkipReason}");
            return;
        }

        // Step 2: Build conversation transcript
        var messages = await _messages.GetSessionMessagesAsync(session.SessionId);
        var transcript = BuildTranscript(messages);

        // Step 3: Extract structured data via Ollama
        var extraction = await ExtractFromConversationAsync(transcript, session.SessionId);
        if (extraction == null)
        {
            await _sessions.MarkConsolidationFailedAsync(session.SessionId,
                "Ollama extraction returned null");
            return;
        }

        // Step 4: Store extracted memories (local vector store)
        var memoriesStored = 0;
        foreach (var mem in extraction.Memories)
        {
            var stored = await _memory.StoreAsync(new MemoryStoreRequest
            {
                SessionId = session.SessionId,
                Summary = mem.Summary,
                Solution = mem.Solution,
                OutcomeLabel = mem.OutcomeLabel,
                Tags = mem.Tags,
                Language = mem.Language,
                Project = mem.Project
            });
            if (stored != null) memoriesStored++;
        }

        // Step 5: Store extracted anti-patterns in SQL
        var antiPatternsStored = 0;
        foreach (var ap in extraction.AntiPatterns)
        {
            await _antiPatterns.CreateAsync(new AntiPattern
            {
                SessionId = session.SessionId,
                Pattern = ap.Pattern,
                Reason = ap.Reason,
                Language = ap.Language,
                ErrorCode = ap.ErrorCode
            });
            antiPatternsStored++;
        }

        // Step 6: Store extracted suggestions in SQL
        var suggestionsStored = 0;
        // Get first assistant message ID as fallback for suggestion linkage
        var firstAssistantMsg = messages.FirstOrDefault(m => m.Role == MessageRole.Assistant);
        var fallbackMessageId = firstAssistantMsg?.MessageId ?? messages.First().MessageId;
        foreach (var sug in extraction.Suggestions)
        {
            var sugType = sug.Type switch
            {
                "CodeSnippet" => SuggestionType.CodeSnippet,
                "ArchitecturalPattern" => SuggestionType.ArchitecturalPattern,
                "ConfigChange" => SuggestionType.ConfigChange,
                _ => SuggestionType.CodeSnippet
            };
            await _suggestions.CreateAsync(new Suggestion
            {
                MessageId = fallbackMessageId,
                SuggestionType = sugType,
                Content = sug.Content,
                Language = sug.Language,
                FilePath = sug.FilePath,
                ExtractionMethod = ExtractionMethod.LLM,
                ExtractionConfidence = sug.Confidence,
                CreatedAt = DateTime.UtcNow
            });
            suggestionsStored++;
        }

        // Step 7: Reinforce or create inferred preferences
        var prefsUpdated = 0;
        var prefsReinforced = 0;
        foreach (var pref in extraction.Preferences)
        {
            var result = await _preferences.ReinforceOrUpsertAsync(
                pref.Category, pref.Key, pref.Value, session.SessionId);
            prefsUpdated++;
            if (result.ReinforcementCount > 1) prefsReinforced++;
        }

        // Step 8: Mark consolidated
        var summary = $"{extraction.SessionSummary} | " +
                      $"Extracted: {memoriesStored} memories, {antiPatternsStored} anti-patterns, {suggestionsStored} suggestions, {prefsUpdated} preferences ({prefsReinforced} reinforced)";
        await _sessions.MarkConsolidatedAsync(session.SessionId, summary);
        await _hub.SendSessionUpdate(session.SessionId, "Consolidated", summary);

        _logger.LogInformation(
            "Session {SessionId} consolidated: {Memories} memories, {AntiPatterns} anti-patterns, {Suggestions} suggestions, {Prefs} preferences ({Reinforced} reinforced)",
            session.SessionId, memoriesStored, antiPatternsStored, suggestionsStored, prefsUpdated, prefsReinforced);
    }

    private async Task<ConsolidationQualityResult> CheckQualityAsync(int sessionId)
    {
        var messages = await _messages.GetSessionMessagesAsync(sessionId);
        var totalChars = messages.Sum(m => m.Content.Length);
        var corrections = await _messages.GetCorrectionCountAsync(sessionId);

        if (messages.Count < MinMessages)
            return new ConsolidationQualityResult
            {
                ShouldConsolidate = false,
                SkipReason = $"Too few messages ({messages.Count}, min {MinMessages})",
                MessageCount = messages.Count, TotalChars = totalChars, CorrectionCount = corrections
            };

        if (totalChars < MinTotalChars)
            return new ConsolidationQualityResult
            {
                ShouldConsolidate = false,
                SkipReason = $"Too short ({totalChars} chars, min {MinTotalChars})",
                MessageCount = messages.Count, TotalChars = totalChars, CorrectionCount = corrections
            };

        if (corrections > MaxCorrections)
            return new ConsolidationQualityResult
            {
                ShouldConsolidate = false,
                SkipReason = $"Too many corrections ({corrections}, max {MaxCorrections})",
                MessageCount = messages.Count, TotalChars = totalChars, CorrectionCount = corrections
            };

        return new ConsolidationQualityResult
        {
            ShouldConsolidate = true,
            MessageCount = messages.Count,
            TotalChars = totalChars,
            CorrectionCount = corrections
        };
    }

    private async Task<ConsolidationExtraction?> ExtractFromConversationAsync(string transcript, int sessionId)
    {
        var prompt = "Analyze this developer conversation and extract structured knowledge.\n\n" +
            "CONVERSATION:\n" + transcript + "\n\n" +
            "Extract the following as JSON:\n" +
            "{\n" +
            "  \"sessionSummary\": \"One sentence describing what was accomplished\",\n" +
            "  \"memories\": [\n" +
            "    {\n" +
            "      \"summary\": \"What was learned or solved (be specific)\",\n" +
            "      \"solution\": \"The approach or fix that worked (or null if no solution)\",\n" +
            "      \"outcomeLabel\": \"Worked | Failed | Partial | null\",\n" +
            "      \"tags\": \"comma,separated,tags\",\n" +
            "      \"language\": \"C# | PHP | JavaScript | SQL | null\",\n" +
            "      \"project\": \"Project name if mentioned, or null\"\n" +
            "    }\n" +
            "  ],\n" +
            "  \"antiPatterns\": [\n" +
            "    {\n" +
            "      \"pattern\": \"What NOT to do (specific)\",\n" +
            "      \"reason\": \"Why it failed or causes problems\",\n" +
            "      \"language\": \"C# | PHP | JavaScript | null\",\n" +
            "      \"errorCode\": \"SHORT_ERROR_CODE or null\"\n" +
            "    }\n" +
            "  ],\n" +
            "  \"suggestions\": [\n" +
            "    {\n" +
            "      \"content\": \"The specific actionable suggestion the assistant made\",\n" +
            "      \"type\": \"CodeSnippet | ArchitecturalPattern | ConfigChange\",\n" +
            "      \"language\": \"C# | PHP | JavaScript | SQL | null\",\n" +
            "      \"filePath\": \"File path if mentioned, or null\",\n" +
            "      \"confidence\": 0.8\n" +
            "    }\n" +
            "  ],\n" +
            "  \"preferences\": [\n" +
            "    {\n" +
            "      \"category\": \"CodingStyle | Architecture | Tooling\",\n" +
            "      \"key\": \"PreferenceName in PascalCase\",\n" +
            "      \"value\": \"What the developer prefers\"\n" +
            "    }\n" +
            "  ]\n" +
            "}\n\n" +
            "Rules:\n" +
            "- Only extract genuinely useful, specific knowledge. Skip generic/obvious things.\n" +
            "- Memories should capture solutions that would help in future similar problems.\n" +
            "- Anti-patterns should only be added if something explicitly failed or was identified as bad.\n" +
            "- Suggestions should be specific, actionable recommendations from the ASSISTANT messages.\n" +
            "- Preferences should only be added if the developer clearly expressed a preference.\n" +
            "- If nothing useful can be extracted for a category, return an empty array.\n" +
            "- Keep summaries concise but specific enough to be useful months later.";


        return await _llm.GenerateJsonAsync<ConsolidationExtraction>(prompt, ExtractionSystemPrompt);
    }

    private static string BuildTranscript(List<Message> messages)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var msg in messages)
        {
            var role = msg.Role == MessageRole.User ? "USER" : "ASSISTANT";
            // Truncate very long messages to keep prompt reasonable
            var content = msg.Content.Length > 2000
                ? msg.Content[..2000] + "... [truncated]"
                : msg.Content;
            sb.AppendLine($"{role}: {content}");
            sb.AppendLine();
        }
        return sb.ToString();
    }
}
