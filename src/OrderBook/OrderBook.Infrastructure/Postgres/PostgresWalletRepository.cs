using Dapper;
using OrderBook.Application.Ports;
using OrderBook.Domain.Modules.Wallets;
using OrderBook.Domain.Shared;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresWalletRepository(PostgresUnitOfWork unitOfWork) : IWalletRepository
{
    public async Task<Wallet?> LockAsync(UserId userId, CancellationToken cancellationToken)
    {
        _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for wallet locking.");
        var row = await unitOfWork.Connection!.QuerySingleOrDefaultAsync<WalletRow>(new CommandDefinition("SELECT brl_available BrlAvailable,brl_locked BrlLocked,vibranium_available VibraniumAvailable,vibranium_locked VibraniumLocked FROM wallets WHERE user_id=@userId FOR UPDATE", new { userId = userId.Value }, unitOfWork.Transaction, cancellationToken: cancellationToken));
        return row is null ? null : Wallet.Rehydrate(userId, row.BrlAvailable, row.BrlLocked, row.VibraniumAvailable, row.VibraniumLocked);
    }
    public async Task SaveAsync(Wallet wallet, CancellationToken cancellationToken)
    {
        _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for wallet persistence.");
        await unitOfWork.Connection!.ExecuteAsync(new CommandDefinition("UPDATE wallets SET brl_available=@brlAvailable,brl_locked=@brlLocked,vibranium_available=@vibraniumAvailable,vibranium_locked=@vibraniumLocked,version=version+1,updated_at=now() WHERE user_id=@user", new { user = wallet.UserId.Value, brlAvailable = wallet.BrlAvailable, brlLocked = wallet.BrlLocked, vibraniumAvailable = wallet.VibraniumAvailable, vibraniumLocked = wallet.VibraniumLocked }, unitOfWork.Transaction, cancellationToken: cancellationToken));
    }
}
