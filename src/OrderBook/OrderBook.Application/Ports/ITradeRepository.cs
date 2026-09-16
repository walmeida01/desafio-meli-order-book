using OrderBook.Domain.Modules.Settlement;

namespace OrderBook.Application.Ports;

public interface ITradeRepository
{
    Task SaveAsync(Trade trade, CancellationToken cancellationToken);
}
