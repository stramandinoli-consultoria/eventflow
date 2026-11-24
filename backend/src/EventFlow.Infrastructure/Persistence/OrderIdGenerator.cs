using EventFlow.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Persistence;

/// <summary>Usa uma sequence do PostgreSQL: dois pedidos nunca recebem o mesmo número.</summary>
internal sealed class OrderIdGenerator(AppDbContext db) : IOrderIdGenerator
{
    private const string NextValueSql = "SELECT nextval('" + AppDbContext.OrderNumberSequence + "') AS \"Value\"";

    public async Task<string> NextAsync(CancellationToken ct)
    {
        var next = await db.Database.SqlQueryRaw<long>(NextValueSql).SingleAsync(ct);
        return $"ord-{next}";
    }
}
