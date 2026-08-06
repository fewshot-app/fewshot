// Apex.Proxy — MCP Audit Proxy
// Sits between Claude Desktop and ANY MCP server.
// Intercepts every JSON-RPC message over stdio, scans for PII/secrets, logs to APEX API.
//
// Usage in claude_desktop_config.json — wrap any MCP server:
//
//   "apex": {
//     "command": "C:\\...\\Apex.Proxy.exe",
//     "args": ["--server", "C:\\...\\Apex.Mcp.exe"]
//   }
//
//   "filesystem": {
//     "command": "C:\\...\\Apex.Proxy.exe",
//     "args": ["--server", "npx", "--", "@modelcontextprotocol/server-filesystem", "C:\\Users\\Joe"]
//   }

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

// Shadow-copy: run from temp so the install directory is never locked
// This allows updates/uninstalls without quitting Claude Desktop.
if (Environment.GetEnvironmentVariable("APEX_PROXY_SHADOW") != "1")
{
    var src = Environment.ProcessPath!;
    var shadowDir = Path.Combine(Path.GetTempPath(), "apex-proxy", Path.GetRandomFileName());
    Directory.CreateDirectory(shadowDir);
    var shadowExe = Path.Combine(shadowDir, Path.GetFileName(src));

    // Copy all files from the proxy directory (exe + deps)
    var srcDir = Path.GetDirectoryName(src)!;
    foreach (var file in Directory.GetFiles(srcDir))
        File.Copy(file, Path.Combine(shadowDir, Path.GetFileName(file)), true);

    var shadowPsi = new ProcessStartInfo
    {
        FileName = shadowExe,
        UseShellExecute = false,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = false,
    };
    // Pass through all original args
    foreach (var a in args) shadowPsi.ArgumentList.Add(a);
    shadowPsi.Environment["APEX_PROXY_SHADOW"] = "1";

    using var shadow = Process.Start(shadowPsi)!;

    // Pipe stdin ΓåÆ shadow stdin
    var pipeIn = Task.Run(async () =>
    {
        try
        {
            await Console.OpenStandardInput().CopyToAsync(shadow.StandardInput.BaseStream);
            shadow.StandardInput.Close();
        }
        catch { /* Claude closed stdin */ }
    });

    // Pipe shadow stdout ΓåÆ our stdout
    var pipeOut = Task.Run(async () =>
    {
        try { await shadow.StandardOutput.BaseStream.CopyToAsync(Console.OpenStandardOutput()); }
        catch { /* shadow exited */ }
    });

    await shadow.WaitForExitAsync();

    // Cleanup shadow copy (best-effort)
    try { Directory.Delete(shadowDir, true); } catch { }

    return shadow.ExitCode;
}

// Capture stdout BEFORE redirecting (MCP protocol must use raw stdout)
var stdoutStream = new FileStream(
    new SafeFileHandle(GetStdHandle(-11), ownsHandle: false),
    FileAccess.Write);
var mcpOut = new StreamWriter(stdoutStream, new UTF8Encoding(false)) { AutoFlush = true };

// Redirect Console.Out → stderr so any accidental writes don't corrupt the JSON-RPC stream
Console.SetOut(Console.Error);

// ── Parse args: --server <exe> [server-args...] ───────────────────────────────
var serverExe = string.Empty;
var serverArgs = new List<string>();
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--server" && i + 1 < args.Length)
        serverExe = args[++i];
    else if (!string.IsNullOrEmpty(serverExe))
        serverArgs.Add(args[i]);
}

if (string.IsNullOrEmpty(serverExe))
{
    Console.Error.WriteLine("[APEX-PROXY] Usage: Apex.Proxy.exe --server <mcp-server.exe> [args...]");
    return 1;
}

var apexApiBase = Environment.GetEnvironmentVariable("APEX_API_URL") ?? "http://localhost:5000";
Console.Error.WriteLine($"[APEX-PROXY] Starting. Server: {serverExe}. APEX: {apexApiBase}");

// ── Spawn the real MCP server ─────────────────────────────────────────────────
var psi = new ProcessStartInfo
{
    FileName = serverExe,
    UseShellExecute = false,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    StandardInputEncoding = new UTF8Encoding(false),
    StandardOutputEncoding = new UTF8Encoding(false),
};

