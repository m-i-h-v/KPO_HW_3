namespace PaymentsService.Models;

public class InboxMessage
{
    public Guid Id { get; }
    public string Payload { get; }
    public bool IsProcessed { get; }
    public DateTimeOffset CreatedAt { get; }

    public InboxMessage(Guid id, string payload)
    {
        Id = id; 
        Payload = payload;
        IsProcessed = false;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}