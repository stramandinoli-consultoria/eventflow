using EventFlow.Domain.Auditing;
using EventFlow.Domain.Events;
using EventFlow.Domain.Orders;
using EventFlow.Domain.Users;
using EventFlow.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EventFlow.Infrastructure.Persistence;

// Nomes de tabelas e colunas em snake_case (UseSnakeCaseNamingConvention).

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Email).HasMaxLength(320);
        b.HasIndex(x => x.Email).IsUnique();
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.PartnerId).HasMaxLength(64);
        b.HasOne<Partner>().WithMany().HasForeignKey(x => x.PartnerId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.TokenHash).HasMaxLength(64);
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> b)
    {
        b.ToTable("events");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Banner).HasMaxLength(2000);
        b.Property(x => x.StartsAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.Location).HasMaxLength(200);
        b.Property(x => x.Description).HasMaxLength(2000);
    }
}

internal sealed class StandConfiguration : IEntityTypeConfiguration<Stand>
{
    public void Configure(EntityTypeBuilder<Stand> b)
    {
        b.ToTable("stands");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Cover).HasMaxLength(2000);
        b.Property(x => x.Rating).HasPrecision(3, 1);
        b.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Price).HasPrecision(10, 2);
        b.Property(x => x.Image).HasMaxLength(2000);
        b.HasOne<Stand>().WithMany().HasForeignKey(x => x.StandId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PartnerConfiguration : IEntityTypeConfiguration<Partner>
{
    public void Configure(EntityTypeBuilder<Partner> b)
    {
        b.ToTable("partners");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(64);
        b.Property(x => x.CompanyName).HasMaxLength(200);
        b.Property(x => x.Cnpj).HasMaxLength(20);
        b.Property(x => x.ContactName).HasMaxLength(200);
        b.Property(x => x.Email).HasMaxLength(320);
        b.Property(x => x.Phone).HasMaxLength(40);
        b.Property(x => x.Address).HasMaxLength(300);
        b.Property(x => x.Description).HasMaxLength(2000);
        b.Property(x => x.Logo).HasMaxLength(2000);
        b.HasOne<Event>().WithMany().HasForeignKey(x => x.EventId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Stand>().WithMany().HasForeignKey(x => x.StandId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.ToTable("orders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasMaxLength(32);
        // Pedidos são histórico: guardam o nome do stand e não dependem do evento/stand continuarem existindo.
        b.Property(x => x.EventId).HasMaxLength(64);
        b.Property(x => x.StandId).HasMaxLength(64);
        b.Property(x => x.StandName).HasMaxLength(120);
        b.Property(x => x.CustomerId).HasMaxLength(64);
        b.Property(x => x.CustomerName).HasMaxLength(120);
        b.Property(x => x.CustomerPhone).HasMaxLength(40);
        b.Property(x => x.Total).HasPrecision(12, 2);
        b.Property(x => x.AccessKeyHash).HasMaxLength(64);
        b.Property<uint>("Version").IsRowVersion(); // xmin: evita que duas pessoas avancem o mesmo pedido ao mesmo tempo
        b.HasIndex(x => x.CreatedAt);
        b.HasIndex(x => x.StandId);
        b.HasIndex(x => x.CustomerId);

        b.OwnsMany(x => x.Items, i =>
        {
            i.ToTable("order_items");
            i.WithOwner().HasForeignKey("OrderId");
            i.HasKey(x => x.Id);
            // Ids são criados no domínio; sem isso o EF trataria item novo como existente (UPDATE em vez de INSERT).
            i.Property(x => x.Id).ValueGeneratedNever();
            i.Property(x => x.ProductId).HasMaxLength(64);
            i.Property(x => x.ProductName).HasMaxLength(120);
            i.Property(x => x.UnitPrice).HasPrecision(10, 2);
            i.Ignore(x => x.Subtotal);
        });
        b.Navigation(x => x.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        b.OwnsMany(x => x.Timeline, t =>
        {
            t.ToTable("order_timeline");
            t.WithOwner().HasForeignKey("OrderId");
            t.HasKey(x => x.Id);
            t.Property(x => x.Id).ValueGeneratedNever();
            t.Property(x => x.Note).HasMaxLength(280);
            t.HasIndex("OrderId", nameof(OrderTimelineEntry.At));
        });
        b.Navigation(x => x.Timeline).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class WebhookConfiguration : IEntityTypeConfiguration<Webhook>
{
    public void Configure(EntityTypeBuilder<Webhook> b)
    {
        b.ToTable("webhooks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Url).HasMaxLength(2000);
        b.Property(x => x.Secret).HasMaxLength(128);
        b.Property(x => x.EventId).HasMaxLength(64);
        b.Property(x => x.StandName).HasMaxLength(120);
        b.Property(x => x.LastStatus).HasMaxLength(300);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> b)
    {
        b.ToTable("webhook_deliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Payload).HasColumnType("jsonb");
        b.Property(x => x.OrderId).HasMaxLength(32);
        b.Property(x => x.LastError).HasMaxLength(300);
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.HasOne<Webhook>().WithMany().HasForeignKey(x => x.WebhookId).OnDelete(DeleteBehavior.Cascade);

        b.OwnsMany(x => x.AttemptLog, a =>
        {
            a.ToTable("webhook_attempts");
            a.WithOwner().HasForeignKey("DeliveryId");
            a.HasKey(x => x.Id);
            a.Property(x => x.Id).ValueGeneratedNever();
            a.Property(x => x.Error).HasMaxLength(300);
        });
        b.Navigation(x => x.AttemptLog).HasField("_attempts").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("audit_logs");
        b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(64);
        b.Property(x => x.Entity).HasMaxLength(64);
        b.Property(x => x.EntityId).HasMaxLength(64);
        b.Property(x => x.Data).HasColumnType("jsonb");
        b.HasIndex(x => x.At);
    }
}
