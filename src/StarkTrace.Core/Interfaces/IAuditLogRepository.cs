using StarkTrace.Core.Models;

namespace StarkTrace.Core.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog> CreateAsync(AuditLog entry);
    Task<List<AuditLog>> GetBySessionAsync(int sessionId);
    Task<List<AuditLog>> GetBlockedAsync(int count = 50);
}
