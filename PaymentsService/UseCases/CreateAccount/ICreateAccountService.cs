using PaymentsService.Models;

namespace PaymentsService.UseCases.CreateAccount;

public interface ICreateAccountService
{
	Task<Account> CreateAccountAsync(Guid userId, CancellationToken cancellationToken);

	Task<Account> GetAccountAsync(Guid userId, CancellationToken cancellationToken);

	Task<InboxMessage> GetInboxMessageAsync(Guid orderId, CancellationToken cancellationToken);
}