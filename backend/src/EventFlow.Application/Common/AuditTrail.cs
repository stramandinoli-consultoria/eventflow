using System.Text.Json;
using EventFlow.Application.Abstractions;
using EventFlow.Domain.Auditing;

namespace EventFlow.Application.Common;

/// <summary>Adiciona um registro de auditoria; é gravado junto com o SaveChanges do caso de uso.</summary>
public sealed class AuditTrail(IAppDbContext db, ICurrentUser user, TimeProvider clock)
{
    public void Record(string action, string entity, string entityId, object? data = null) =>
        db.AuditLogs.Add(AuditLog.Record(user.Id, action, entity, entityId,
            data is null ? null : JsonSerializer.Serialize(data), clock.GetUtcNow()));
}
