using Fewshot.Core.Models;

namespace Fewshot.Core.Interfaces;

/// <summary>
/// Generates vector embeddings from text using the local Ollama instance.
/// Uses nomic-embed-text (768 dimensions).
/// </summary>
public interface IEmbeddingService
{
    Task<float[]> EmbedAsync(string text);
    Task<List<float[]>> EmbedBatchAsync(List<string> texts);
}
