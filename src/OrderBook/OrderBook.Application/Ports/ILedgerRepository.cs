using OrderBook.Domain.Modules.Settlement;

namespace OrderBook.Application.Ports;

public interface ILedgerRepository
{
    Task SaveAsync(LedgerEntry entry, CancellationToken cancellationToken);
}
