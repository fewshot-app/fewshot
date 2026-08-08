using Fewshot.Core.Enums;
using Fewshot.Core.Models;

namespace Fewshot.Core.Interfaces;

public interface IMessageRepository
{
    Task<Message> CreateAsync(int sessionId, MessageRole role, string content, int? tokenCount = null);
    Task<List<Message>> GetBySessionAsync(int sessionId);
    Task<int> GetCorrectionCountAsync(int sessionId);
    Task<int> GetReExplanationCountAsync(int sessionId);
}
