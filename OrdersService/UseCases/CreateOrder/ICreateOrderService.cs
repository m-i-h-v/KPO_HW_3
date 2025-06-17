using System;
using System.Threading;
using System.Threading.Tasks;
using OrdersService.Models;

namespace OrdersService.UseCases.CreateOrder;

public interface ICreateOrderService
{
    Task<Order> CreateOrderAsync(Guid userId, decimal price, CancellationToken cancellationToken);

    Task<Order> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);

    Task<List<Order>> GetOrdersAsync(Guid userId, CancellationToken cancellationToken);
}