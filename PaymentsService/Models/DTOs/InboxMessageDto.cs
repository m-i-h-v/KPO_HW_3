namespace PaymentsService.Models.DTOs;

public sealed record InboxMessageDto(Guid OrderId, Guid UserId, decimal Price);