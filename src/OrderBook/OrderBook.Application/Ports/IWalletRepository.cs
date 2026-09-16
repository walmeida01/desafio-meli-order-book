using OrderBook.Domain.Modules.Wallets;
using OrderBook.Domain.Shared;

namespace OrderBook.Application.Ports;

public interface IWalletRepository
{
    Task<Wallet?> LockAsync(UserId userId, CancellationToken cancellationToken);
    Task SaveAsync(Wallet wallet, CancellationToken cancellationToken);
}
