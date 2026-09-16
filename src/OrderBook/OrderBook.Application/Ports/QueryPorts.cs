using OrderBook.Contracts.Queries;
using OrderBook.Contracts.Orders;

namespace OrderBook.Application.Ports;

public interface IOrderQueries
{
    Task<OrderResponse?> GetOrderAsync(Guid id, CancellationToken cancellationToken);
    Task<WalletResponse?> GetWalletAsync(Guid userId, CancellationToken cancellationToken);
    Task<TradePage> GetTradesAsync(string? cursor, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<BookLevel>> GetBookAsync(CancellationToken cancellationToken);
}
