using EventFlow.Domain;
using EventFlow.Domain.Common;
using EventFlow.Domain.Events;
using EventFlow.Domain.Orders;

namespace EventFlow.Domain.Tests;

public class OrderTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 14, 20, 0, 0, TimeSpan.Zero);
    private static readonly Stand Burger = Stand.Create("s3", "ev1", "Burger Republic", StandType.Food, "", "", 4.9m, 0, 0, 120, 90);
    private static readonly Stand Stage = Stand.Create("s6", "ev1", "Palco", StandType.Stage, "", "", 0, 0, 0, 220, 100);
    private static readonly Product[] Menu =
    [
        Product.Create("p5", "s3", "Smash Burger Duplo", "", 38, 12, "https://x/img", true),
        Product.Create("p6", "s3", "Fritas Trufadas", "", 24, 7, "https://x/img", true),
        Product.Create("p9", "s3", "Esgotado", "", 10, 1, "https://x/img", false),
        Product.Create("p1", "s1", "Taco", "", 32, 8, "https://x/img", true),
    ];

    private static Order Place(params (string, int)[] lines) =>
        Order.Place("ord-1", Burger, Menu, lines, null, "Marina", "+5511999990001", "hash", Now);

    [Fact]
    public void Total_vem_do_cardapio_e_agrupa_itens_repetidos()
    {
        var order = Place(("p5", 1), ("p6", 1), ("p5", 1));

        Assert.Equal(38 * 2 + 24, order.Total);
        Assert.Equal(["p5", "p6"], order.Items.OrderBy(i => i.Position).Select(i => i.ProductId));
        Assert.Equal(OrderStatus.AwaitingPayment, order.Status);
        var first = Assert.Single(order.Timeline);
        Assert.Equal("Dirija-se ao caixa para pagar", first.Note);
    }

    [Fact]
    public void Fluxo_completo_registra_timeline_com_as_notas_do_app()
    {
        var order = Place(("p5", 1));

        order.Advance(Now.AddMinutes(1));
        order.Advance(Now.AddMinutes(2));
        order.Advance(Now.AddMinutes(3));

        Assert.Equal(OrderStatus.Completed, order.Status);
        Assert.Equal(
            [OrderStatus.AwaitingPayment, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Completed],
            order.Timeline.Select(t => t.Status));
        Assert.Equal("Pagamento confirmado no caixa", order.Timeline[1].Note);
        Assert.Equal("Notificação WhatsApp enviada", order.Timeline[2].Note);
        Assert.Throws<DomainException>(() => order.Advance(Now.AddMinutes(4)));
    }

    [Fact]
    public void Pedido_cancelado_nao_avanca_nem_cancela_de_novo()
    {
        var order = Place(("p5", 1));
        order.Cancel(Now);

        Assert.Equal(OrderStatus.Cancelled, order.Status);
        Assert.Throws<DomainException>(() => order.Advance(Now));
        Assert.Throws<DomainException>(() => order.Cancel(Now));
    }

    [Fact]
    public void ChangeStatus_usa_nota_padrao_e_ignora_mesmo_status()
    {
        var order = Place(("p5", 1));

        Assert.False(order.ChangeStatus(OrderStatus.AwaitingPayment, null, Now));
        Assert.True(order.ChangeStatus(OrderStatus.Completed, null, Now));
        Assert.Equal("Pedido entregue ao cliente", order.Timeline[^1].Note);
        Assert.True(order.ChangeStatus(OrderStatus.Cancelled, "Cliente desistiu", Now));
        Assert.Equal("Cliente desistiu", order.Timeline[^1].Note);
    }

    [Theory]
    [InlineData("p1", 1, "não pertence")]
    [InlineData("p9", 1, "indisponível")]
    [InlineData("p5", 0, "Quantidade")]
    [InlineData("p5", 51, "Quantidade")]
    public void Rejeita_itens_invalidos(string productId, int qty, string message)
    {
        var ex = Assert.Throws<DomainException>(() => Place((productId, qty)));
        Assert.Contains(message, ex.Message);
    }

    [Fact]
    public void Nao_aceita_pedido_vazio_nem_em_area_que_nao_vende()
    {
        Assert.Throws<DomainException>(() => Place());
        Assert.Throws<DomainException>(() =>
            Order.Place("ord-2", Stage, Menu, [("p5", 1)], null, "A", "1", "h", Now));
    }
}
