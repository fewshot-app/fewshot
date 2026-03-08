namespace Apex.Core.Interfaces;

/// <summary>
/// Counts tokens for context budget management.
/// Wraps tiktoken or Anthropic's token counting API.
/// </summary>
public interface ITokenCounter
{
    int Count(string text);
    string TruncateToTokens(string text, int maxTokens);
}
