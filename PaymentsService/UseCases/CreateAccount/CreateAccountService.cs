using PaymentsService.Database;
using PaymentsService.Models;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System;

namespace PaymentsService.UseCases.CreateAccount;

public sealed class CreateAccountService : ICreateAccountService
{
	private readonly PaymentContext _context;

	public CreateAccountService(PaymentContext context)
	{
		_context = context;
	}

	public async Task<Account> CreateAccountAsync(Guid userId, CancellationToken cancellationToken)
	{
		var exists = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

		if (exists != null)
		{
			throw new InvalidOperationException("Account already exists");
		}

		var account = new Account(userId, 0);

		await _context.Accounts.AddAsync(account, cancellationToken);
		await _context.SaveChangesAsync(cancellationToken);

		return account;
	}

	public async Task<Account> GetAccountAsync(Guid userId, CancellationToken cancellationToken)
	{
		var account = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);

		if (account == null)
		{
			throw new InvalidOperationException("Account not found");
		}

		return account;
	}

	public async Task<InboxMessage> GetInboxMessageAsync(Guid orderId, CancellationToken cancellationToken)
	{
        var account = await _context.InboxMessages.Where(a => a.Id == orderId).FirstOrDefaultAsync(cancellationToken);

        if (account == null)
        {
            throw new InvalidOperationException("Account not found");
        }

        return account;
    }
}