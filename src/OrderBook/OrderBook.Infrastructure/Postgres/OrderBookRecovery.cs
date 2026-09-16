using Dapper;
using OrderBook.Domain.Modules.Matching;
using OrderBook.Domain.Modules.Orders;
using OrderBook.Domain.Shared;

namespace OrderBook.Infrastructure.Postgres;

public sealed class OrderBookRecovery(PostgresConnectionFactory factory)
{
    private static readonly string[] LedgerEffects = ["BUYER_BRL_DEBIT", "BUYER_VIBRANIUM_CREDIT", "SELLER_VIBRANIUM_DEBIT", "SELLER_BRL_CREDIT"];

    public async Task RebuildAsync(global::OrderBook.Domain.Modules.Matching.OrderBook book, CancellationToken cancellationToken)
    {
        await using var connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await ValidateAllOrdersAsync(connection, cancellationToken);
        await ValidateWalletReservationsAsync(connection, cancellationToken);
        await ValidateTradesAndLedgerAsync(connection, cancellationToken);

        var candidate = new global::OrderBook.Domain.Modules.Matching.OrderBook();
        const string sql = "SELECT o.id OrderId,o.user_id UserId,o.side Side,o.limit_price_brl_cents Price,o.original_quantity OriginalQuantity,o.remaining_quantity RemainingQuantity,o.status Status,o.accepted_sequence AcceptedSequence,r.id ReservationId,r.user_id ReservationUserId,r.side ReservationSide,r.asset ReservationAsset,r.remaining_amount ReservationRemaining FROM orders o LEFT JOIN reservations r ON r.order_id=o.id WHERE o.status IN ('OPEN','PARTIALLY_FILLED') ORDER BY o.accepted_sequence";
        var rows = await connection.QueryAsync<RecoveryRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        foreach (var row in rows)
        {
            ValidateActiveOrder(row);
            var order = Order.Rehydrate(OrderId.Create(row.OrderId), UserId.Create(row.UserId), Enum.Parse<Side>(row.Side), BrlCents.Create(row.Price), Quantity.Create(row.OriginalQuantity), row.RemainingQuantity, Enum.Parse<OrderStatus>(row.Status), AcceptedSequence.Create(row.AcceptedSequence!.Value));
            candidate.Add(order);
        }
        // Never expose a partially rebuilt snapshot.
        book.ReplaceWith(candidate);
    }

    private static async Task ValidateAllOrdersAsync(Npgsql.NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT id OrderId,user_id UserId,side Side,instrument Instrument,limit_price_brl_cents Price,original_quantity OriginalQuantity,remaining_quantity RemainingQuantity,status Status,accepted_sequence AcceptedSequence FROM orders ORDER BY accepted_sequence NULLS LAST,id";
        var rows = await connection.QueryAsync<OrderValidationRow>(new CommandDefinition(sql, cancellationToken: cancellationToken));
        var sequences = new HashSet<long>();
        foreach (var row in rows)
        {
            if (row.Instrument != "VIBRANIUM" || row.Side is not ("BUY" or "SELL") || row.Status is not ("OPEN" or "PARTIALLY_FILLED" or "FILLED" or "REJECTED") || row.Price <= 0 || row.OriginalQuantity <= 0 || row.RemainingQuantity < 0 || row.RemainingQuantity > row.OriginalQuantity)
                throw Divergence(row.OrderId, "invalid order fields");
            if ((row.Status == "OPEN" && row.RemainingQuantity != row.OriginalQuantity)
                || (row.Status == "PARTIALLY_FILLED" && (row.RemainingQuantity == 0 || row.RemainingQuantity == row.OriginalQuantity))
                || (row.Status == "FILLED" && row.RemainingQuantity != 0)
                || (row.Status == "REJECTED" && row.RemainingQuantity != row.OriginalQuantity))
                throw Divergence(row.OrderId, "invalid lifecycle and remaining quantity");
            if (row.Status == "REJECTED" && row.AcceptedSequence is not null || row.Status != "REJECTED" && (row.AcceptedSequence is null || row.AcceptedSequence <= 0))
                throw Divergence(row.OrderId, "invalid accepted sequence/status combination");
            if (row.AcceptedSequence is not null && !sequences.Add(row.AcceptedSequence.Value)) throw Divergence(row.OrderId, "duplicate accepted sequence");
        }
    }

