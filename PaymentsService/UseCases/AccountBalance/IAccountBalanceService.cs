using PaymentsService.Models;

namespace PaymentsService.UseCases.AccountBalance;

public interface IAccountBalanceService
{
	Task<Account> RefillAccountBalanceAsync(Guid userId, decimal amount, CancellationToken cancellationToken);
}