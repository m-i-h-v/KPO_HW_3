using PaymentsService.Database;
using PaymentsService.Models;
using Microsoft.EntityFrameworkCore;

namespace PaymentsService.UseCases.AccountBalance;

public class AccountBalanceService : IAccountBalanceService
{
    private readonly PaymentContext _context;

    public AccountBalanceService(PaymentContext context)
    {
        _context = context;
    }

    public async Task<Account> RefillAccountBalanceAsync(Guid userId, decimal amount, CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException("Amount to refill is negative");
        }

        var tries = 0;

        while (++tries < 25)
        {
            await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

            var account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId);

            if (account == null)
            {
                throw new InvalidOperationException("Account not found");
            }

            var oldBalance = account.Balance;
            var newBalance = oldBalance + amount;

            var updatedRows = await _context.Accounts
                .Where(a => a.UserId == userId)
                .Where(a => a.Balance == oldBalance)
                .ExecuteUpdateAsync(a => a.SetProperty(a => a.Balance, newBalance), cancellationToken);

            if (updatedRows != 1)
            {
                await tx.RollbackAsync(cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(1));
                continue;
            }

            await tx.CommitAsync(cancellationToken);

            account = await _context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == userId);

            return account;
        }

        throw new InvalidOperationException("Account balance refill failed");
    }
}