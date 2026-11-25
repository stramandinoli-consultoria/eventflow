using EventFlow.Api.Infrastructure;
using EventFlow.Application.Common;
using EventFlow.Application.Features.Dashboard;
using EventFlow.Application.Features.Orders;
using EventFlow.Application.Features.Webhooks;
using EventFlow.Domain;
using Microsoft.AspNetCore.Mvc;

namespace EventFlow.Api.Endpoints;

internal static class OrderEndpoints
{
    /// <summary>Cabeçalho com a chave do pedido feito sem login.</summary>
    public const string OrderKeyHeader = "X-Order-Key";

    public sealed record LookupBody(IReadOnlyList<OrderKey> Orders);
    public sealed record StatusBody(OrderStatus Status, string? Note);
    public sealed record BulkStatusBody(IReadOnlyList<string> Ids, OrderStatus Status, string? Note);
    public sealed record RetryBody(Guid? DeliveryId);

    public static void MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var orders = app.MapGroup("/api/orders").WithTags("Pedidos");

        orders.MapPost("/", async (PlaceOrderCommand cmd, IDispatcher d, CancellationToken ct) =>
                (await d.Send(cmd, ct)).ToHttp(r => Results.Created($"/api/orders/{r.Order.Id}", r)))
            .WithSummary("Faz um pedido (com ou sem login). Devolve a chave para acompanhar o pedido sem login.");

        orders.MapGet("/", async ([AsParameters] ListOrdersParams p, IDispatcher d, CancellationToken ct) =>
                EnumCodes.TryParse<OrderStatus>(p.Status, out var status)
                    ? (await d.Send(new ListOrdersQuery(p.EventId, p.StandId, p.StandName, status, p.Page ?? 1, p.Limit ?? 25,
                        p.Sort == "created_at_asc"), ct)).ToHttp()
                    : EnumCodes.Invalid("status"))
            .RequireAuthorization()
            .WithSummary("Pedidos visíveis para o usuário (admin: todos; parceiro: do seu stand; cliente: os seus)");

        orders.MapPost("/lookup", async (LookupBody body, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new LookupOrdersQuery(body.Orders), ct)).ToHttp())
            .WithSummary("Busca pedidos feitos sem login a partir de id + chave");

        orders.MapGet("/{id}", async (string id, [FromHeader(Name = OrderKeyHeader)] string? key, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new GetOrderQuery(id, key), ct)).ToHttp())
            .WithSummary("Detalhe do pedido com itens e timeline");

        orders.MapGet("/{id}/timeline", async (string id, [FromHeader(Name = OrderKeyHeader)] string? key, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new GetOrderQuery(id, key), ct)).ToHttp(o => Results.Ok(o.Timeline)))
            .WithSummary("Timeline do pedido");

        orders.MapPost("/{id}/advance", async (string id, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new AdvanceOrderCommand(id), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Avança para o próximo status (admin ou parceiro do stand)");

        orders.MapPost("/{id}/cancel", async (string id, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new CancelOrderCommand(id), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Cancela o pedido (admin ou parceiro do stand)");

        orders.MapPatch("/{id}/status", async (string id, StatusBody b, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new ChangeOrderStatusCommand(id, b.Status, b.Note), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Define o status diretamente e dispara webhooks");

        orders.MapPatch("/status", async (BulkStatusBody b, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new BulkChangeOrderStatusCommand(b.Ids, b.Status, b.Note), ct)).ToHttp())
            .RequireAuthorization().WithSummary("Atualiza o status de vários pedidos");

        app.MapGet("/api/admin/dashboard", async (string? eventId, IDispatcher d, CancellationToken ct) =>
                (await d.Send(new GetDashboardQuery(eventId), ct)).ToHttp())
            .RequireAuthorization().WithTags("Dashboard").WithSummary("Receita, pedidos, ranking e tempo médio (admin)");

        var webhooks = app.MapGroup("/api/webhooks").WithTags("Webhooks").RequireAuthorization();
        webhooks.MapGet("/", async (IDispatcher d, CancellationToken ct) => (await d.Send(new ListWebhooksQuery(), ct)).ToHttp());
        webhooks.MapPost("/", async (SubscribeWebhookCommand cmd, IDispatcher d, CancellationToken ct) =>
            (await d.Send(cmd, ct)).ToHttp(r => Results.Created($"/api/webhooks/{r.Webhook.Id}", r)))
            .WithSummary("Cadastra webhook (o segredo HMAC só aparece nesta resposta)");
        webhooks.MapDelete("/{id:guid}", async (Guid id, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new UnsubscribeWebhookCommand(id), ct)).ToNoContent());
        webhooks.MapGet("/deliveries", async ([AsParameters] DeliveriesParams p, IDispatcher d, CancellationToken ct) =>
            EnumCodes.TryParse<DeliveryStatus>(p.Status, out var status)
                ? (await d.Send(new ListDeliveriesQuery(status, p.WebhookId, p.OrderId, p.Limit ?? 25, p.Offset ?? 0), ct)).ToHttp()
                : EnumCodes.Invalid("status"));
        webhooks.MapPost("/deliveries/retry", async (RetryBody? b, IDispatcher d, CancellationToken ct) =>
            (await d.Send(new RetryDeliveriesCommand(b?.DeliveryId), ct)).ToHttp());
    }

    public sealed record ListOrdersParams(string? EventId, string? StandId, string? StandName, string? Status,
        int? Page, int? Limit, string? Sort);

    public sealed record DeliveriesParams(string? Status, Guid? WebhookId, string? OrderId, int? Limit, int? Offset);
}
