using Dapper;
using OrderBook.Domain.Modules.Settlement;
using OrderBook.Domain.Shared;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresTradeDuplicateChecker(PostgresUnitOfWork unitOfWork)
{
    public async Task<DuplicateTradeDisposition> CheckAsync(Trade expected, CancellationToken cancellationToken)
    {
        _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for duplicate trade checking.");
        var row = await unitOfWork.Connection!.QuerySingleOrDefaultAsync<TradeRow>(new CommandDefinition("SELECT id TradeId,taker_order_id TakerOrderId,maker_order_id MakerOrderId,buyer_user_id BuyerUserId,seller_user_id SellerUserId,quantity Quantity,price_brl_cents Price,accepted_sequence AcceptedSequence,fill_ordinal FillOrdinal FROM trades WHERE id=@id FOR UPDATE", new { id = expected.Id.Value }, unitOfWork.Transaction, cancellationToken: cancellationToken));
        if (row is null) return DuplicateTradeDisposition.New;
        var persisted = new Trade(TradeId.Create(row.TradeId), OrderId.Create(row.TakerOrderId), OrderId.Create(row.MakerOrderId), UserId.Create(row.BuyerUserId), UserId.Create(row.SellerUserId), row.Quantity, BrlCents.Create(row.Price), AcceptedSequence.Create(row.AcceptedSequence), row.FillOrdinal);
        var ledgerRows = await unitOfWork.Connection!.QueryAsync<LedgerRow>(new CommandDefinition("SELECT id Id,trade_id TradeId,effect_type EffectType,user_id UserId,asset Asset,direction Direction,amount Amount FROM ledger_entries WHERE trade_id=@id", new { id = expected.Id.Value }, unitOfWork.Transaction, cancellationToken: cancellationToken));
        var entries = ledgerRows.Select(x => new LedgerEntry(x.Id, TradeId.Create(x.TradeId), Enum.Parse<LedgerEffectType>(x.EffectType), UserId.Create(x.UserId), x.Asset, x.Direction, x.Amount)).ToArray();
        return SettlementValidator.CompareTrade(expected, persisted, entries);
    }
}