    private static async Task ValidateWalletReservationsAsync(Npgsql.NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = "SELECT w.user_id UserId,w.brl_locked BrlLocked,w.vibranium_locked VibraniumLocked,COALESCE(SUM(CASE WHEN r.asset='BRL' AND r.status='ACTIVE' THEN r.remaining_amount ELSE 0 END),0) BrlReserved,COALESCE(SUM(CASE WHEN r.asset='VIBRANIUM' AND r.status='ACTIVE' THEN r.remaining_amount ELSE 0 END),0) VibraniumReserved FROM wallets w LEFT JOIN reservations r ON r.user_id=w.user_id GROUP BY w.user_id,w.brl_locked,w.vibranium_locked";
        foreach (var row in await connection.QueryAsync<WalletValidationRow>(new CommandDefinition(sql, cancellationToken: cancellationToken)))
        {
            if (row.BrlLocked < 0 || row.VibraniumLocked < 0 || row.BrlLocked != row.BrlReserved || row.VibraniumLocked != row.VibraniumReserved) throw Divergence(row.UserId, "wallet locked balance differs from active reservations");
        }
        const string activeReservations = "SELECT r.id ReservationId,r.order_id OrderId,r.user_id UserId,r.side Side,r.asset Asset,r.original_amount OriginalAmount,r.remaining_amount RemainingAmount,r.status Status,o.user_id OrderUserId,o.side OrderSide,o.limit_price_brl_cents Price,o.remaining_quantity OrderRemaining FROM reservations r JOIN orders o ON o.id=r.order_id";
        foreach (var row in await connection.QueryAsync<ReservationValidationRow>(new CommandDefinition(activeReservations, cancellationToken: cancellationToken)))
        {
            if (row.Status is not ("ACTIVE" or "RELEASED") || row.UserId != row.OrderUserId || row.Side != row.OrderSide || (row.Side == "BUY" && row.Asset != "BRL") || (row.Side == "SELL" && row.Asset != "VIBRANIUM") || row.OriginalAmount <= 0 || row.RemainingAmount < 0 || row.RemainingAmount > row.OriginalAmount) throw Divergence(row.ReservationId, "invalid reservation reference or amount");
            var expected = row.Side == "BUY" ? checked(row.OrderRemaining * row.Price) : row.OrderRemaining;
            if (row.RemainingAmount != expected) throw Divergence(row.ReservationId, "reservation does not match order remaining quantity");
        }
    }

