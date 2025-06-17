namespace OrdersService.Models.DTOs;

public sealed record OutboxMessageDto(Guid OrderId, Guid UserId, decimal Price);