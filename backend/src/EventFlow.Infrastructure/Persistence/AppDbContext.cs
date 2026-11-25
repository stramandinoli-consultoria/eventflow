using EventFlow.Application.Abstractions;
using EventFlow.Domain;
using EventFlow.Domain.Auditing;
using EventFlow.Domain.Events;
using EventFlow.Domain.Orders;
using EventFlow.Domain.Users;
using EventFlow.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
{
    public const string OrderNumberSequence = "order_number_seq";

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Stand> Stands => Set<Stand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Partner> Partners => Set<Partner>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<Webhook> Webhooks => Set<Webhook>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums gravados como texto em snake_case ("awaiting_payment"), igual ao JSON da API.
        builder.Properties<UserRole>().HaveConversion<SnakeCaseEnumConverter<UserRole>>().HaveMaxLength(32);
        builder.Properties<EventStatus>().HaveConversion<SnakeCaseEnumConverter<EventStatus>>().HaveMaxLength(32);
        builder.Properties<StandType>().HaveConversion<SnakeCaseEnumConverter<StandType>>().HaveMaxLength(32);
        builder.Properties<OrderStatus>().HaveConversion<SnakeCaseEnumConverter<OrderStatus>>().HaveMaxLength(32);
        builder.Properties<DeliveryStatus>().HaveConversion<SnakeCaseEnumConverter<DeliveryStatus>>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.HasSequence<long>(OrderNumberSequence).StartsAt(1044);
        model.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
