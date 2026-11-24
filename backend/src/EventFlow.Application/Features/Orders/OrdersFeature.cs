using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Application.Dtos;
using EventFlow.Domain;
using EventFlow.Domain.Orders;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EventFlow.Application.Features.Orders;

public sealed record PlaceOrderLine(string ProductId, int Qty);
public sealed record PlaceOrderCommand(string StandId, string CustomerName, string CustomerPhone,
    IReadOnlyList<PlaceOrderLine> Items) : ICommand<PlacedOrderDto>;

public sealed record GetOrderQuery(string Id, string? AccessKey) : IQuery<OrderDto>;
public sealed record OrderKey(string Id, string Key);
/// <summary>Pedidos feitos sem login neste aparelho (o front guarda id + chave).</summary>
public sealed record LookupOrdersQuery(IReadOnlyList<OrderKey> Keys) : IQuery<IReadOnlyList<OrderDto>>;

public sealed record ListOrdersQuery(string? EventId = null, string? StandId = null, string? StandName = null,
    OrderStatus? Status = null, int Page = 1, int Limit = 25, bool Ascending = false) : IQuery<Paged<OrderDto>>;

public sealed record AdvanceOrderCommand(string Id) : ICommand<OrderDto>;
public sealed record CancelOrderCommand(string Id) : ICommand<OrderDto>;

public sealed record StatusChangeResult(OrderDto Order, OrderStatus PreviousStatus, TimelineEventDto TimelineEntry,
    WebhookDispatchSummary Webhooks);
public sealed record ChangeOrderStatusCommand(string Id, OrderStatus Status, string? Note) : ICommand<StatusChangeResult>;

public sealed record BulkItemResult(string OrderId, bool Ok, OrderStatus? PreviousStatus, string? Skipped, string? Error);
public sealed record BulkStatusResult(OrderStatus Status, int Updated, int Skipped, int Failed,
    IReadOnlyList<BulkItemResult> Results, WebhookDispatchSummary Webhooks);
public sealed record BulkChangeOrderStatusCommand(IReadOnlyList<string> Ids, OrderStatus Status, string? Note)
    : ICommand<BulkStatusResult>;

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderCommand>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.StandId).ValidId();
        RuleFor(x => x.CustomerName).NotEmpty().WithMessage("Informe nome e WhatsApp").MaximumLength(120);
        RuleFor(x => x.CustomerPhone).NotEmpty().WithMessage("Informe nome e WhatsApp").MaximumLength(40);
        RuleFor(x => x.Items).NotEmpty().WithMessage("Adicione produtos");
        RuleForEach(x => x.Items).ChildRules(i =>
        {
            i.RuleFor(x => x.ProductId).ValidId();
            i.RuleFor(x => x.Qty).InclusiveBetween(1, Order.MaxQuantityPerItem);
        });
    }
}

public sealed class ListOrdersValidator : AbstractValidator<ListOrdersQuery>
{
    public ListOrdersValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Limit).InclusiveBetween(1, 200);
    }
}

public sealed class ChangeOrderStatusValidator : AbstractValidator<ChangeOrderStatusCommand>
{
    public ChangeOrderStatusValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(280);
    }
}

public sealed class BulkChangeOrderStatusValidator : AbstractValidator<BulkChangeOrderStatusCommand>
{
    public BulkChangeOrderStatusValidator()
    {
        RuleFor(x => x.Ids).NotEmpty().Must(ids => ids.Count <= 100).WithMessage("Envie no máximo 100 pedidos.");
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(280);
    }
}

internal sealed class PlaceOrderHandler(IAppDbContext db, ICurrentUser user, ICrypto crypto, IOrderIdGenerator ids,
    IRealtimeNotifier realtime, TimeProvider clock) : IRequestHandler<PlaceOrderCommand, PlacedOrderDto>
{
    public async Task<Result<PlacedOrderDto>> Handle(PlaceOrderCommand cmd, CancellationToken ct)
    {
        var stand = await db.Stands.AsNoTracking().FirstOrDefaultAsync(s => s.Id == cmd.StandId, ct);
        if (stand is null) return Error.NotFound("Barraca não encontrada.");

        var productIds = cmd.Items.Select(i => i.ProductId).Distinct().ToList();
        var menu = await db.Products.AsNoTracking().Where(p => productIds.Contains(p.Id)).ToListAsync(ct);

        var accessKey = crypto.RandomToken(24);
        var order = Order.Place(await ids.NextAsync(ct), stand, menu,
            cmd.Items.Select(i => (i.ProductId, i.Qty)).ToList(),
            user.Id, cmd.CustomerName, cmd.CustomerPhone, crypto.Hash(accessKey), clock.GetUtcNow());

        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        var dto = order.ToDto();
        await realtime.OrderChangedAsync(dto, ct);
        return new PlacedOrderDto(dto, accessKey);
    }
}

