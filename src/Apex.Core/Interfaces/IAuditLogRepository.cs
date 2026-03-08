using Apex.Core.Models;

namespace Apex.Core.Interfaces;

public interface IAuditLogRepository
{
    Task<AuditLog> CreateAsync(AuditLog entry);
    Task<List<AuditLog>> GetBySessionAsync(int sessionId);
    Task<List<AuditLog>> GetBlockedAsync(int count = 50);
}
