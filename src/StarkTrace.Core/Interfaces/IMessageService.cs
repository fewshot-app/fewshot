using StarkTrace.Core.Models;
using StarkTrace.Core.Enums;

namespace StarkTrace.Core.Interfaces;

public interface IMessageService
{
    Task<Message> LogMessageAsync(int sessionId, MessageRole role, string content, int? tokenCount = null);
    Task<List<Message>> GetSessionMessagesAsync(int sessionId);
    Task<int> GetCorrectionCountAsync(int sessionId);
    Task<int> GetRepeatExplanationCountAsync(int sessionId);
}
