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

var apexApiBase = Environment.GetEnvironmentVariable("APEX_API_URL") ?? "http://127.0.0.1:5000";
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
        ["SSN", "PrivateKey", "ConnectionString", "BearerToken", "JwtToken", "CreditCard"];

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
            return Redact(rawLine);
        }

        var types2 = string.Join(", ", findings.Select(f => f.Type).Distinct());
        Console.Error.WriteLine($"[APEX-PROXY] ℹ Logged {direction} ({method}): {types2}");
        return rawLine;
    }

    private static List<ProxyFinding> Scan(string text)
    {
        var findings = new List<ProxyFinding>();

        foreach (var (name, pattern) in Patterns)
            if (pattern.IsMatch(text))
                findings.Add(new(name, "Regex", 1.0));

        foreach (Match m in TokenPattern.Matches(text))
        {
            var e = Entropy(m.Value);
            if (e > EntropyThreshold)
                findings.Add(new("HighEntropySecret", "Entropy", Math.Min(e / 6.0, 1.0)));
        }

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

    private static string Redact(string rawLine)
    {
        var result = rawLine;
        foreach (var (_, p) in Patterns)
            result = p.Replace(result, "[REDACTED]");
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
