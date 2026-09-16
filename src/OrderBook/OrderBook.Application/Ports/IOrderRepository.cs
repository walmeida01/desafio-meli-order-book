using OrderBook.Domain.Modules.Orders;
using OrderBook.Domain.Shared;

namespace OrderBook.Application.Ports;

public interface IOrderRepository
{
    Task<Order?> GetAsync(OrderId id, CancellationToken cancellationToken);
    Task SaveAsync(Order order, CancellationToken cancellationToken);
}
