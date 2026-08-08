using System.Text;
using System.Text.Json;
using StarkTrace.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace StarkTrace.Infrastructure.Memory;

/// <summary>
/// Generates 768-dim embeddings via local Ollama nomic-embed-text model.
/// </summary>
public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger<EmbeddingService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public EmbeddingService(IHttpClientFactory httpFactory, IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _http = httpFactory.CreateClient("Ollama");
        _model = config["StarkTrace:Ollama:EmbeddingModel"] ?? "nomic-embed-text";
        _logger = logger;
    }

    public async Task<float[]> EmbedAsync(string text)
    {
        // Try /api/embed first (Ollama 0.4+), fallback to /api/embeddings (legacy)
        var request = new { model = _model, input = text };
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _http.PostAsync("/api/embed", content);

        if (!response.IsSuccessStatusCode)
        {
            // Fallback to legacy endpoint
            var legacyRequest = new { model = _model, prompt = text };
            json = JsonSerializer.Serialize(legacyRequest);
            content = new StringContent(json, Encoding.UTF8, "application/json");
            response = await _http.PostAsync("/api/embeddings", content);
        }

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync();
        _logger.LogDebug("Ollama embedding response: {Response}", responseJson[..Math.Min(500, responseJson.Length)]);

        // Try new format first: { "embeddings": [[...]] }
        var newResult = JsonSerializer.Deserialize<OllamaEmbedResponse>(responseJson, JsonOptions);
        if (newResult?.Embeddings is { Count: > 0 } && newResult.Embeddings[0].Length > 0)
        {
            _logger.LogDebug("Generated {Dims}-dim embedding (new API)", newResult.Embeddings[0].Length);
            return newResult.Embeddings[0];
        }

        // Try legacy format: { "embedding": [...] }
        var legacyResult = JsonSerializer.Deserialize<OllamaEmbeddingResponse>(responseJson, JsonOptions);
        if (legacyResult?.Embedding is { Length: > 0 })
        {
            _logger.LogDebug("Generated {Dims}-dim embedding (legacy API)", legacyResult.Embedding.Length);
            return legacyResult.Embedding;
        }

        throw new InvalidOperationException($"Ollama returned empty embedding for model {_model}. Response: {responseJson[..Math.Min(200, responseJson.Length)]}");
    }

    public async Task<List<float[]>> EmbedBatchAsync(List<string> texts)
    {
        // Ollama doesn't support batch embeddings natively, so we parallelize
        var tasks = texts.Select(EmbedAsync);
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    private class OllamaEmbedResponse
    {
        public List<float[]> Embeddings { get; set; } = [];
    }

    private class OllamaEmbeddingResponse
    {
        public float[] Embedding { get; set; } = [];
    }
}
