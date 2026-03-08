using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Apex.Core.Interfaces;
using Apex.Core.Models;
using Apex.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Apex.Infrastructure.Audit;

/// <summary>
/// Three-stage audit pipeline: Regex → Presidio → Shannon Entropy.
/// Presidio sidecar integration is stubbed — replace HttpClient call when container is running.
/// </summary>
public class AuditService : IAuditService
{
    private readonly ApexDbContext _db;
    private readonly HttpClient _presidioClient;

    // Stage 1: High-specificity regex patterns (near-zero false positive rate)
    private static readonly (string Name, Regex Pattern)[] RegexPatterns =
    [
        ("ConnectionString", new Regex(@"(Server|Data Source|Initial Catalog|Password|Pwd)\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("BearerToken", new Regex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.Compiled)),
        ("PrivateKey", new Regex(@"-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----", RegexOptions.Compiled)),
        ("AWSKey", new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled)),
        ("GitHubToken", new Regex(@"gh[ps]_[A-Za-z0-9_]{36,}", RegexOptions.Compiled)),
        ("SSN", new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)),
        ("ApiKeyParam", new Regex(@"[?&](api[_-]?key|apikey|access[_-]?token)\s*=\s*[A-Za-z0-9\-._~+/]{16,}", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
    ];

    // Stage 3: Shannon entropy threshold for detecting secrets
    private const double EntropyThreshold = 4.5;
    private const int MinTokenLength = 20;

    public AuditService(ApexDbContext db, IHttpClientFactory httpClientFactory)
    {
        _db = db;
        _presidioClient = httpClientFactory.CreateClient("Presidio");
    }

    public async Task<AuditPipelineResult> AnalyzeAsync(string content, int sessionId)
    {
        var findings = new List<AuditFinding>();

        // Stage 1: Regex pre-filter
        foreach (var (name, pattern) in RegexPatterns)
        {
            foreach (Match match in pattern.Matches(content))
            {
                findings.Add(new AuditFinding
                {
                    DetectedType = name,
                    Stage = AuditStage.Regex,
                    Confidence = 1.0,
                    StartIndex = match.Index,
                    Length = match.Length
                });
            }
        }

        // Stage 2: Presidio (if sidecar available)
        try
        {
            var presidioFindings = await RunPresidioAsync(content);
            findings.AddRange(presidioFindings);
        }
        catch (HttpRequestException)
        {
            // Presidio sidecar unavailable — fail open per architecture doc
        }

        // Stage 3: Shannon entropy on long tokens in code blocks
        var entropyFindings = AnalyzeEntropy(content);
        findings.AddRange(entropyFindings);

        // Determine verdict
        var hasBlockingFinding = findings.Any(f => f.Confidence >= 0.95 &&
            f.DetectedType is "SSN" or "PrivateKey" or "ConnectionString");

        var result = new AuditPipelineResult
        {
            IsSafe = !hasBlockingFinding,
            RequiresReview = findings.Count > 0 && !hasBlockingFinding,
            Findings = findings
        };

        if (result.RequiresReview)
            result.RedactedContent = RedactContent(content, findings);

        // Log audit record
        await LogAuditAsync(sessionId, findings, result);

        return result;
    }

    private async Task<List<AuditFinding>> RunPresidioAsync(string content)
    {
        var findings = new List<AuditFinding>();

        var request = new
        {
            text = content,
            language = "en",
            entities = new[] { "PERSON", "PHONE_NUMBER", "EMAIL_ADDRESS", "CREDIT_CARD", "US_SSN", "MEDICAL_LICENSE", "US_DRIVER_LICENSE", "IP_ADDRESS" },
            score_threshold = 0.6
        };

        var json = System.Text.Json.JsonSerializer.Serialize(request);
        var response = await _presidioClient.PostAsync("/analyze",
            new StringContent(json, Encoding.UTF8, "application/json"));

        if (!response.IsSuccessStatusCode) return findings;

        var responseJson = await response.Content.ReadAsStringAsync();
        var results = System.Text.Json.JsonSerializer.Deserialize<List<PresidioResult>>(responseJson,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (results == null) return findings;

        foreach (var r in results)
        {
            findings.Add(new AuditFinding
            {
                DetectedType = r.EntityType,
                Stage = AuditStage.Presidio,
                Confidence = r.Score,
                StartIndex = r.Start,
                Length = r.End - r.Start
            });
        }

        return findings;
    }

    private class PresidioResult
    {
        [System.Text.Json.Serialization.JsonPropertyName("entity_type")]
        public string EntityType { get; set; } = string.Empty;
        public double Score { get; set; }
        public int Start { get; set; }
        public int End { get; set; }
    }

    private static List<AuditFinding> AnalyzeEntropy(string content)
    {
        var findings = new List<AuditFinding>();
        var tokenPattern = new Regex(@"[A-Za-z0-9\-._~+/=]{" + MinTokenLength + @",}");

        foreach (Match match in tokenPattern.Matches(content))
        {
            var entropy = CalculateShannonEntropy(match.Value);
            if (entropy > EntropyThreshold)
            {
                findings.Add(new AuditFinding
                {
                    DetectedType = "HighEntropySecret",
                    Stage = AuditStage.Entropy,
                    Confidence = Math.Min(entropy / 6.0, 1.0)
                });
            }
        }

        return findings;
    }

    private static double CalculateShannonEntropy(string input)
    {
        var freq = new Dictionary<char, int>();
        foreach (var c in input)
            freq[c] = freq.GetValueOrDefault(c) + 1;

        var len = (double)input.Length;
        return freq.Values.Sum(count =>
        {
            var p = count / len;
            return -p * Math.Log2(p);
        });
    }

    private static string RedactContent(string content, List<AuditFinding> findings)
    {
        var redacted = content;
        foreach (var (_, pattern) in RegexPatterns)
            redacted = pattern.Replace(redacted, "[REDACTED]");
        return redacted;
    }

    private async Task LogAuditAsync(int sessionId, List<AuditFinding> findings, AuditPipelineResult result)
    {
        if (findings.Count == 0 || sessionId <= 0) return;

        var grouped = findings
            .Where(f => !string.IsNullOrEmpty(f.DetectedType))
            .GroupBy(f => f.DetectedType);
        foreach (var group in grouped)
        {
            _db.AuditLogs.Add(new AuditLog
            {
                SessionId = sessionId,
                DetectedType = group.Key,
                FindingCount = group.Count(),
                WasBlocked = !result.IsSafe,
                WasRedacted = result.RequiresReview
            });
        }
        await _db.SaveChangesAsync();
    }
}
