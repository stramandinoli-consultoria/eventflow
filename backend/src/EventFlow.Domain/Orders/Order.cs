using EventFlow.Domain.Common;
using EventFlow.Domain.Events;

namespace EventFlow.Domain.Orders;

/// <summary>
/// Pedido feito numa barraca. O total é sempre calculado aqui, com o preço do
/// cardápio, nunca com valores enviados pelo cliente.
/// </summary>
public sealed class Order
{
    public const int MaxQuantityPerItem = 50;

    /// <summary>Observações gravadas ao avançar pelo painel (mesmos textos do app original).</summary>
    private static readonly Dictionary<OrderStatus, string> AdvanceNotes = new()
    {
        [OrderStatus.Preparing] = "Pagamento confirmado no caixa",
        [OrderStatus.Ready] = "Notificação WhatsApp enviada",
    };

    /// <summary>Observações padrão quando o status é definido diretamente (ferramentas MCP).</summary>
    private static readonly Dictionary<OrderStatus, string> DefaultNotes = new()
    {
        [OrderStatus.Preparing] = "Pagamento confirmado no caixa",
        [OrderStatus.Ready] = "Notificação WhatsApp enviada",
        [OrderStatus.Completed] = "Pedido entregue ao cliente",
        [OrderStatus.Cancelled] = "Pedido cancelado",
    };

    private readonly List<OrderItem> _items = [];
    private readonly List<OrderTimelineEntry> _timeline = [];

    public string Id { get; private set; } = default!;
    public string EventId { get; private set; } = default!;
    public string StandId { get; private set; } = default!;
    public string StandName { get; private set; } = default!;
    public string? CustomerId { get; private set; }
    public string CustomerName { get; private set; } = default!;
    public string CustomerPhone { get; private set; } = default!;
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    /// <summary>Hash da chave que permite a quem fez o pedido sem login acompanhá-lo.</summary>
    public string AccessKeyHash { get; private set; } = default!;

    public IReadOnlyList<OrderItem> Items => _items;
    public IReadOnlyList<OrderTimelineEntry> Timeline => _timeline;

    private Order() { }

    public static Order Place(string id, Stand stand, IReadOnlyCollection<Product> menu,
        IReadOnlyCollection<(string ProductId, int Qty)> lines, string? customerId, string customerName,
        string customerPhone, string accessKeyHash, DateTimeOffset now)
    {
        if (!stand.Category.IsVendor()) throw new DomainException("Este elemento do mapa não vende produtos.");
        if (lines.Count == 0) throw new DomainException("Adicione produtos ao pedido.");

        var order = new Order
        {
            Id = id,
            EventId = stand.EventId,
            StandId = stand.Id,
            StandName = stand.Name,
            CustomerId = customerId,
            CustomerName = Guard.Required(customerName, "Nome", 120),
            CustomerPhone = Guard.Required(customerPhone, "WhatsApp", 40),
            Status = OrderStatus.AwaitingPayment,
            CreatedAt = now,
            UpdatedAt = now,
            AccessKeyHash = accessKeyHash,
        };

        foreach (var (productId, qty) in lines.GroupBy(l => l.ProductId).Select(g => (g.Key, g.Sum(l => l.Qty))))
        {
            var product = menu.FirstOrDefault(p => p.Id == productId && p.StandId == stand.Id)
                ?? throw new DomainException($"Produto {productId} não pertence a esta barraca.");
            if (!product.Available) throw new DomainException($"{product.Name} está indisponível no momento.");
            if (qty is < 1 or > MaxQuantityPerItem)
                throw new DomainException($"Quantidade de {product.Name} deve estar entre 1 e {MaxQuantityPerItem}.");
            order._items.Add(new OrderItem(order._items.Count, product.Id, product.Name, qty, product.Price));
        }

        order.Total = order._items.Sum(i => i.Subtotal);
        order._timeline.Add(new OrderTimelineEntry(OrderStatus.AwaitingPayment, "Dirija-se ao caixa para pagar", now));
        return order;
    }

    /// <summary>Próximo passo do fluxo: aguardando pagamento → em preparo → pronto → finalizado.</summary>
    public OrderStatus Advance(DateTimeOffset now)
    {
        var next = Status switch
        {
            OrderStatus.AwaitingPayment => OrderStatus.Preparing,
            OrderStatus.Preparing => OrderStatus.Ready,
            OrderStatus.Ready => OrderStatus.Completed,
            _ => throw new DomainException("Este pedido já foi encerrado."),
        };
        var previous = Status;
        Apply(next, AdvanceNotes.GetValueOrDefault(next), now);
        return previous;
    }

    public OrderStatus Cancel(DateTimeOffset now)
    {
        if (Status.IsFinal()) throw new DomainException("Este pedido já foi encerrado.");
        var previous = Status;
        Apply(OrderStatus.Cancelled, null, now);
        return previous;
    }

    /// <summary>Define o status diretamente. Retorna false se o pedido já estava nesse status.</summary>
    public bool ChangeStatus(OrderStatus status, string? note, DateTimeOffset now)
    {
        if (status == Status) return false;
        var finalNote = string.IsNullOrWhiteSpace(note) ? DefaultNotes.GetValueOrDefault(status) : note.Trim();
        Apply(status, finalNote, now);
        return true;
    }

    private void Apply(OrderStatus status, string? note, DateTimeOffset now)
    {
        Status = status;
        UpdatedAt = now;
        _timeline.Add(new OrderTimelineEntry(status, note, now));
    }
}
