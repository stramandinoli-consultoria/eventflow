using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Application.Dtos;
using EventFlow.Domain;
using EventFlow.Domain.Webhooks;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Application.Features.Webhooks;

public sealed record SubscribedWebhookDto(WebhookDto Webhook, string Secret);
public sealed record SubscribeWebhookCommand(string Url, string? EventId, string? StandName) : ICommand<SubscribedWebhookDto>;
public sealed record ListWebhooksQuery : IQuery<IReadOnlyList<WebhookDto>>;
public sealed record UnsubscribeWebhookCommand(Guid Id) : ICommand<Unit>;
public sealed record ListDeliveriesQuery(DeliveryStatus? Status = null, Guid? WebhookId = null, string? OrderId = null,
    int Limit = 25, int Offset = 0) : IQuery<DeliveriesPage>;
public sealed record DeliveriesPage(IReadOnlyList<DeliveryDto> Deliveries, int Total, int Limit, int Offset)
{
    public bool HasMore => Offset + Deliveries.Count < Total;
}
public sealed record RetryDeliveriesCommand(Guid? DeliveryId = null) : ICommand<IReadOnlyList<DeliveryAttemptDto>>;

public sealed class SubscribeWebhookValidator : AbstractValidator<SubscribeWebhookCommand>
{
    public SubscribeWebhookValidator()
    {
        RuleFor(x => x.Url).NotEmpty().MaximumLength(2000).HttpUrl();
        RuleFor(x => x.EventId).MaximumLength(64);
        RuleFor(x => x.StandName).MaximumLength(120);
    }
}

public sealed class ListDeliveriesValidator : AbstractValidator<ListDeliveriesQuery>
{
    public ListDeliveriesValidator()
    {
        RuleFor(x => x.Limit).InclusiveBetween(1, 100);
        RuleFor(x => x.Offset).GreaterThanOrEqualTo(0);
    }
}

internal sealed class SubscribeWebhookHandler(IAppDbContext db, ICurrentUser user, ICrypto crypto, TimeProvider clock)
    : IRequestHandler<SubscribeWebhookCommand, SubscribedWebhookDto>
{
    public async Task<Result<SubscribedWebhookDto>> Handle(SubscribeWebhookCommand cmd, CancellationToken ct)
    {
        if (user.Id is null) return Error.Unauthorized();
        if (user.Role is not (UserRole.Admin or UserRole.Partner))
            return Error.Forbidden("Apenas organizadores e empresas parceiras podem cadastrar webhooks.");
        var webhook = Webhook.Subscribe(user.Id, cmd.Url, crypto.RandomToken(32), cmd.EventId, cmd.StandName, clock.GetUtcNow());
        db.Webhooks.Add(webhook);
        await db.SaveChangesAsync(ct);
        return new SubscribedWebhookDto(webhook.ToDto(), webhook.Secret);
    }
}

internal sealed class ListWebhooksHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<ListWebhooksQuery, IReadOnlyList<WebhookDto>>
{
    public async Task<Result<IReadOnlyList<WebhookDto>>> Handle(ListWebhooksQuery query, CancellationToken ct)
    {
        if (user.Id is null) return Error.Unauthorized();
        var list = await db.Webhooks.AsNoTracking().Where(w => w.OwnerId == user.Id)
            .OrderByDescending(w => w.CreatedAt).ToListAsync(ct);
        return list.Select(w => w.ToDto()).ToList();
    }
}

internal sealed class UnsubscribeWebhookHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<UnsubscribeWebhookCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UnsubscribeWebhookCommand cmd, CancellationToken ct)
    {
        if (user.Id is null) return Error.Unauthorized();
        var webhook = await db.Webhooks.FirstOrDefaultAsync(w => w.Id == cmd.Id && w.OwnerId == user.Id, ct);
        if (webhook is null) return Error.NotFound("Webhook não encontrado.");
        db.Webhooks.Remove(webhook);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

internal sealed class ListDeliveriesHandler(IAppDbContext db, ICurrentUser user) : IRequestHandler<ListDeliveriesQuery, DeliveriesPage>
{
    public async Task<Result<DeliveriesPage>> Handle(ListDeliveriesQuery query, CancellationToken ct)
    {
        if (user.Id is null) return Error.Unauthorized();
        var deliveries = db.WebhookDeliveries.AsNoTracking().Where(d => d.OwnerId == user.Id);
        if (query.Status is { } status) deliveries = deliveries.Where(d => d.Status == status);
        if (query.WebhookId is { } webhookId) deliveries = deliveries.Where(d => d.WebhookId == webhookId);
        if (!string.IsNullOrEmpty(query.OrderId)) deliveries = deliveries.Where(d => d.OrderId == query.OrderId);

        var total = await deliveries.CountAsync(ct);
        var page = await deliveries.OrderByDescending(d => d.CreatedAt).Skip(query.Offset).Take(query.Limit).ToListAsync(ct);
        return new DeliveriesPage(page.Select(d => d.ToDto()).ToList(), total, query.Limit, query.Offset);
    }
}

internal sealed class RetryDeliveriesHandler(IAppDbContext db, ICurrentUser user, IWebhookDispatcher dispatcher, TimeProvider clock)
    : IRequestHandler<RetryDeliveriesCommand, IReadOnlyList<DeliveryAttemptDto>>
{
    public async Task<Result<IReadOnlyList<DeliveryAttemptDto>>> Handle(RetryDeliveriesCommand cmd, CancellationToken ct)
    {
        if (user.Id is null) return Error.Unauthorized();
        if (cmd.DeliveryId is null) return Result<IReadOnlyList<DeliveryAttemptDto>>.Ok(await dispatcher.ProcessDueAsync(50, user.Id, ct));

        var delivery = await db.WebhookDeliveries.FirstOrDefaultAsync(d => d.Id == cmd.DeliveryId && d.OwnerId == user.Id, ct);
        if (delivery is null) return Error.NotFound($"Entrega {cmd.DeliveryId} não encontrada.");
        delivery.AllowManualRetry(clock.GetUtcNow());
        return new List<DeliveryAttemptDto> { await dispatcher.AttemptAsync(delivery, ct) };
    }
}
