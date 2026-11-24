using System.Globalization;
using EventFlow.Domain;
using EventFlow.Domain.Events;
using EventFlow.Domain.Orders;
using EventFlow.Domain.Users;
using EventFlow.Domain.Webhooks;

namespace EventFlow.Application.Dtos;

// Formato dos dados trafegados com o front. Os nomes dos campos são os mesmos
// que o front já usava no localStorage, para as telas não mudarem.

public sealed record UserDto(string Id, string Name, string Email, string? Phone, UserRole Role, string? PartnerId);

public sealed record AuthResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, UserDto User);

public sealed record EventDto(string Id, string Name, string Banner, string Date, string Location, string Description, EventStatus Status);

public sealed record StandDto(string Id, string EventId, string Name, StandType Category, string Description, string Cover,
    decimal Rating, int X, int Y, int Width, int Height);

public sealed record ProductDto(string Id, string StandId, string Name, string Description, decimal Price, int PrepMinutes,
    string Image, bool Available);

public sealed record PartnerDto(string Id, string CompanyName, string Cnpj, string ContactName, string Email, string Phone,
    string Address, StandType Category, string Description, string Logo, string EventId, string StandId);

public sealed record OrderItemDto(string ProductId, string ProductName, int Qty, decimal UnitPrice);

public sealed record TimelineEventDto(OrderStatus Status, DateTimeOffset At, string? Note);

public sealed record OrderDto(string Id, string EventId, string StandId, string StandName, string? CustomerId,
    string CustomerName, string CustomerPhone, IReadOnlyList<OrderItemDto> Items, decimal Total, OrderStatus Status,
    IReadOnlyList<TimelineEventDto> Timeline, DateTimeOffset CreatedAt);

public sealed record PlacedOrderDto(OrderDto Order, string AccessKey);

public sealed record WebhookDto(Guid Id, string Url, string? EventId, string? StandName, bool Active,
    string? LastStatus, DateTimeOffset? LastSentAt, DateTimeOffset CreatedAt);

public sealed record WebhookDispatchSummary(int Delivered, int QueuedForRetry, int Failed)
{
    public static readonly WebhookDispatchSummary Empty = new(0, 0, 0);
    public WebhookDispatchSummary Add(WebhookDispatchSummary other) =>
        new(Delivered + other.Delivered, QueuedForRetry + other.QueuedForRetry, Failed + other.Failed);
}

public sealed record DeliveryAttemptDto(Guid Id, int Attempt, DeliveryStatus Status, int? ResponseStatus, string? Error);

public sealed record AttemptLogDto(int Attempt, bool Ok, int? ResponseStatus, string? Error, int DurationMs, DateTimeOffset At);

public sealed record DeliveryDto(Guid Id, Guid WebhookId, string? OrderId, DeliveryStatus Status, int Attempts, int MaxAttempts,
    DateTimeOffset NextAttemptAt, string? LastError, int? LastResponseStatus, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    IReadOnlyList<AttemptLogDto> AttemptLog);

public sealed record Paged<T>(IReadOnlyList<T> Items, int Page, int Limit, int Total)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(Total / (double)Limit));
    public bool HasNext => Page < TotalPages;
}

public static class Mapping
{
    /// <summary>Formato de data do evento usado nas telas ("2026-06-14 17:00").</summary>
    public const string EventDateFormat = "yyyy-MM-dd HH:mm";

    public static bool TryParseEventDate(string? text, out DateTime date) =>
        DateTime.TryParseExact(text?.Trim(), EventDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static UserDto ToDto(this User u) => new(u.Id, u.Name, u.Email, u.Phone, u.Role, u.PartnerId);

    public static EventDto ToDto(this Event e) => new(e.Id, e.Name, e.Banner,
        e.StartsAt.ToString(EventDateFormat, CultureInfo.InvariantCulture), e.Location, e.Description, e.Status);

    public static StandDto ToDto(this Stand s) => new(s.Id, s.EventId, s.Name, s.Category, s.Description, s.Cover,
        s.Rating, s.X, s.Y, s.Width, s.Height);

    public static ProductDto ToDto(this Product p) => new(p.Id, p.StandId, p.Name, p.Description, p.Price, p.PrepMinutes,
        p.Image, p.Available);

    public static PartnerDto ToDto(this Partner p, bool includePrivateData) => includePrivateData
        ? new(p.Id, p.CompanyName, p.Cnpj, p.ContactName, p.Email, p.Phone, p.Address, p.Category, p.Description, p.Logo,
            p.EventId, p.StandId ?? "")
        // Visitantes veem só o que aparece no mapa; dados cadastrais ficam ocultos.
        : new(p.Id, p.CompanyName, "", "", "", "", "", p.Category, p.Description, p.Logo, p.EventId, p.StandId ?? "");

    public static OrderDto ToDto(this Order o) => new(o.Id, o.EventId, o.StandId, o.StandName, o.CustomerId, o.CustomerName,
        o.CustomerPhone,
        o.Items.OrderBy(i => i.Position).Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Qty, i.UnitPrice)).ToList(),
        o.Total, o.Status,
        o.Timeline.OrderBy(t => t.At).Select(t => new TimelineEventDto(t.Status, t.At, t.Note)).ToList(),
        o.CreatedAt);

    public static WebhookDto ToDto(this Webhook w) => new(w.Id, w.Url, w.EventId, w.StandName, w.Active, w.LastStatus,
        w.LastSentAt, w.CreatedAt);

    public static DeliveryDto ToDto(this WebhookDelivery d) => new(d.Id, d.WebhookId, d.OrderId, d.Status, d.Attempts,
        d.MaxAttempts, d.NextAttemptAt, d.LastError, d.LastResponseStatus, d.CreatedAt, d.UpdatedAt,
        d.AttemptLog.OrderBy(a => a.Attempt)
            .Select(a => new AttemptLogDto(a.Attempt, a.Ok, a.ResponseStatus, a.Error, a.DurationMs, a.At)).ToList());
}
