using EventFlow.Application.Abstractions;
using EventFlow.Domain;
using EventFlow.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Application.Common;

/// <summary>
/// Regras de quem pode ver e mexer em quê:
/// admin vê tudo; parceiro só o próprio stand; cliente só os próprios pedidos;
/// quem fez o pedido sem login acompanha com a chave recebida na criação.
/// </summary>
public sealed class AccessScope(ICurrentUser user, IAppDbContext db, ICrypto crypto)
{
    private string? _standId;
    private bool _standLoaded;

    public ICurrentUser User => user;
    public bool IsAdmin => user.Role == UserRole.Admin;
    public bool IsPartner => user.Role == UserRole.Partner;

    /// <summary>Stand vinculado ao parceiro logado (null para os demais).</summary>
    public async Task<string?> PartnerStandIdAsync(CancellationToken ct)
    {
        if (_standLoaded) return _standId;
        _standLoaded = true;
        if (IsPartner && user.PartnerId is not null)
            _standId = await db.Partners.Where(p => p.Id == user.PartnerId).Select(p => p.StandId).FirstOrDefaultAsync(ct);
        return _standId;
    }

    public async Task<bool> CanManageStandAsync(string standId, CancellationToken ct) =>
        IsAdmin || (IsPartner && await PartnerStandIdAsync(ct) == standId);

    public Task<bool> CanManageOrderAsync(Order order, CancellationToken ct) => CanManageStandAsync(order.StandId, ct);

    public async Task<bool> CanViewOrderAsync(Order order, string? accessKey, CancellationToken ct) =>
        await CanManageOrderAsync(order, ct)
        || (user.Id is not null && order.CustomerId == user.Id)
        || (!string.IsNullOrEmpty(accessKey) && crypto.Hash(accessKey) == order.AccessKeyHash);

    /// <summary>Pedidos que o usuário logado pode listar.</summary>
    public async Task<IQueryable<Order>> VisibleOrdersAsync(CancellationToken ct)
    {
        var orders = db.Orders.AsQueryable();
        if (IsAdmin) return orders;
        if (IsPartner)
        {
            var standId = await PartnerStandIdAsync(ct);
            return standId is null ? orders.Where(_ => false) : orders.Where(o => o.StandId == standId);
        }
        return user.Id is null ? orders.Where(_ => false) : orders.Where(o => o.CustomerId == user.Id);
    }
}
