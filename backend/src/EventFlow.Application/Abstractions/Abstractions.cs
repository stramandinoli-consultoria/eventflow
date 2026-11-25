using EventFlow.Application.Dtos;
using EventFlow.Domain;
using EventFlow.Domain.Auditing;
using EventFlow.Domain.Events;
using EventFlow.Domain.Orders;
using EventFlow.Domain.Users;
using EventFlow.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Event> Events { get; }
    DbSet<Stand> Stands { get; }
    DbSet<Product> Products { get; }
    DbSet<Partner> Partners { get; }
    DbSet<Order> Orders { get; }
    DbSet<Webhook> Webhooks { get; }
    DbSet<WebhookDelivery> WebhookDeliveries { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

/// <summary>Usuário da requisição atual (vem do JWT).</summary>
public interface ICurrentUser
{
    string? Id { get; }
    string? Email { get; }
    UserRole? Role { get; }
    string? PartnerId { get; }
    bool IsAuthenticated => Id is not null;
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string hash, string password);
}

public interface ITokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken(User user);
    TimeSpan RefreshTokenLifetime { get; }
}

public interface ICrypto
{
    /// <summary>Texto aleatório seguro (hex) com o número de bytes pedido.</summary>
    string RandomToken(int bytes = 32);
    /// <summary>SHA-256 em hex, usado para guardar tokens sem o valor original.</summary>
    string Hash(string value);
}

/// <summary>Gera o código do pedido (ord-1044, ord-1045…).</summary>
public interface IOrderIdGenerator
{
    Task<string> NextAsync(CancellationToken ct);
}

/// <summary>Avisos em tempo real para o front (implementado com SignalR).</summary>
public interface IRealtimeNotifier
{
    Task OrderChangedAsync(OrderDto order, CancellationToken ct);
    /// <summary>Catálogo mudou (eventos, stands, produtos ou parceiros): o front recarrega.</summary>
    Task CatalogChangedAsync(string kind, CancellationToken ct);
}

public interface IWebhookDispatcher
{
    /// <summary>Enfileira um aviso por webhook compatível e tenta entregar na hora.</summary>
    Task<WebhookDispatchSummary> DispatchAsync(Order order, OrderStatus previousStatus, string? note, CancellationToken ct);
    Task<DeliveryAttemptDto> AttemptAsync(WebhookDelivery delivery, CancellationToken ct);
    Task<IReadOnlyList<DeliveryAttemptDto>> ProcessDueAsync(int limit, string? ownerId, CancellationToken ct);
}

/// <summary>Envio de mensagens de WhatsApp ao cliente.</summary>
public interface IWhatsAppSender
{
    Task SendOrderReadyAsync(Order order, CancellationToken ct);
}
