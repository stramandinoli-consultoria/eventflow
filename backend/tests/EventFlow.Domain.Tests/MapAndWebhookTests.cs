using EventFlow.Domain;
using EventFlow.Domain.Common;
using EventFlow.Domain.Events;
using EventFlow.Domain.Webhooks;

namespace EventFlow.Domain.Tests;

public class MapAndWebhookTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 14, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Stand_mantem_coordenadas_logicas_dentro_dos_limites()
    {
        var stand = Stand.Create("s1", "ev1", "Taco", StandType.Food, "", "", 4.5m, -40, 20, 5, 99999);

        Assert.Equal(0, stand.X);
        Assert.Equal(20, stand.Y);
        Assert.Equal(Stand.MinSize, stand.Width);
        Assert.Equal(Stand.MaxSize, stand.Height);

        stand.MoveTo(440, 160);
        Assert.Equal((440, 160), (stand.X, stand.Y));
    }

    [Fact]
    public void Parceiro_precisa_ser_de_categoria_de_venda()
    {
        Assert.Throws<DomainException>(() => Partner.Create("pt", "Empresa", "1", "Ana", "a@b.com", "1", "Rua", StandType.Stage,
            null, "https://x/logo", "ev1", null));
    }

    [Theory]
    [InlineData(1, 30)]
    [InlineData(2, 60)]
    [InlineData(5, 480)]
    [InlineData(10, 3600)]
    public void Espera_do_reenvio_dobra_ate_uma_hora(int attempt, int seconds) =>
        Assert.Equal(seconds, WebhookDelivery.BackoffSeconds(attempt));

    [Fact]
    public void Entrega_falha_definitivamente_apos_o_limite_de_tentativas()
    {
        var hook = Webhook.Subscribe("u1", "https://exemplo.com/hook", "segredo", null, null, Now);
        var delivery = WebhookDelivery.Queue(hook, "ord-1", "{}", Now);

        for (var i = 1; i < WebhookDelivery.DefaultMaxAttempts; i++)
        {
            delivery.RecordAttempt(false, 500, "http_500", 10, webhookUsable: true, Now);
            Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        }
        delivery.RecordAttempt(false, 500, "http_500", 10, webhookUsable: true, Now);
        Assert.Equal(DeliveryStatus.Failed, delivery.Status);

        delivery.AllowManualRetry(Now);
        delivery.RecordAttempt(true, 200, null, 10, webhookUsable: true, Now);
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(WebhookDelivery.DefaultMaxAttempts + 1, delivery.AttemptLog.Count);
    }

    [Fact]
    public void Webhook_filtra_por_evento_e_stand_sem_diferenciar_maiusculas()
    {
        var hook = Webhook.Subscribe("u1", "https://exemplo.com/hook", "s", "ev1", "beer station", Now);

        Assert.True(hook.Matches("ev1", "Beer Station"));
        Assert.False(hook.Matches("ev2", "Beer Station"));
        Assert.False(hook.Matches("ev1", "Taco Loco"));
        Assert.Throws<DomainException>(() => Webhook.Subscribe("u1", "ftp://x", "s", null, null, Now));
    }
}
