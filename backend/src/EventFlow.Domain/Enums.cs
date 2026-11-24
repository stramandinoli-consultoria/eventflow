namespace EventFlow.Domain;

// Os valores são gravados no banco e trafegam no JSON em snake_case
// ("awaiting_payment", "food"…), exatamente como o front-end já usa.

public enum UserRole { Customer, Partner, Admin }

public enum EventStatus { Upcoming, Live, Ended }

public enum StandType { Food, Drink, Beer, Dessert, Vip, Stage, Restroom, Entrance, Cashier }

public enum OrderStatus { AwaitingPayment, Preparing, Ready, Completed, Cancelled }

public enum DeliveryStatus { Pending, Delivered, Failed }

public static class StandTypeExtensions
{
    /// <summary>Barracas que vendem algo (as demais são áreas do mapa: palco, banheiro…).</summary>
    public static bool IsVendor(this StandType type) =>
        type is StandType.Food or StandType.Drink or StandType.Beer or StandType.Dessert;
}

public static class OrderStatusExtensions
{
    public static bool IsFinal(this OrderStatus status) =>
        status is OrderStatus.Completed or OrderStatus.Cancelled;
}
