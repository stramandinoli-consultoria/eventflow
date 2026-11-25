namespace EventFlow.Domain.Orders;

/// <summary>Item do pedido. Guarda nome e preço do momento da compra.</summary>
public sealed class OrderItem
{
    public Guid Id { get; private set; }
    public string ProductId { get; private set; } = default!;
    public string ProductName { get; private set; } = default!;
    public int Qty { get; private set; }
    public decimal UnitPrice { get; private set; }
    /// <summary>Ordem em que o item foi adicionado (o banco não garante ordem sem isso).</summary>
    public int Position { get; private set; }

    public decimal Subtotal => Qty * UnitPrice;

    private OrderItem() { }

    internal OrderItem(int position, string productId, string productName, int qty, decimal unitPrice)
    {
        Id = Guid.NewGuid();
        Position = position;
        ProductId = productId;
        ProductName = productName;
        Qty = qty;
        UnitPrice = unitPrice;
    }
}
