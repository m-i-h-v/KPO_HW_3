using Microsoft.AspNetCore.Mvc;
using PaymentsService.UseCases.CreateAccount;
using PaymentsService.UseCases.AccountBalance;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace PaymentsService.Controllers;

[Route("api/v1/accounts")]
[ApiController]
public class AccountsController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public AccountsController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Create new user account by user id
    /// </summary>
    /// <param name="userId">Unique user id</param>
    /// <returns>Created account</returns>
    /// <response code="200">Returns created account</response>
    /// <response code="400">Account already exists</response>
    [HttpPost]
    public async Task<IActionResult> CreateAccount([FromQuery] Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var accountService = _serviceProvider.GetRequiredService<ICreateAccountService>();

            var account = await accountService.CreateAccountAsync(userId, cancellationToken);

            return Ok(account);
        }
        
        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Refill user account balance
    /// </summary>
    /// <param name="userId">Unique user id</param>
    /// <param name="price">Amount to refill</param>
    /// <returns>Account with updated balance</returns>
    /// <response code="200">Returns account with updated balance</response>
    /// <response code="400">Account not exists or invalid amount to refill</response>
    [HttpPost("{userId}/refill")]
    public async Task<IActionResult> RefillAccount(Guid userId, [FromQuery] decimal price, CancellationToken cancellationToken)
    {
        try
        {
            var accountBalanceService = _serviceProvider.GetRequiredService<IAccountBalanceService>();

            var account = await accountBalanceService.RefillAccountBalanceAsync(userId, price, cancellationToken);

            return Ok(account);
        }

        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get user account
    /// </summary>
    /// <param name="userId">Unique user id</param>
    /// <returns>User account</returns>
    /// <response code="200">Returns user account</response>
    /// <response code="400">Account not exists</response>
    [HttpGet("{userId}")]
    public async Task<IActionResult> GetAccount(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var accountService = _serviceProvider.GetRequiredService<ICreateAccountService>();

            var account = await accountService.GetAccountAsync(userId, cancellationToken);

            return Ok(account);
        }

        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}