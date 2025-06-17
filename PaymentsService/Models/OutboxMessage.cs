namespace PaymentsService.Models
{
    public class OutboxMessage
    {
        public Guid Id { get; }
        public string Payload { get; }
        public bool IsSent { get; }
        public DateTimeOffset CreatedAt { get; }

        public OutboxMessage(Guid id, string payload)
        {
            Id = id;
            Payload = payload;
            IsSent = false;
            CreatedAt = DateTimeOffset.UtcNow;
        }
    }
}