    private static async Task ValidateTradesAndLedgerAsync(Npgsql.NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string trades = "SELECT id TradeId,taker_order_id TakerOrderId,maker_order_id MakerOrderId,buyer_user_id BuyerUserId,seller_user_id SellerUserId,quantity Quantity,price_brl_cents Price,accepted_sequence AcceptedSequence,fill_ordinal FillOrdinal FROM trades ORDER BY accepted_sequence,fill_ordinal,id";
        foreach (var trade in await connection.QueryAsync<TradeValidationRow>(new CommandDefinition(trades, cancellationToken: cancellationToken)))
        {
            if (trade.Quantity <= 0 || trade.Price <= 0 || trade.AcceptedSequence <= 0 || trade.FillOrdinal <= 0) throw Divergence(trade.TradeId, "invalid trade fields");
            var taker = await connection.QuerySingleOrDefaultAsync<OrderReference>(new CommandDefinition("SELECT user_id UserId,accepted_sequence AcceptedSequence,original_quantity OriginalQuantity FROM orders WHERE id=@id", new { id = trade.TakerOrderId }, cancellationToken: cancellationToken));
            var maker = await connection.QuerySingleOrDefaultAsync<OrderReference>(new CommandDefinition("SELECT user_id UserId,accepted_sequence AcceptedSequence,original_quantity OriginalQuantity FROM orders WHERE id=@id", new { id = trade.MakerOrderId }, cancellationToken: cancellationToken));
            if (taker is null || maker is null || taker.AcceptedSequence != trade.AcceptedSequence || trade.Quantity > taker.OriginalQuantity || trade.Quantity > maker.OriginalQuantity)
                throw Divergence(trade.TradeId, "trade references invalid orders or sequence");
            var ledger = (await connection.QueryAsync<LedgerValidationRow>(new CommandDefinition("SELECT effect_type EffectType,user_id UserId,asset Asset,direction Direction,amount Amount FROM ledger_entries WHERE trade_id=@tradeId ORDER BY effect_type", new { tradeId = trade.TradeId }, cancellationToken: cancellationToken))).ToArray();
            if (ledger.Length != 4 || ledger.Select(x => x.EffectType).Distinct(StringComparer.Ordinal).Count() != 4 || !LedgerEffects.OrderBy(x => x).SequenceEqual(ledger.Select(x => x.EffectType).OrderBy(x => x), StringComparer.Ordinal)) throw Divergence(trade.TradeId, "trade does not have exactly four ledger effects");
            var value = checked(trade.Quantity * trade.Price);
            var expected = new Dictionary<string, (Guid User, string Asset, string Direction, long Amount)>
            {
                ["BUYER_BRL_DEBIT"] = (trade.BuyerUserId, "BRL", "DEBIT", value),
                ["BUYER_VIBRANIUM_CREDIT"] = (trade.BuyerUserId, "VIBRANIUM", "CREDIT", trade.Quantity),
                ["SELLER_VIBRANIUM_DEBIT"] = (trade.SellerUserId, "VIBRANIUM", "DEBIT", trade.Quantity),
                ["SELLER_BRL_CREDIT"] = (trade.SellerUserId, "BRL", "CREDIT", value)
            };
            foreach (var entry in ledger)
            {
                var e = expected[entry.EffectType];
                if (entry.UserId != e.User || entry.Asset != e.Asset || entry.Direction != e.Direction || entry.Amount != e.Amount) throw Divergence(trade.TradeId, "ledger effect differs from trade");
            }
        }
        const string ordinals = "SELECT taker_order_id TakerOrderId,COUNT(*) Count,MIN(fill_ordinal) FirstOrdinal,MAX(fill_ordinal) LastOrdinal FROM trades GROUP BY taker_order_id";
        foreach (var row in await connection.QueryAsync<OrdinalValidationRow>(new CommandDefinition(ordinals, cancellationToken: cancellationToken)))
            if (row.FirstOrdinal != 1 || row.LastOrdinal != row.Count) throw Divergence(row.TakerOrderId, "trade ordinals are not consecutive");
        const string conservation = "SELECT asset,COALESCE(SUM(CASE WHEN direction='DEBIT' THEN amount ELSE 0 END),0) Debits,COALESCE(SUM(CASE WHEN direction='CREDIT' THEN amount ELSE 0 END),0) Credits FROM ledger_entries GROUP BY asset";
        foreach (var row in await connection.QueryAsync<ConservationRow>(new CommandDefinition(conservation, cancellationToken: cancellationToken)))
            if (row.Debits != row.Credits) throw Divergence(Guid.Empty, $"ledger is not conserved for {row.Asset}");
    }

    private static void ValidateActiveOrder(RecoveryRow row)
    {
        if (row.ReservationId is null || row.ReservationUserId != row.UserId || row.ReservationSide != row.Side || (row.Side == "BUY" && row.ReservationAsset != "BRL") || (row.Side == "SELL" && row.ReservationAsset != "VIBRANIUM") || row.AcceptedSequence is null || row.AcceptedSequence <= 0) throw Divergence(row.OrderId, "active order lacks a valid reservation or sequence");
        var expected = row.Side == "BUY" ? checked(row.RemainingQuantity * row.Price) : row.RemainingQuantity;
        if (row.ReservationRemaining != expected) throw Divergence(row.OrderId, "active reservation does not match remaining quantity");
    }

    private static InvalidOperationException Divergence(Guid id, string reason) => new($"Recovery divergence for {id}: {reason}.");
}
