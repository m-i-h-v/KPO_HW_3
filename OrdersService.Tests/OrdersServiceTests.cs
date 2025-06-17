using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Xunit;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using OrdersService.Database;
using OrdersService.Models;
using OrdersService.Models.DTOs;
using OrdersService.UseCases.CreateOrder;
using Microsoft.Extensions.DependencyInjection;

namespace OrdersService.Tests;

public class OrdersServiceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer;
    private OrdersContext _context;

    public OrdersServiceTests()
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

        var options = new DbContextOptionsBuilder<OrdersContext>()
            .UseNpgsql(_postgresContainer.GetConnectionString())
            .Options;

        _context = new OrdersContext(options);
        await _context.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgresContainer.DisposeAsync();
    }

    [Theory]
    [InlineData(15, 15, false)]
    [InlineData(-15, -15, true)]
    public async Task CreateOrder_Success(decimal price, decimal expectedPrice, bool exception)
    {
        var service = new CreateOrderService(_context);
        var userId = Guid.NewGuid();

        if (exception)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>service.CreateOrderAsync(userId, price, CancellationToken.None));
        }

        else
        {
            var result = await service.CreateOrderAsync(userId, price, CancellationToken.None);

            Assert.NotNull(result);
            Assert.Equal(userId, result.UserId);
            Assert.Equal(price, result.Price);
            Assert.Equal(OrderStatusType.Unpaid, result.OrderStatus);

            var outboxMessage = await _context.OutboxMessages.FirstOrDefaultAsync(a => a.Id == result.Id);

            Assert.NotNull(outboxMessage);
            var order = JsonSerializer.Deserialize<OutboxMessageDto>(outboxMessage.Payload);
            Assert.NotNull(order);
            Assert.Equal(userId, order.UserId);
            Assert.Equal(price, order.Price);
            Assert.False(outboxMessage.IsSent);
        }
    }
}