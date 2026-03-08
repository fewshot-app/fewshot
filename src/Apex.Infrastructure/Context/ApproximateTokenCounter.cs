using Apex.Core.Interfaces;

namespace Apex.Infrastructure.Context;

/// <summary>
/// Approximate token counter (~4 chars/token).
/// Replace with tiktoken or Anthropic's API for exact counts.
/// </summary>
public class ApproximateTokenCounter : ITokenCounter
{
    private const double CharsPerToken = 4.0;
    private const double BufferRatio = 0.9; // 10% safety margin

    public int Count(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (int)Math.Ceiling(text.Length / CharsPerToken);
    }

    public string TruncateToTokens(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text)) return text;

        var targetTokens = (int)(maxTokens * BufferRatio);
        var maxChars = (int)(targetTokens * CharsPerToken);

        if (text.Length <= maxChars) return text;

        // Truncate at last newline before limit to avoid breaking mid-line
        var truncated = text[..maxChars];
        var lastNewline = truncated.LastIndexOf('\n');
        return lastNewline > 0 ? truncated[..lastNewline] : truncated;
    }
}
