namespace EventFlow.Domain.Auditing;

/// <summary>Registro de quem alterou o quê (ações de admin e parceiros).</summary>
public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public string? UserId { get; private set; }
    public string Action { get; private set; } = default!;
    public string Entity { get; private set; } = default!;
    public string EntityId { get; private set; } = default!;
    public string? Data { get; private set; }
    public DateTimeOffset At { get; private set; }

    private AuditLog() { }

    public static AuditLog Record(string? userId, string action, string entity, string entityId, string? data, DateTimeOffset at) =>
        new() { Id = Guid.NewGuid(), UserId = userId, Action = action, Entity = entity, EntityId = entityId, Data = data, At = at };
}
