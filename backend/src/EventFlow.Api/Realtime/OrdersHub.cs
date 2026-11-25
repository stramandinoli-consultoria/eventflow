using System.Security.Claims;
using EventFlow.Application.Abstractions;
using EventFlow.Application.Common;
using EventFlow.Application.Dtos;
using EventFlow.Application.Features.Orders;
using EventFlow.Infrastructure.Security;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Api.Realtime;

/// <summary>
/// Canal de tempo real. Cada conexão entra nos grupos que pode ver:
/// admins, o stand do parceiro, os pedidos do cliente e os pedidos acompanhados com chave.
/// </summary>
public sealed class OrdersHub(IAppDbContext db, IDispatcher dispatcher) : Hub
{
    public const string Path = "/hubs/orders";
    public const string OrderChanged = "orderChanged";
    public const string CatalogChanged = "catalogChanged";

    public static string AdminsGroup => "admins";
    public static string StandGroup(string standId) => $"stand:{standId}";
    public static string UserGroup(string userId) => $"user:{userId}";
    public static string OrderGroup(string orderId) => $"order:{orderId}";

    public override async Task OnConnectedAsync()
    {
        var ct = Context.ConnectionAborted;
        var principal = Context.User;
        var userId = principal?.FindFirstValue(AppClaims.Subject);
        var role = principal?.FindFirstValue(AppClaims.Role);

        if (userId is not null) await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId), ct);
        if (role == "admin") await Groups.AddToGroupAsync(Context.ConnectionId, AdminsGroup, ct);
        if (role == "partner" && principal!.FindFirstValue(AppClaims.PartnerId) is { } partnerId)
        {
            var standId = await db.Partners.Where(p => p.Id == partnerId).Select(p => p.StandId).FirstOrDefaultAsync(ct);
            if (standId is not null) await Groups.AddToGroupAsync(Context.ConnectionId, StandGroup(standId), ct);
        }
        await base.OnConnectedAsync();
    }

    /// <summary>Passa a receber as mudanças de um pedido feito sem login (exige a chave do pedido).</summary>
    public async Task<bool> JoinOrder(string orderId, string? accessKey)
    {
        var result = await dispatcher.Send(new LookupOrdersQuery([new OrderKey(orderId, accessKey ?? "")]), Context.ConnectionAborted);
        if (!result.IsSuccess || result.Value.Count == 0) return false;
        await Groups.AddToGroupAsync(Context.ConnectionId, OrderGroup(orderId), Context.ConnectionAborted);
        return true;
    }
}

internal sealed class SignalRNotifier(IHubContext<OrdersHub> hub) : IRealtimeNotifier
{
    public Task OrderChangedAsync(OrderDto order, CancellationToken ct)
    {
        var groups = new List<string> { OrdersHub.AdminsGroup, OrdersHub.StandGroup(order.StandId), OrdersHub.OrderGroup(order.Id) };
        if (order.CustomerId is not null) groups.Add(OrdersHub.UserGroup(order.CustomerId));
        return hub.Clients.Groups(groups).SendAsync(OrdersHub.OrderChanged, order, ct);
    }

    public Task CatalogChangedAsync(string kind, CancellationToken ct) =>
        hub.Clients.All.SendAsync(OrdersHub.CatalogChanged, kind, ct);
}