internal sealed class GetOrderHandler(IAppDbContext db, AccessScope access) : IRequestHandler<GetOrderQuery, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(GetOrderQuery query, CancellationToken ct)
    {
        var order = await db.Orders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == query.Id, ct);
        // Mesmo erro para "não existe" e "sem acesso", para não revelar quais pedidos existem.
        if (order is null || !await access.CanViewOrderAsync(order, query.AccessKey, ct))
            return Error.NotFound("Pedido não encontrado.");
        return order.ToDto();
    }
}

internal sealed class LookupOrdersHandler(IAppDbContext db, ICrypto crypto) : IRequestHandler<LookupOrdersQuery, IReadOnlyList<OrderDto>>
{
    public async Task<Result<IReadOnlyList<OrderDto>>> Handle(LookupOrdersQuery query, CancellationToken ct)
    {
        var keys = query.Keys.Take(100).Where(k => !string.IsNullOrEmpty(k.Id) && !string.IsNullOrEmpty(k.Key))
            .GroupBy(k => k.Id).ToDictionary(g => g.Key, g => crypto.Hash(g.First().Key));
        var idList = keys.Keys.ToList();
        var orders = await db.Orders.AsNoTracking().Where(o => idList.Contains(o.Id)).ToListAsync(ct);
        return orders.Where(o => keys[o.Id] == o.AccessKeyHash)
            .OrderByDescending(o => o.CreatedAt).Select(o => o.ToDto()).ToList();
    }
}

internal sealed class ListOrdersHandler(AccessScope access) : IRequestHandler<ListOrdersQuery, Paged<OrderDto>>
{
    public async Task<Result<Paged<OrderDto>>> Handle(ListOrdersQuery query, CancellationToken ct)
    {
        if (!access.User.IsAuthenticated) return Error.Unauthorized();
        var orders = (await access.VisibleOrdersAsync(ct)).AsNoTracking();
        if (query.EventId is not null) orders = orders.Where(o => o.EventId == query.EventId);
        if (query.StandId is not null) orders = orders.Where(o => o.StandId == query.StandId);
        if (!string.IsNullOrWhiteSpace(query.StandName))
        {
            var name = query.StandName.Trim().ToLower();
            orders = orders.Where(o => o.StandName.ToLower().Contains(name));
        }
        if (query.Status is { } status) orders = orders.Where(o => o.Status == status);

        var total = await orders.CountAsync(ct);
        orders = query.Ascending
            ? orders.OrderBy(o => o.CreatedAt).ThenBy(o => o.Id)
            : orders.OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id);
        var page = await orders.Skip((query.Page - 1) * query.Limit).Take(query.Limit).ToListAsync(ct);
        return new Paged<OrderDto>(page.Select(o => o.ToDto()).ToList(), query.Page, query.Limit, total);
    }
}

/// <summary>Efeitos depois de uma mudança de status: WhatsApp, tempo real e webhooks.</summary>
public sealed class OrderStatusEffects(IRealtimeNotifier realtime, IWebhookDispatcher webhooks, IWhatsAppSender whatsApp,
    ILogger<OrderStatusEffects> logger)
{
    public async Task<WebhookDispatchSummary> RunAsync(Order order, OrderStatus previous, string? note, CancellationToken ct)
    {
        if (order.Status == OrderStatus.Ready)
        {
            try { await whatsApp.SendOrderReadyAsync(order, ct); }
            catch (Exception ex) { logger.LogWarning(ex, "Falha ao enviar WhatsApp do pedido {OrderId}", order.Id); }
        }

        await realtime.OrderChangedAsync(order.ToDto(), ct);

        try { return await webhooks.DispatchAsync(order, previous, note, ct); }
        catch (Exception ex)
        {
            // Falha de webhook nunca desfaz a mudança de status.
            logger.LogWarning(ex, "Falha ao disparar webhooks do pedido {OrderId}", order.Id);
            return WebhookDispatchSummary.Empty;
        }
    }
}