// Handle args that may include a sub-command (e.g. "npx -- @mcp/server-filesystem C:\path")
if (serverArgs.Count > 0)
    psi.Arguments = string.Join(" ", serverArgs.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));

using var server = Process.Start(psi)
    ?? throw new InvalidOperationException($"[APEX-PROXY] Failed to start: {serverExe}");

// Forward server stderr → our stderr
_ = Task.Run(async () =>
{
    string? line;
    while ((line = await server.StandardError.ReadLineAsync()) != null)
        await Console.Error.WriteLineAsync($"[SERVER] {line}");
});

using var http = new HttpClient { BaseAddress = new Uri(apexApiBase), Timeout = TimeSpan.FromSeconds(3) };
var audit = new AuditClient(http);

// ── Claude → Proxy → Server (scan requests) ───────────────────────────────────
var claudeToServer = Task.Run(async () =>
{
    string? line;
    while ((line = await Console.In.ReadLineAsync()) != null)
    {
        var forwarded = await audit.ProcessAsync(line, "outbound");
        await server.StandardInput.WriteLineAsync(forwarded);
        await server.StandardInput.FlushAsync();
    }
    server.StandardInput.Close();
});

// ── Server → Proxy → Claude (scan responses) ─────────────────────────────────
var serverToClaude = Task.Run(async () =>
{
    string? line;
    while ((line = await server.StandardOutput.ReadLineAsync()) != null)
    {
        var forwarded = await audit.ProcessAsync(line, "inbound");
        await mcpOut.WriteLineAsync(forwarded);
        await mcpOut.FlushAsync();
    }
});

await Task.WhenAny(claudeToServer, serverToClaude);
await server.WaitForExitAsync();
Console.Error.WriteLine($"[APEX-PROXY] Server exited with code {server.ExitCode}");
return server.ExitCode;

[DllImport("kernel32.dll", SetLastError = true)]
static extern nint GetStdHandle(int nStdHandle);

// ═════════════════════════════════════════════════════════════════════════════
// AuditClient — scans JSON-RPC messages, logs findings to APEX API
// ═════════════════════════════════════════════════════════════════════════════
public class AuditClient
{
    private readonly HttpClient _http;

