namespace EventFlow.Domain.Webhooks;

/// <summary>
/// Uma entrega de aviso para um webhook. Falhas são reenviadas com espera
/// exponencial (30s, 60s, 120s… até 1h) até <see cref="MaxAttempts"/> tentativas.
/// </summary>
public sealed class WebhookDelivery
{
    public const int DefaultMaxAttempts = 6;
    private const int BaseBackoffSeconds = 30;
    private const int MaxBackoffSeconds = 3600;

    private readonly List<WebhookAttempt> _attempts = [];

    public Guid Id { get; private set; }
    public Guid WebhookId { get; private set; }
    public string OwnerId { get; private set; } = default!;
    public string? OrderId { get; private set; }
    public string Payload { get; private set; } = default!;
    public DeliveryStatus Status { get; private set; }
    public int Attempts { get; private set; }
    public int MaxAttempts { get; private set; }
    public DateTimeOffset NextAttemptAt { get; private set; }
    public string? LastError { get; private set; }
    public int? LastResponseStatus { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyList<WebhookAttempt> AttemptLog => _attempts;

    private WebhookDelivery() { }

    public static WebhookDelivery Queue(Webhook webhook, string? orderId, string payloadJson, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        WebhookId = webhook.Id,
        OwnerId = webhook.OwnerId,
        OrderId = orderId,
        Payload = payloadJson,
        Status = DeliveryStatus.Pending,
        MaxAttempts = DefaultMaxAttempts,
        NextAttemptAt = now,
        CreatedAt = now,
        UpdatedAt = now,
    };

    public static int BackoffSeconds(int attempt) =>
        (int)Math.Min(BaseBackoffSeconds * Math.Pow(2, Math.Max(0, attempt - 1)), MaxBackoffSeconds);

    /// <summary>Reenvio manual: garante pelo menos mais uma tentativa, mesmo após falha definitiva.</summary>
    public void AllowManualRetry(DateTimeOffset now)
    {
        MaxAttempts = Math.Max(MaxAttempts, Attempts + 1);
        Status = DeliveryStatus.Pending;
        NextAttemptAt = now;
    }

    public WebhookAttempt RecordAttempt(bool ok, int? responseStatus, string? error, int durationMs, bool webhookUsable, DateTimeOffset now)
    {
        Attempts++;
        var attempt = new WebhookAttempt(Attempts, ok, responseStatus, error, durationMs, now);
        _attempts.Add(attempt);

        var exhausted = Attempts >= MaxAttempts || !webhookUsable;
        Status = ok ? DeliveryStatus.Delivered : exhausted ? DeliveryStatus.Failed : DeliveryStatus.Pending;
        LastError = error;
        LastResponseStatus = responseStatus;
        NextAttemptAt = Status == DeliveryStatus.Pending ? now.AddSeconds(BackoffSeconds(Attempts)) : now;
        UpdatedAt = now;
        return attempt;
    }
}

public sealed class WebhookAttempt
{
    public Guid Id { get; private set; }
    public int Attempt { get; private set; }
    public bool Ok { get; private set; }
    public int? ResponseStatus { get; private set; }
    public string? Error { get; private set; }
    public int DurationMs { get; private set; }
    public DateTimeOffset At { get; private set; }

    private WebhookAttempt() { }

    internal WebhookAttempt(int attempt, bool ok, int? responseStatus, string? error, int durationMs, DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        Attempt = attempt;
        Ok = ok;
        ResponseStatus = responseStatus;
        Error = error;
        DurationMs = durationMs;
        At = at;
    }
}
