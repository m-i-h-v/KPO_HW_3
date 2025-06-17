using System.Text.Json;
using Npgsql;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PaymentsService.Database;
using PaymentsService.Models;
using PaymentsService.Models.DTOs;
using System;

namespace PaymentsService.UseCases.ConsumePayment;

internal sealed class ConsumePaymentService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<ConsumePaymentService> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topic;

    public ConsumePaymentService(IServiceScopeFactory serviceScopeFactory, ILogger<ConsumePaymentService> logger, 
        IConsumer<string, string> consumer, string topic)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _consumer = consumer;
        _topic = topic;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 10; ++i)
        {
            try
            {
                _logger.LogInformation($"Trying to subscribe to {_topic}");
                _consumer.Subscribe(_topic);
                _logger.LogInformation($"Successfully subscribed to {_topic}");
                break;
            }

            catch (Exception)
            {
                _logger.LogError("Error occured while trying to subscribe");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(TimeSpan.FromSeconds(5));

                if (consumeResult == null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }

                var order = JsonSerializer.Deserialize<InboxMessageDto>(consumeResult.Message.Value);

                if (order == null)
                {
                    _logger.LogWarning("Received invalid payment task: {Message}", consumeResult.Message.Value);

                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }

                var inboxMessage = new InboxMessage(order.OrderId, consumeResult.Message.Value);

                await ProcessAsync(inboxMessage, cancellationToken);

                _consumer.Commit(consumeResult);
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while consuming messages");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task ProcessAsync(InboxMessage inboxMessage, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PaymentContext>();

        try
        {
            await context.InboxMessages.AddAsync(inboxMessage, cancellationToken);

            await context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Received order: {Payload}", inboxMessage.Payload);
        }

        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            _logger.LogInformation("Order already exists: {Payload}", inboxMessage.Payload);
        }

        catch (Exception)
        {
            throw;
        }
    }
}