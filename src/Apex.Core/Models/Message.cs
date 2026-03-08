using Apex.Core.Enums;

namespace Apex.Core.Models;

public class Message
{
    public int MessageId { get; set; }
    public int SessionId { get; set; }
    public MessageRole Role { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public int? TokenCount { get; set; }
}
