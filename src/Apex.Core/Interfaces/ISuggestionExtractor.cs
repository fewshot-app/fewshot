using Apex.Core.Models;

namespace Apex.Core.Interfaces;

/// <summary>
/// Extracts suggestions from Claude's response messages (async background job).
/// </summary>
public interface ISuggestionExtractor
{
    Task<List<Suggestion>> ExtractAsync(int messageId, string content);
}
