using EventFlow.Application.Abstractions;
using EventFlow.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace EventFlow.Infrastructure.Notifications;

/// <summary>
/// Implementação provisória: só registra no log a mensagem que seria enviada.
/// Para integrar de verdade (Z-API, Evolution API ou Meta Cloud API), crie outra
/// implementação de <see cref="IWhatsAppSender"/> e troque o registro na DI.
/// </summary>
internal sealed class LoggingWhatsAppSender(ILogger<LoggingWhatsAppSender> logger) : IWhatsAppSender
{
    public Task SendOrderReadyAsync(Order order, CancellationToken ct)
    {
        logger.LogInformation("[WhatsApp] Para {Phone}: Seu pedido #{OrderId} está pronto para retirada no stand {Stand}.",
            MaskPhone(order.CustomerPhone), order.Id, order.StandName);
        return Task.CompletedTask;
    }

    // Não grava o telefone completo no log.
    private static string MaskPhone(string phone) => phone.Length <= 4 ? "****" : new string('*', phone.Length - 4) + phone[^4..];
}
