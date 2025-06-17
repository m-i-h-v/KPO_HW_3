using Microsoft.AspNetCore.Mvc;
using OrdersService.UseCases.CreateOrder;

namespace OrdersService.Controllers;

[Route("api/v1/orders")]
[ApiController]
public class OrdersController : ControllerBase
{
    private readonly IServiceProvider _serviceProvider;

    public OrdersController(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Create order by user id
    /// </summary>
    /// <param name="userId">Unique user id</param>
    /// <param name="price">Order price</param>
    /// <returns>Created order</returns>
    /// <response code="200">Returns created order</response>
    /// <response code="400">User account not exists or invalid order price</response>
    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromQuery] Guid userId, [FromQuery] decimal price, CancellationToken cancellationToken)
    {
        try
        {
            var orderService = _serviceProvider.GetRequiredService<ICreateOrderService>();

            var order = await orderService.CreateOrderAsync(userId, price, cancellationToken);

            return Ok(order);
        }

        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Returns particular order
    /// </summary>
    /// <param name="userId">User id</param>
    /// <param name="orderId">Order id</param>
    /// <returns>Order</returns>
    /// <response code="200">Returns order</response>
    /// <response code="400">Order not exists</response>
    [HttpGet("order/{orderId}")]
    public async Task<IActionResult> GetOrder(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            var createOrderService = _serviceProvider.GetRequiredService<ICreateOrderService>();

            var order = await createOrderService.GetOrderAsync(orderId, cancellationToken);

            return Ok(order);
        }

        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Returns all orders made by user
    /// </summary>
    /// <param name="userId">User id</param>
    /// <returns>Orders</returns>
    /// <response code="200">Returns orders</response>
    /// <response code="400">User not exists or have no orders</response>
    [HttpGet("orders/{userId}")]
    public async Task<IActionResult> GetOrders(Guid userId, CancellationToken cancellationToken)
    {
        try
        {
            var createOrderService = _serviceProvider.GetRequiredService<ICreateOrderService>();

            var orders = await createOrderService.GetOrdersAsync(userId, cancellationToken);

            return Ok(orders);
        }

        catch (Exception ex)
        {
            return BadRequest(ex.Message);
        }
    }
}