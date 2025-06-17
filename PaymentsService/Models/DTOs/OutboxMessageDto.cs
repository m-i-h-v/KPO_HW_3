namespace PaymentsService.Models.DTOs;

public sealed record OutboxMessageDto(Guid OrderId, OrderStatusType OrderStatus);