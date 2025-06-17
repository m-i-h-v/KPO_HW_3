namespace OrdersService.Models.DTOs;

public sealed record OrderStatusUpdateDto(Guid OrderId, OrderStatusType OrderStatus);