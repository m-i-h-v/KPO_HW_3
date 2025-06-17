using System.Linq;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentsService.Database;
using PaymentsService.Models;
using PaymentsService.Models.DTOs;

namespace PaymentsService.UseCases.ProcessPayment;

public class PaymentProcessService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<PaymentProcessService> _logger;

    public PaymentProcessService(IServiceScopeFactory serviceScopeFactory, ILogger<PaymentProcessService> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await ProcessAsync(cancellationToken);

                if (!result)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    continue;
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing payments");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task<bool> ProcessAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        await using var context = scope.ServiceProvider.GetRequiredService<PaymentContext>();
        
        // ForUpdateSkipLocked deleted
        var inboxMessage = await context.InboxMessages
            .Where(a => a.IsProcessed == false)
            .OrderBy(a => a.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (inboxMessage == null)
        {
            return false;
        }

        var messageProcessed = await context.InboxMessagesProcessed.AnyAsync(a => a.Id == inboxMessage.Id);

        if (messageProcessed == true)
        {
            return true;
        }

        await ProcessMessageAsync(inboxMessage, context, cancellationToken);

        return true;
    }

    public async Task ProcessMessageAsync(InboxMessage message, PaymentContext context, CancellationToken cancellationToken)
    {
        var order = JsonSerializer.Deserialize<InboxMessageDto>(message.Payload);

        if (order == null)
        {
            return;
        }

        var account = await context.Accounts
            .Where(a => a.UserId == order.UserId)
            .FirstOrDefaultAsync(cancellationToken);

        if (account != null && account.Balance >= order.Price) 
        {
            decimal oldBalance;
            decimal newBalance;

            var tries = 0;

            while (tries++ < 10)
            {
                await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    oldBalance = account.Balance;
                    newBalance = oldBalance - order.Price;

                    if (newBalance < 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);

                        break;
                    }

                    var debitFromAccountResult = await context.Accounts
                        .Where(a => a.Id == account.Id)
                        .Where(a => a.Balance == oldBalance)
                        .ExecuteUpdateAsync(a => a.SetProperty(a => a.Balance, newBalance), cancellationToken);

                    var updatedInboxMessageResult = await context.InboxMessages
                        .Where(a => a.Id == message.Id)
                        .Where(a => a.IsProcessed == false)
                        .ExecuteUpdateAsync(a => a.SetProperty(a => a.IsProcessed, true), cancellationToken);

                    var inboxMessage = new InboxMessageProcessed(order.OrderId);
                    await context.InboxMessagesProcessed
                        .AddAsync(inboxMessage, cancellationToken);

                    var payload = JsonSerializer.Serialize<OutboxMessageDto>(new OutboxMessageDto(order.OrderId, OrderStatusType.Paid));

                    var outboxMessage = new OutboxMessage(order.OrderId, payload);
                    await context.OutboxMessages
                        .AddAsync(outboxMessage, cancellationToken);

                    if (debitFromAccountResult == 1 && updatedInboxMessageResult == 1)
                    {
                        await context.SaveChangesAsync(cancellationToken);

                        await transaction.CommitAsync(cancellationToken);

                        return;
                    }

                    await transaction.RollbackAsync(cancellationToken);

                    context.Entry(inboxMessage).State = EntityState.Detached;
                    context.Entry(outboxMessage).State = EntityState.Detached;

                    Task.Delay(TimeSpan.FromSeconds(1));

                    account = await context.Accounts.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == order.UserId);
                }

                catch (Exception)
                {
                    await transaction.RollbackAsync(cancellationToken);

                    return;
                }
            }
        }

        await using var trans = await context.Database.BeginTransactionAsync(cancellationToken);

        var updatedInboxMessageResultNew = await context.InboxMessages
                    .Where(a => a.Id == message.Id)
                    .Where(a => a.IsProcessed == false)
                    .ExecuteUpdateAsync(a => a.SetProperty(a => a.IsProcessed, true), cancellationToken);

        await context.InboxMessagesProcessed
            .AddAsync(new InboxMessageProcessed(order.OrderId), cancellationToken);

        var payloadNew = JsonSerializer.Serialize<OutboxMessageDto>(new OutboxMessageDto(order.OrderId, OrderStatusType.Declined));

        await context.OutboxMessages
            .AddAsync(new OutboxMessage(order.OrderId, payloadNew), cancellationToken);

        if (updatedInboxMessageResultNew == 1)
        {
            await context.SaveChangesAsync(cancellationToken);

            await trans.CommitAsync(cancellationToken); 
            
            return;
        }

        await trans.RollbackAsync(cancellationToken);
    }
}