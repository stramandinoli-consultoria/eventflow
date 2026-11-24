using EventFlow.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EventFlow.Infrastructure.Webhooks;

/// <summary>A cada 30 segundos reenvia as entregas pendentes cujo horário de reenvio já chegou.</summary>
internal sealed class WebhookRetryWorker(IServiceScopeFactory scopes, ILogger<WebhookRetryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IWebhookDispatcher>();
                var processed = await dispatcher.ProcessDueAsync(50, ownerId: null, stoppingToken);
                if (processed.Count > 0) logger.LogInformation("Reenvio de webhooks: {Count} entrega(s) processada(s).", processed.Count);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha ao processar a fila de webhooks.");
            }
        }
    }
}
