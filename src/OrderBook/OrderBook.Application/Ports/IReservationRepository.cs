using OrderBook.Domain.Modules.Wallets;
using OrderBook.Domain.Shared;

namespace OrderBook.Application.Ports;

public interface IReservationRepository
{
    Task<Reservation?> LockByOrderAsync(OrderId orderId, CancellationToken cancellationToken);
    Task SaveAsync(Reservation reservation, CancellationToken cancellationToken);
}
