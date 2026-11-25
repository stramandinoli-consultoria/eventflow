using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EventFlow.Application.Abstractions;
using EventFlow.Application.Dtos;
using EventFlow.Domain;
using EventFlow.Domain.Orders;
using EventFlow.Domain.Webhooks;
using Microsoft.EntityFrameworkCore;

namespace EventFlow.Infrastructure.Webhooks;

/// <summary>
/// Envia os avisos de mudança de status. Mesmo formato do app original:
/// cabeçalho <c>X-EventFlow-Signature: t=&lt;unix&gt;,v1=&lt;hmac&gt;</c>, onde o HMAC-SHA256
/// é calculado sobre <c>"{t}.{corpo}"</c> com o segredo do webhook.
/// </summary>
public sealed class WebhookDispatcher(IAppDbContext db, IHttpClientFactory httpFactory, TimeProvider clock) : IWebhookDispatcher
{
    public const string HttpClientName = "webhooks";
    public const string SignatureHeader = "X-EventFlow-Signature";
    private static readonly JsonSerializerOptions SnakeCase = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<WebhookDispatchSummary> DispatchAsync(Order order, OrderStatus previousStatus, string? note, CancellationToken ct)
    {
        var hooks = (await db.Webhooks.Where(w => w.Active).ToListAsync(ct))
            .Where(w => w.Matches(order.EventId, order.StandName)).ToList();
        if (hooks.Count == 0) return WebhookDispatchSummary.Empty;

        var now = clock.GetUtcNow();
        var payload = JsonSerializer.Serialize(new
        {
            type = "order.status_changed",
            at = now,
            order = new
            {
                id = order.Id,
                event_id = order.EventId,
                stand_id = order.StandId,
                stand_name = order.StandName,
                customer_name = order.CustomerName,
                status = Code(order.Status),
                total = order.Total,
            },
            previous_status = Code(previousStatus),
            note,
        }, SnakeCase);

        var summary = WebhookDispatchSummary.Empty;
        foreach (var hook in hooks)
        {
            var delivery = WebhookDelivery.Queue(hook, order.Id, payload, now);
            db.WebhookDeliveries.Add(delivery);
            await db.SaveChangesAsync(ct);
            var result = await AttemptAsync(delivery, ct);
            summary = summary.Add(result.Status switch
            {
                DeliveryStatus.Delivered => new WebhookDispatchSummary(1, 0, 0),
                DeliveryStatus.Pending => new WebhookDispatchSummary(0, 1, 0),
                _ => new WebhookDispatchSummary(0, 0, 1),
            });
        }
        return summary;
    }

    public async Task<DeliveryAttemptDto> AttemptAsync(WebhookDelivery delivery, CancellationToken ct)
    {
        var hook = await db.Webhooks.FirstOrDefaultAsync(w => w.Id == delivery.WebhookId, ct);
        var attemptNumber = delivery.Attempts + 1;
        var watch = Stopwatch.StartNew();
        var ok = false;
        int? responseStatus = null;
        string? error = null;

        if (hook is null || !hook.Active)
        {
            error = "webhook inativo ou removido";
        }
        else
        {
            var timestamp = clock.GetUtcNow().ToUnixTimeSeconds();
            using var request = new HttpRequestMessage(HttpMethod.Post, hook.Url)
            {
                Content = new StringContent(delivery.Payload, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add(SignatureHeader, $"t={timestamp},v1={Sign(hook.Secret, timestamp, delivery.Payload)}");
            request.Headers.Add("X-EventFlow-Delivery", delivery.Id.ToString());
            request.Headers.Add("X-EventFlow-Attempt", attemptNumber.ToString());
            try
            {
                using var response = await httpFactory.CreateClient(HttpClientName).SendAsync(request, ct);
                responseStatus = (int)response.StatusCode;
                ok = response.IsSuccessStatusCode;
                if (!ok) error = $"http_{responseStatus}";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or BlockedAddressException)
            {
                error = Truncate(ex.InnerException?.Message ?? ex.Message, 300);
            }
        }

        var now = clock.GetUtcNow();
        delivery.RecordAttempt(ok, responseStatus, error, (int)watch.ElapsedMilliseconds, webhookUsable: hook is { Active: true }, now);
        hook?.RecordSend(ok ? "ok" : Truncate(error ?? "erro", 300), now);
        await db.SaveChangesAsync(ct);
        return new DeliveryAttemptDto(delivery.Id, attemptNumber, delivery.Status, responseStatus, error);
    }

    public async Task<IReadOnlyList<DeliveryAttemptDto>> ProcessDueAsync(int limit, string? ownerId, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var due = db.WebhookDeliveries.Where(d => d.Status == DeliveryStatus.Pending && d.NextAttemptAt <= now);
        if (ownerId is not null) due = due.Where(d => d.OwnerId == ownerId);
        var list = await due.OrderBy(d => d.NextAttemptAt).Take(limit).ToListAsync(ct);

        var results = new List<DeliveryAttemptDto>();
        foreach (var delivery in list) results.Add(await AttemptAsync(delivery, ct));
        return results;
    }

    /// <summary>HMAC-SHA256 em hex de "{timestamp}.{corpo}".</summary>
    public static string Sign(string secret, long timestamp, string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")));

    private static string Code(OrderStatus status) => JsonNamingPolicy.SnakeCaseLower.ConvertName(status.ToString());

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}
