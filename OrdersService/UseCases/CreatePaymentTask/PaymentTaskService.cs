using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using OrdersService.Database;

namespace OrdersService.UseCases.CreatePaymentTask;

internal sealed class PaymentTaskService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<PaymentTaskService> _logger;
    private readonly IProducer<string, string> _producer;
    private readonly string _topic;

    public PaymentTaskService(IServiceScopeFactory serviceScopeFactory, ILogger<PaymentTaskService> logger,
        IProducer<string, string> producer, string topic)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _producer = producer;
        _topic = topic;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var processedMessages = await ProcessMessagesAsync(cancellationToken);

                if (processedMessages == SendResult.AllSent)
                {
                    await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
                    continue;
                }
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while sending notifications");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }
    private async Task<SendResult> ProcessMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrdersContext>();

        var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

        var outboxMessages = await context.OutboxMessages
            .Where(a => !a.IsSent)
            .AsNoTracking()
            .OrderBy(a => a.CreatedAt)
            .Take(10 /* Batch Size */)
            //.ForUpdateSkipLocked() !!!!!!!!!!!!!!
            .ToListAsync(cancellationToken);

        if (!outboxMessages.Any())
        {
            return SendResult.AllSent;
        }

        var messagesIds = outboxMessages.Select(a => a.Id).ToList();

        DeliveryResult<string, string> deliveryResult;

        foreach (var outboxMessage in outboxMessages)
        {
            deliveryResult = await _producer.ProduceAsync(_topic,
                new Message<string, string> { Key = outboxMessage.Id.ToString(), Value = outboxMessage.Payload },
                cancellationToken);

            if (deliveryResult.Status != PersistenceStatus.Persisted)
            {
                await transaction.RollbackAsync(cancellationToken);

                return SendResult.AllSent;
            }
        }

        await context.OutboxMessages
            .Where(a => messagesIds.Contains(a.Id))
            .ExecuteUpdateAsync(a => a.SetProperty(a => a.IsSent, true), cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return SendResult.AllSent;
    }

    private enum SendResult
    {
        AllSent,
        HasMore
    }
}