using StarkTrace.Core.Interfaces;
using StarkTrace.Core.Models;
using StarkTrace.Core.Enums;
using StarkTrace.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace StarkTrace.Infrastructure.Services;

public class MessageService : IMessageService
{
    private readonly StarkTraceDbContext _db;

    public MessageService(StarkTraceDbContext db) => _db = db;

    public async Task<Message> LogMessageAsync(int sessionId, MessageRole role, string content, int? tokenCount = null)
    {
        var msg = new Message
        {
            SessionId = sessionId,
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow,
            TokenCount = tokenCount
        };
        _db.Messages.Add(msg);
        await _db.SaveChangesAsync();
        return msg;
    }

    public async Task<List<Message>> GetSessionMessagesAsync(int sessionId)
    {
        return await _db.Messages
            .Where(m => m.SessionId == sessionId)
            .OrderBy(m => m.Timestamp)
            .ToListAsync();
    }

    public async Task<int> GetCorrectionCountAsync(int sessionId)
    {
        return await _db.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS [Value] FROM Messages
                WHERE SessionId = {0} AND Role = 'User'
                AND (
                    Content LIKE '%that''s not%' OR Content LIKE '%that is not%'
                    OR Content LIKE '%no, %wrong%' OR Content LIKE '%incorrect%'
                    OR Content LIKE '%I already told%' OR Content LIKE '%I already said%'
                    OR Content LIKE '%as I mentioned%' OR Content LIKE '%let me re-explain%'
                    OR Content LIKE '%let me reexplain%' OR Content LIKE '%let me clarify%'
                )
                """, sessionId)
            .SingleAsync();
    }

    public async Task<int> GetRepeatExplanationCountAsync(int sessionId)
    {
        return await _db.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*) AS [Value] FROM (
                    SELECT Content, ROW_NUMBER() OVER (ORDER BY Timestamp) AS MsgNum
                    FROM Messages
                    WHERE SessionId = {0} AND Role = 'User'
                ) ranked
                WHERE MsgNum <= 3 AND LENGTH(Content) > 500
                """, sessionId)
            .SingleAsync();
    }
}
