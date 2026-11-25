namespace EventFlow.Domain.Orders;

public sealed class OrderTimelineEntry
{
    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset At { get; private set; }

    private OrderTimelineEntry() { }

    internal OrderTimelineEntry(OrderStatus status, string? note, DateTimeOffset at)
    {
        Id = Guid.NewGuid();
        Status = status;
        Note = note;
        At = at;
    }
}
