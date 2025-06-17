namespace PaymentsService.Models;

public class Account
{
    public Guid Id { get; }
    public Guid UserId { get; }
    public decimal Balance { get; }
    public DateTimeOffset CreatedAt { get; }

    public Account(Guid userId, decimal balance)
    {
        Id = Guid.NewGuid();
        UserId = userId;
        Balance = balance;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}