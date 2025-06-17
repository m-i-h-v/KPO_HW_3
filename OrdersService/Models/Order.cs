namespace OrdersService.Models;

public class Order
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public decimal Price { get; }
    public OrderStatusType OrderStatus { get; }
    public DateTimeOffset CreatedAt { get; }

    public Order(Guid userId, decimal price)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Price = price;
        OrderStatus = OrderStatusType.Unpaid;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}