using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Xunit;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using PaymentsService.Database;
using PaymentsService.Models;
using PaymentsService.Models.DTOs;
using PaymentsService.UseCases.CreateAccount;
using PaymentsService.UseCases.AccountBalance;
using PaymentsService.UseCases.ProcessPayment;
using Microsoft.Extensions.DependencyInjection;

namespace PaymentsService.Tests;

public class PaymentsServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private PaymentContext _context;

    public PaymentsServiceTests()
    {
        _postgresContainer = new PostgreSqlBuilder()
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .WithImage("postgres:latest")
            .Build();
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        var options = new DbContextOptionsBuilder<PaymentContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _context = new PaymentContext(options);
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task CreateAccount_Success()
    {
        var service = new CreateAccountService(_context);
        var userId = Guid.NewGuid();

        var result = await service.CreateAccountAsync(userId, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(0, result.Balance);
    }

    [Fact]
    public async Task CreateAccount_AlreadyExists_Throws()
    {
        var userId = Guid.NewGuid();
        await _context.Accounts.AddAsync(new Account(userId, 0));
        await _context.SaveChangesAsync();

        var service = new CreateAccountService(_context);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAccountAsync(userId, CancellationToken.None));
    }

    [Theory]
    [InlineData(-15, 0, true)]
    [InlineData(6.4, 6.4, false)]
    [InlineData(15, 15, false)]
    public async Task RefillExistingAccount(decimal balance, decimal expectedBalance, bool expectException)
    {
        var userId = Guid.NewGuid();

        await _context.Accounts.AddAsync(new Account(userId, 0));
        await _context.SaveChangesAsync();

        var service = new AccountBalanceService(_context);

        if (expectException)
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => service.RefillAccountBalanceAsync(userId, balance, CancellationToken.None));
        }

        else
        {
            var result = await service.RefillAccountBalanceAsync(userId, balance, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(expectedBalance, result.Balance);
        }
    }

    [Fact]
    public async Task RefillNotExistingAccount()
    {
        var userId = Guid.NewGuid();

        var service = new AccountBalanceService(_context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RefillAccountBalanceAsync(userId, 5, CancellationToken.None));

        await _context.Accounts.AddAsync(new Account(userId, 0));
        await _context.SaveChangesAsync();

        var result = await service.RefillAccountBalanceAsync(userId, 5, CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal(5, result.Balance);
    }

    [Fact]
    public async Task RefillAccount_CompareAndSwap()
    {
        var userId = Guid.NewGuid();

        await _context.AddAsync(new Account(userId, 0));
        await _context.SaveChangesAsync();

        var tasks = new List<Task>();

        var refillAmount = 10;
        var tasksNum = 10;

        for (var i = 0; i < tasksNum; ++i)
        {
            tasks.Add(Task.Run(async () =>
            {
                var options = new DbContextOptionsBuilder<PaymentContext>()
                .UseNpgsql(_postgresContainer.GetConnectionString())
                .Options;

                using var context = new PaymentContext(options);
                var service = new AccountBalanceService(context);

                await service.RefillAccountBalanceAsync(userId, refillAmount, CancellationToken.None);
            }));
        }

        await Task.WhenAll(tasks);

        var account = await _context.Accounts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.UserId == userId);

        Assert.NotNull(account);
        Assert.Equal(userId, account.UserId);
        Assert.Equal(refillAmount * tasksNum, account.Balance);
    }

    [Fact]
    public async Task RefillAccount_And_ProcessPayment()
    {
        var userId = Guid.NewGuid();

        await _context.AddAsync(new Account(userId, 100));
        await _context.SaveChangesAsync();

        var tasks = new List<Task>();

        var refillAmount = 10;
        var withdrawAmount = 5;
        var tasksNum = 10;
        var withdrawNum = 7;

        for (var i = 0; i < tasksNum; ++i)
        {
            tasks.Add(Task.Run(async () =>
            {
                var options = new DbContextOptionsBuilder<PaymentContext>()
                .UseNpgsql(_postgresContainer.GetConnectionString())
                .Options;

                using var context = new PaymentContext(options);
                var service = new AccountBalanceService(context);

                await service.RefillAccountBalanceAsync(userId, refillAmount, CancellationToken.None);
            }));
        }

        var logger = Substitute.For<ILogger<PaymentProcessService>>();
        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        for (var i = 0; i < withdrawNum; ++i)
        {
            tasks.Add(Task.Run(async () =>
            {
                var options = new DbContextOptionsBuilder<PaymentContext>()
                .UseNpgsql(_postgresContainer.GetConnectionString())
                .Options;

                using var context = new PaymentContext(options);
                var service = new PaymentProcessService(scopeFactory, logger);

                var orderId = Guid.NewGuid();

                var payload = JsonSerializer.Serialize(new InboxMessageDto(orderId, userId, withdrawAmount));

                var inbox = new InboxMessage(orderId, payload);

                await context.InboxMessages.AddAsync(inbox);
                await context.SaveChangesAsync();

                await service.ProcessMessageAsync(inbox, context, CancellationToken.None);
            }));
        }

        await Task.WhenAll(tasks);

        var account = await _context.Accounts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.UserId == userId);

        Assert.NotNull(account);
        Assert.Equal(userId, account.UserId);
        Assert.Equal(100 + refillAmount * tasksNum - withdrawAmount * withdrawNum, account.Balance);
    }
}