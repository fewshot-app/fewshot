using System.Text;
using System.Text.Json;
using StarkTrace.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace StarkTrace.Infrastructure.Memory;

/// <summary>
/// Calls local Ollama gemma4 for text generation and structured extraction.
/// </summary>
public class LlmService : ILlmService
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<LlmService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public LlmService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<LlmService> logger)
    {
        _http = httpFactory.CreateClient("Ollama");
        _model = config["StarkTrace:Ollama:SummarizationModel"] ?? "gemma4";
        _logger = logger;
    }

    public async Task<string> GenerateAsync(string prompt, string? systemPrompt = null, double temperature = 0.3, bool jsonFormat = false)
    {
        var request = new Dictionary<string, object>
        {
            ["model"] = _model,
            ["prompt"] = prompt,
            ["stream"] = false,
            ["options"] = new { temperature, num_predict = 8192 }
        };

        if (jsonFormat)
            request["format"] = "json"; // grammar-constrained: Ollama guarantees syntactically valid JSON

        if (systemPrompt != null)
            request["system"] = systemPrompt;

        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        _logger.LogDebug("Ollama generate: {Model}, prompt length: {Len}", _model, prompt.Length);

        var response = await _http.PostAsync("/api/generate", content);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<OllamaGenerateResponse>(responseJson, JsonOptions);

        _logger.LogDebug("Ollama response: {Len} chars, {Duration}ms",
            result?.Response?.Length ?? 0, result?.TotalDuration / 1_000_000);

        return result?.Response ?? string.Empty;
    }

    public async Task<T?> GenerateJsonAsync<T>(string prompt, string? systemPrompt = null, double temperature = 0.1) where T : class
    {
        // Append JSON instruction
        var jsonPrompt = prompt + "\n\nRespond with ONLY valid JSON, no markdown, no explanation.";

        var raw = await GenerateAsync(jsonPrompt, systemPrompt, temperature, jsonFormat: true);
        _logger.LogInformation("Raw Ollama JSON response ({Len} chars): {Raw}",
            raw.Length, raw[..Math.Min(1000, raw.Length)]);

        // Strip markdown fences if present
        raw = raw.Trim();
        if (raw.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
            raw = raw[7..];
        else if (raw.StartsWith("```"))
            raw = raw[3..];
        if (raw.EndsWith("```"))
            raw = raw[..^3];
        raw = raw.Trim();

        if (string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning("Ollama returned empty response after stripping");
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(raw, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning("Failed to parse Ollama JSON response: {Error}\nRaw: {Raw}",
                ex.Message, raw[..Math.Min(500, raw.Length)]);
            return null;
        }
    }

    private class OllamaGenerateResponse
    {
        public string Response { get; set; } = string.Empty;
        public long TotalDuration { get; set; }
        public int PromptEvalCount { get; set; }
        public int EvalCount { get; set; }
    }
}
