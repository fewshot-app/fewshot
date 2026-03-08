using Apex.Core.Enums;
using Apex.Core.Models;

namespace Apex.Core.Interfaces;

public interface IMessageRepository
{
    Task<Message> CreateAsync(int sessionId, MessageRole role, string content, int? tokenCount = null);
    Task<List<Message>> GetBySessionAsync(int sessionId);
    Task<int> GetCorrectionCountAsync(int sessionId);
    Task<int> GetReExplanationCountAsync(int sessionId);
}
