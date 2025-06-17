using System.Text.Json;
using OrdersService.Database;
using Microsoft.EntityFrameworkCore;
using OrdersService.Models;
using OrdersService.Models.DTOs;
using System.Threading.Tasks;
using System;

namespace OrdersService.UseCases.CreateOrder;

public class CreateOrderService : ICreateOrderService
{
    private readonly OrdersContext _context;

    public CreateOrderService(OrdersContext context)
    {
        _context = context;
    }

    public async Task<Order> CreateOrderAsync(Guid userId, decimal price, CancellationToken cancellationToken)
    {
        if (price <= 0)
        {
            throw new InvalidOperationException("Negative order price");
        }

        using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        var order = new Models.Order(userId, price);

        await _context.Orders.AddAsync(order, cancellationToken);

        var payload = JsonSerializer.Serialize(new OutboxMessageDto(order.Id, order.UserId, order.Price));

        var outboxMessage = new Models.OutboxMessage(order.Id, payload);

        await _context.OutboxMessages.AddAsync(outboxMessage, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return order;
    }

    public async Task<Order> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(a => a.Id == orderId, cancellationToken);

        if (order == null)
        {
            throw new InvalidOperationException("Order not found");
        }

        return order;
    }

    public async Task<List<Order>> GetOrdersAsync(Guid userId, CancellationToken cancellationToken)
    {
        var orders = await _context.Orders.Where(a => a.UserId == userId).ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            throw new InvalidOperationException("No orders found");
        }

        return orders;
    }
}