using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Domain;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Application.Features.Dashboard;

public sealed record RankingItem(string StandId, string Name, decimal Total, int Count);
public sealed record DashboardDto(decimal Revenue, int Orders, int Active, int Stands, double AveragePrepMinutes,
    IReadOnlyList<RankingItem> Ranking);
public sealed record GetDashboardQuery(string? EventId = null) : IQuery<DashboardDto>;

/// <summary>Números do painel administrativo: receita, pedidos, ranking e tempo médio de preparo.</summary>
internal sealed class GetDashboardHandler(IAppDbContext db, AccessScope access) : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    private static readonly StandType[] VendorTypes = [StandType.Food, StandType.Drink, StandType.Beer, StandType.Dessert];

    public async Task<Result<DashboardDto>> Handle(GetDashboardQuery query, CancellationToken ct)
    {
        if (!access.IsAdmin) return Error.Forbidden();

        var orders = db.Orders.AsNoTracking();
        var stands = db.Stands.AsNoTracking();
        if (query.EventId is not null)
        {
            orders = orders.Where(o => o.EventId == query.EventId);
            stands = stands.Where(s => s.EventId == query.EventId);
        }

        var list = await orders.ToListAsync(ct);
        var paid = list.Where(o => o.Status != OrderStatus.Cancelled).ToList();
        var ranking = paid.GroupBy(o => o.StandId)
            .Select(g => new RankingItem(g.Key, g.First().StandName, g.Sum(o => o.Total), g.Count()))
            .OrderByDescending(r => r.Total).Take(5).ToList();

        // Tempo médio entre "em preparo" e "pronto".
        var prepTimes = list
            .Select(o => (Start: o.Timeline.FirstOrDefault(t => t.Status == OrderStatus.Preparing)?.At,
                          End: o.Timeline.FirstOrDefault(t => t.Status == OrderStatus.Ready)?.At))
            .Where(t => t.Start is not null && t.End is not null)
            .Select(t => (t.End!.Value - t.Start!.Value).TotalMinutes)
            .ToList();

        return new DashboardDto(
            Revenue: paid.Sum(o => o.Total),
            Orders: list.Count,
            Active: list.Count(o => o.Status is OrderStatus.AwaitingPayment or OrderStatus.Preparing),
            Stands: await stands.CountAsync(s => VendorTypes.Contains(s.Category), ct),
            AveragePrepMinutes: prepTimes.Count == 0 ? 0 : Math.Round(prepTimes.Average(), 1),
            Ranking: ranking);
    }
}