internal sealed class AdvanceOrderHandler(IAppDbContext db, AccessScope access, AuditTrail audit, OrderStatusEffects effects,
    TimeProvider clock) : IRequestHandler<AdvanceOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(AdvanceOrderCommand cmd, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == cmd.Id, ct);
        if (order is null) return Error.NotFound("Pedido não encontrado.");
        if (!await access.CanManageOrderAsync(order, ct)) return Error.Forbidden();

        var previous = order.Advance(clock.GetUtcNow());
        audit.Record("advance", "order", order.Id, new { from = previous, to = order.Status });
        await db.SaveChangesAsync(ct);
        await effects.RunAsync(order, previous, order.Timeline[^1].Note, ct);
        return order.ToDto();
    }
}

internal sealed class CancelOrderHandler(IAppDbContext db, AccessScope access, AuditTrail audit, OrderStatusEffects effects,
    TimeProvider clock) : IRequestHandler<CancelOrderCommand, OrderDto>
{
    public async Task<Result<OrderDto>> Handle(CancelOrderCommand cmd, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == cmd.Id, ct);
        if (order is null) return Error.NotFound("Pedido não encontrado.");
        if (!await access.CanManageOrderAsync(order, ct)) return Error.Forbidden();

        var previous = order.Cancel(clock.GetUtcNow());
        audit.Record("cancel", "order", order.Id, new { from = previous });
        await db.SaveChangesAsync(ct);
        await effects.RunAsync(order, previous, null, ct);
        return order.ToDto();
    }
}

internal sealed class ChangeOrderStatusHandler(IAppDbContext db, AccessScope access, AuditTrail audit,
    OrderStatusEffects effects, TimeProvider clock) : IRequestHandler<ChangeOrderStatusCommand, StatusChangeResult>
{
    public async Task<Result<StatusChangeResult>> Handle(ChangeOrderStatusCommand cmd, CancellationToken ct)
    {
        var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == cmd.Id, ct);
        if (order is null || !await access.CanManageOrderAsync(order, ct))
            return Error.NotFound($"Pedido {cmd.Id} não encontrado.");

        var previous = order.Status;
        if (!order.ChangeStatus(cmd.Status, cmd.Note, clock.GetUtcNow()))
            return Error.Rule($"O pedido {cmd.Id} já está neste status.");

        audit.Record("change_status", "order", order.Id, new { from = previous, to = cmd.Status, cmd.Note });
        await db.SaveChangesAsync(ct);
        var entry = order.Timeline[^1];
        var webhooks = await effects.RunAsync(order, previous, entry.Note, ct);
        return new StatusChangeResult(order.ToDto(), previous, new TimelineEventDto(entry.Status, entry.At, entry.Note), webhooks);
    }
}

internal sealed class BulkChangeOrderStatusHandler(IAppDbContext db, AccessScope access, AuditTrail audit,
    OrderStatusEffects effects, TimeProvider clock) : IRequestHandler<BulkChangeOrderStatusCommand, BulkStatusResult>
{
    public async Task<Result<BulkStatusResult>> Handle(BulkChangeOrderStatusCommand cmd, CancellationToken ct)
    {
        var ids = cmd.Ids.Select(i => i.Trim()).Where(i => i.Length > 0).Distinct().ToList();
        var orders = await db.Orders.Where(o => ids.Contains(o.Id)).ToDictionaryAsync(o => o.Id, ct);
        var results = new List<BulkItemResult>();
        var changed = new List<(Order Order, OrderStatus Previous)>();

        foreach (var id in ids)
        {
            if (!orders.TryGetValue(id, out var order) || !await access.CanManageOrderAsync(order, ct))
            {
                results.Add(new BulkItemResult(id, false, null, null, "não encontrado"));
                continue;
            }
            var previous = order.Status;
            if (!order.ChangeStatus(cmd.Status, cmd.Note, clock.GetUtcNow()))
            {
                results.Add(new BulkItemResult(id, true, previous, "já estava neste status", null));
                continue;
            }
            audit.Record("change_status", "order", order.Id, new { from = previous, to = cmd.Status, cmd.Note });
            changed.Add((order, previous));
            results.Add(new BulkItemResult(id, true, previous, null, null));
        }

        await db.SaveChangesAsync(ct);
        var webhooks = WebhookDispatchSummary.Empty;
        foreach (var (order, previous) in changed)
            webhooks = webhooks.Add(await effects.RunAsync(order, previous, order.Timeline[^1].Note, ct));

        return new BulkStatusResult(cmd.Status,
            Updated: results.Count(r => r.Ok && r.Skipped is null),
            Skipped: results.Count(r => r.Skipped is not null),
            Failed: results.Count(r => !r.Ok),
            results, webhooks);
    }
}
