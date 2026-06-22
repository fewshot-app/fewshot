namespace Apex.Core.Interfaces;

/// <summary>
/// Calls local Ollama for text generation (summarization, extraction).
/// Uses gemma4 for structured output.
/// </summary>
public interface ILlmService
{
    /// <summary>
    /// Generate a completion from a prompt. Returns the raw text response.
    /// </summary>
    Task<string> GenerateAsync(string prompt, string? systemPrompt = null, double temperature = 0.3);

    /// <summary>
    /// Generate a structured JSON response from a prompt.
    /// </summary>
    Task<T?> GenerateJsonAsync<T>(string prompt, string? systemPrompt = null, double temperature = 0.1) where T : class;
}
