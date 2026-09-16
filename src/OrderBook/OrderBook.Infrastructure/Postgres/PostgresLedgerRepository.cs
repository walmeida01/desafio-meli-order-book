using Dapper;
using OrderBook.Application.Ports;
using OrderBook.Domain.Modules.Settlement;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresLedgerRepository(PostgresUnitOfWork unitOfWork) : ILedgerRepository
{
    public async Task SaveAsync(LedgerEntry entry, CancellationToken cancellationToken)
    {
        _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for ledger persistence.");
        await unitOfWork.Connection!.ExecuteAsync(new CommandDefinition("INSERT INTO ledger_entries(id,trade_id,effect_type,user_id,asset,direction,amount) VALUES(@id,@trade,@effect,@user,@asset,@direction,@amount)", new { id = entry.Id, trade = entry.TradeId.Value, effect = entry.EffectType.ToString(), user = entry.UserId.Value, entry.Asset, entry.Direction, entry.Amount }, unitOfWork.Transaction, cancellationToken: cancellationToken));
    }
}