    private static readonly (string Name, Regex Pattern)[] Patterns =
    [
        ("ConnectionString", new Regex(@"(Server|Data Source|Initial Catalog|Password|Pwd)\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("BearerToken",      new Regex(@"Bearer\s+[A-Za-z0-9\-._~+/]+=*", RegexOptions.Compiled)),
        ("JwtToken",         new Regex(@"eyJ[A-Za-z0-9_-]{10,}\.eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}", RegexOptions.Compiled)),
        ("PrivateKey",       new Regex(@"-----BEGIN\s+(RSA\s+)?PRIVATE\s+KEY-----", RegexOptions.Compiled)),
        ("AWSKey",           new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled)),
        ("GitHubToken",      new Regex(@"gh[ps]_[A-Za-z0-9_]{36,}", RegexOptions.Compiled)),
        ("SSN",              new Regex(@"\b\d{3}-\d{2}-\d{4}\b", RegexOptions.Compiled)),
        ("ApiKeyParam",      new Regex(@"[?&](api[_-]?key|apikey|access[_-]?token)\s*=\s*[A-Za-z0-9\-._~+/]{16,}", RegexOptions.IgnoreCase | RegexOptions.Compiled)),
        ("CreditCard",       new Regex(@"\b(?:4[0-9]{12}(?:[0-9]{3})?|5[1-5][0-9]{14}|3[47][0-9]{13})\b", RegexOptions.Compiled)),
    ];

    private static readonly HashSet<string> BlockingTypes =
        ["SSN", "PrivateKey", "ConnectionString", "BearerToken", "JwtToken", "CreditCard",
         "CommandInjection", "SystemPromptOverride", "HiddenInstruction"];

    private const double EntropyThreshold = 4.5;
    private const int MinTokenLength = 20;
    private static readonly Regex TokenPattern = new($@"[A-Za-z0-9\-._~+/=]{{{MinTokenLength},}}", RegexOptions.Compiled);

    public AuditClient(HttpClient http) => _http = http;

    public async Task<string> ProcessAsync(string rawLine, string direction)
    {
        if (string.IsNullOrWhiteSpace(rawLine)) return rawLine;

        var (textToScan, method) = ExtractScanTarget(rawLine);
        if (string.IsNullOrEmpty(textToScan)) return rawLine;

        var findings = Scan(textToScan);
        if (findings.Count == 0) return rawLine;

        // Log async — never block the stdio pipe
        _ = LogAsync(direction, method, findings, textToScan);

        var blocking = findings.Where(f => BlockingTypes.Contains(f.Type) && f.Confidence >= 0.9).ToList();
        if (blocking.Count > 0)
        {
            var types = string.Join(", ", blocking.Select(f => f.Type).Distinct());
            Console.Error.WriteLine($"[APEX-PROXY] ⚠ REDACTED {direction} ({method}): {types}");
            return Redact(rawLine, blocking);
        }

        var types2 = string.Join(", ", findings.Select(f => f.Type).Distinct());
        Console.Error.WriteLine($"[APEX-PROXY] ℹ Logged {direction} ({method}): {types2}");
        return rawLine;
    }

    private static List<ProxyFinding> Scan(string text)
    {
        var findings = new List<ProxyFinding>();

        // Stage 1: PII/Secret regex patterns
        foreach (var (name, pattern) in Patterns)
            if (pattern.IsMatch(text))
                findings.Add(new(name, "Regex", 1.0));

        // Stage 2: Shannon entropy for unknown secrets
        foreach (Match m in TokenPattern.Matches(text))
        {
            var e = Entropy(m.Value);
            if (e > EntropyThreshold)
                findings.Add(new("HighEntropySecret", "Entropy", Math.Min(e / 6.0, 1.0)));
        }

        // Stage 3: Prompt injection detection
        findings.AddRange(DetectPromptInjection(text));

        // Stage 4: Command injection detection
        findings.AddRange(DetectCommandInjection(text));

        return findings;
    }

    // ── Stage 3: Prompt Injection Detection (ported from CrunchTime MCP) ──────
    private static readonly (string Name, Regex Pattern, double BaseConfidence)[] PromptInjectionPatterns =
    [
        // Role manipulation
        ("SystemPromptOverride", new Regex(@"ignore\s+(all\s+)?((previous|prior|above)\s+)?(instructions|commands)", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.85),
        ("SystemPromptOverride", new Regex(@"disregard\s+(all\s+)?((previous|prior)\s+)?(instructions|commands|rules)", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.85),
        ("SystemPromptOverride", new Regex(@"forget\s+(everything|all)\s+(before|above)", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.80),
        // Direct override
        ("SystemPromptOverride", new Regex(@"system\s*:\s*you\s+are\s+(now|a)", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.95),
        ("SystemPromptOverride", new Regex(@"you\s+are\s+(now\s+)?a\s+[\w\s]+\s+(without|with\s+no)\s+(restrictions|limits)", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.95),
        ("SystemPromptOverride", new Regex(@"override\s+(safety|security|ethical)\s+(protocols|guidelines|restrictions)", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.95),
        ("SystemPromptOverride", new Regex(@"ignore\s+your\s+(programming|training|guidelines)", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.90),
        // Jailbreak
        ("JailbreakAttempt", new Regex(@"pretend\s+you\s+are", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.70),
        ("JailbreakAttempt", new Regex(@"act\s+as\s+(if|though)\s+you", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.65),
        ("JailbreakAttempt", new Regex(@"simulate\s+being", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.65),
        ("JailbreakAttempt", new Regex(@"roleplay\s+as", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.60),
        // Hidden instruction markers
        ("HiddenInstruction", new Regex(@"\[INST\]", RegexOptions.Compiled), 0.90),
        ("HiddenInstruction", new Regex(@"<\|im_start\|>", RegexOptions.Compiled), 0.90),
        ("HiddenInstruction", new Regex(@"###\s*Instruction", RegexOptions.IgnoreCase | RegexOptions.Compiled), 0.80),
    ];

    private static List<ProxyFinding> DetectPromptInjection(string text)
    {
        var findings = new List<ProxyFinding>();
        foreach (var (name, pattern, confidence) in PromptInjectionPatterns)
        {
            var matches = pattern.Matches(text);
            if (matches.Count > 0)
            {
                // Multiple matches boost confidence
                var boosted = Math.Min(confidence + (matches.Count - 1) * 0.05, 1.0);
                findings.Add(new(name, "PromptInjection", boosted));
            }
        }
        return findings;
    }

    // ── Stage 4: Command Injection Detection (ported from CrunchTime MCP) ─────
    private static readonly string[] DangerousCommands =
    [
        "rm -rf", "del /f", "format c:", "mkfs",
        "dd if=", ":(){ :|:& };:",  // Fork bomb
        "chmod 777", "chown root",
        "sudo ", "su -",
        "eval(", "exec(",
        "net user", "net localgroup",
        "reg delete", "reg add",
    ];

    private static readonly Regex PathTraversalPattern = new(@"\.\.[/\\]", RegexOptions.Compiled);

    private static readonly string[] UrlEncodedShellChars =
    [
        "%3B", "%7C", "%26", "%24", "%60", // ; | & $ `
        "%0A", "%0D",                       // newlines
        "%3C", "%3E",                       // < >
    ];

    private static List<ProxyFinding> DetectCommandInjection(string text)
    {
        var findings = new List<ProxyFinding>();

        // Dangerous commands (auto-block)
        foreach (var cmd in DangerousCommands)
        {
            if (text.Contains(cmd, StringComparison.OrdinalIgnoreCase))
                findings.Add(new("CommandInjection", "CommandInjection", 0.95));
        }

        // Path traversal
        var traversals = PathTraversalPattern.Matches(text);
        if (traversals.Count > 0)
        {
            var confidence = traversals.Count > 3 ? 0.90 : 0.70;
            findings.Add(new("PathTraversal", "CommandInjection", confidence));
        }

        // URL-encoded shell characters (bypass attempts)
        if (UrlEncodedShellChars.Any(enc => text.Contains(enc, StringComparison.OrdinalIgnoreCase)))
            findings.Add(new("EncodingBypass", "CommandInjection", 0.80));

        return findings;
    }

    private static (string text, string method) ExtractScanTarget(string rawLine)
    {
        try
        {
            var node = JsonNode.Parse(rawLine);
            if (node is null) return (string.Empty, "unknown");

            var method = node["method"]?.GetValue<string>() ?? "response";

            // Tool call: scan params.arguments
            var arguments = node["params"]?["arguments"];
            if (arguments is not null)
                return (arguments.ToJsonString(), method);

            // Tool result: scan result.content
            var content = node["result"]?["content"];
            if (content is not null)
                return (content.ToJsonString(), method);

            // Fallback: scan the whole params/result blob
            var target = node["params"] ?? node["result"];
            return target is not null
                ? (target.ToJsonString(), method)
                : (string.Empty, method);
        }
        catch { return (string.Empty, "unknown"); }
    }

    private static string Redact(string rawLine, List<ProxyFinding> blocking)
    {
        var result = rawLine;
        var blockedTypes = blocking.Select(f => f.Type).ToHashSet();

        foreach (var (_, p) in Patterns)
            result = p.Replace(result, "[REDACTED]");

        foreach (var (name, pattern, _) in PromptInjectionPatterns)
            if (blockedTypes.Contains(name))
                result = pattern.Replace(result, "[BLOCKED]");

        if (blockedTypes.Contains("CommandInjection"))
            foreach (var cmd in DangerousCommands)
                result = Regex.Replace(result, Regex.Escape(cmd), "[BLOCKED]", RegexOptions.IgnoreCase);

        return result;
    }

    private async Task LogAsync(string direction, string method, List<ProxyFinding> findings, string snippet)
    {
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                source = "apex-proxy",
                direction,
                method,
                findings = findings.Select(f => new { f.Type, f.Stage, f.Confidence }),
                snippet = snippet.Length > 300 ? snippet[..300] + "…" : snippet,
                timestamp = DateTime.UtcNow
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            await _http.PostAsync("/api/proxy-audit/log", content);
        }
        catch { /* APEX API unavailable — proxy keeps running */ }
    }

    private static double Entropy(string s)
    {
        var freq = new Dictionary<char, int>();
        foreach (var c in s) freq[c] = freq.GetValueOrDefault(c) + 1;
        var len = (double)s.Length;
        return freq.Values.Sum(n => { var p = n / len; return -p * Math.Log2(p); });
    }
}

public record ProxyFinding(string Type, string Stage, double Confidence);
