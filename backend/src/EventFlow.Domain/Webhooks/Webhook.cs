using EventFlow.Domain.Common;

namespace EventFlow.Domain.Webhooks;

/// <summary>Assinatura de avisos de mudança de status de pedidos (POST assinado com HMAC-SHA256).</summary>
public sealed class Webhook
{
    public Guid Id { get; private set; }
    public string OwnerId { get; private set; } = default!;
    public string Url { get; private set; } = default!;
    public string Secret { get; private set; } = default!;
    public string? EventId { get; private set; }
    public string? StandName { get; private set; }
    public bool Active { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? LastStatus { get; private set; }
    public DateTimeOffset? LastSentAt { get; private set; }

    private Webhook() { }

    public static Webhook Subscribe(string ownerId, string url, string secret, string? eventId, string? standName, DateTimeOffset now)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            throw new DomainException("Informe uma URL http(s) válida.");

        return new Webhook
        {
            Id = Guid.NewGuid(),
            OwnerId = ownerId,
            Url = uri.ToString(),
            Secret = secret,
            EventId = string.IsNullOrWhiteSpace(eventId) ? null : eventId.Trim(),
            StandName = string.IsNullOrWhiteSpace(standName) ? null : standName.Trim(),
            Active = true,
            CreatedAt = now,
        };
    }

    public bool Matches(string eventId, string standName) =>
        Active
        && (EventId is null || EventId == eventId)
        && (StandName is null || string.Equals(StandName, standName, StringComparison.OrdinalIgnoreCase));

    public void RecordSend(string status, DateTimeOffset at)
    {
        LastStatus = status;
        LastSentAt = at;
    }
}
