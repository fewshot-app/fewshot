using Apex.Core.Models;
using Apex.Core.Enums;

namespace Apex.Core.Interfaces;

public interface IMessageService
{
    Task<Message> LogMessageAsync(int sessionId, MessageRole role, string content, int? tokenCount = null);
    Task<List<Message>> GetSessionMessagesAsync(int sessionId);
    Task<int> GetCorrectionCountAsync(int sessionId);
    Task<int> GetRepeatExplanationCountAsync(int sessionId);
}
