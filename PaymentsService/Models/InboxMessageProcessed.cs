namespace PaymentsService.Models;

public class InboxMessageProcessed
{
    public Guid Id { get; }
    public DateTimeOffset ProcessedAt { get; }

    public InboxMessageProcessed(Guid id)
    {
        Id = id;
        ProcessedAt = DateTimeOffset.UtcNow;
    }
}