using System.Text.Json;
using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrdersService.Database;
using OrdersService.Models;
using OrdersService.Models.DTOs;

namespace OrdersService.UseCases.UpdateOrderStatus;

internal sealed class UpdateOrderStatusService : BackgroundService
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<UpdateOrderStatusService> _logger;
    private readonly IConsumer<string, string> _consumer;
    private readonly string _topic;

    public UpdateOrderStatusService(IServiceScopeFactory serviceScopeFactory, ILogger<UpdateOrderStatusService> logger,
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
                _logger.LogError("Error occured while trting to subscribe");
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

                var orderUpdate = JsonSerializer.Deserialize<OrderStatusUpdateDto>(consumeResult.Message.Value);

                if (orderUpdate == null)
                {
                    _logger.LogWarning("Received invalid order update message: {Message}", consumeResult.Message.Value);

                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                    continue;
                }

                await ProcessAsync(orderUpdate, cancellationToken);

                _consumer.Commit(consumeResult);
            }

            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while consuming messages");
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task ProcessAsync(OrderStatusUpdateDto orderUpdate, CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrdersContext>();

        await context.Orders
            .Where(a => a.Id == orderUpdate.OrderId)
            .ExecuteUpdateAsync(a => a.SetProperty(a => a.OrderStatus, orderUpdate.OrderStatus), cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Received order update: OrderId = {OrderId}, OrderStatus - {OrderStatus}", orderUpdate.OrderId, orderUpdate.OrderStatus);
    }
}