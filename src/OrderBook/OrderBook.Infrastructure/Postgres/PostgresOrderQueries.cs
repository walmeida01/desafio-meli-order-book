using Dapper;
using OrderBook.Application.Ports;
using OrderBook.Contracts.Orders;
using OrderBook.Contracts.Queries;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresOrderQueries(PostgresConnectionFactory factory) : IOrderQueries
{
    public async Task<OrderResponse?> GetOrderAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = factory.Create(); await connection.OpenAsync(cancellationToken);
        const string sql = "SELECT id OrderId,user_id UserId,side Side,status Status,original_quantity OriginalQuantity,original_quantity-remaining_quantity ExecutedQuantity,remaining_quantity RemainingQuantity,accepted_sequence AcceptedSequence FROM orders WHERE id=@id";
        var order = await connection.QuerySingleOrDefaultAsync<QueryOrderRow>(new CommandDefinition(sql, new { id }, cancellationToken: cancellationToken));
        if (order is null) return null;
        var trades = await connection.QueryAsync<TradeResponse>(new CommandDefinition("SELECT id TradeId,quantity Quantity,price_brl_cents PriceBrlCents FROM trades WHERE taker_order_id=@id OR maker_order_id=@id ORDER BY accepted_sequence,fill_ordinal", new { id }, cancellationToken: cancellationToken));
        return new OrderResponse(order.OrderId, order.UserId, order.Side, order.Status, order.OriginalQuantity, order.ExecutedQuantity, order.RemainingQuantity, order.AcceptedSequence, trades.ToArray());
    }
    public async Task<WalletResponse?> GetWalletAsync(Guid userId, CancellationToken cancellationToken)
    {
        await using var connection = factory.Create(); await connection.OpenAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<WalletResponse>(new CommandDefinition("SELECT user_id UserId,brl_available BrlAvailable,brl_locked BrlLocked,vibranium_available VibraniumAvailable,vibranium_locked VibraniumLocked FROM wallets WHERE user_id=@userId", new { userId }, cancellationToken: cancellationToken));
    }
    public async Task<TradePage> GetTradesAsync(string? cursor, int limit, CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(limit));
        await using var connection = factory.Create(); await connection.OpenAsync(cancellationToken);
        Cursor? decoded = Decode(cursor);
        var rows = (await connection.QueryAsync<TradeHistoryResponse>(new CommandDefinition("SELECT id TradeId,taker_order_id TakerOrderId,maker_order_id MakerOrderId,quantity Quantity,price_brl_cents PriceBrlCents,accepted_sequence AcceptedSequence,fill_ordinal FillOrdinal FROM trades WHERE (@sequence IS NULL OR (accepted_sequence,fill_ordinal,id) > (@sequence,@ordinal,@tradeId)) ORDER BY accepted_sequence,fill_ordinal,id LIMIT @limit", new { sequence = decoded?.Sequence, ordinal = decoded?.Ordinal, tradeId = decoded?.TradeId, limit = limit + 1 }, cancellationToken: cancellationToken))).ToArray();
        var hasNext = rows.Length > limit;
        var items = rows.Take(limit).ToArray();
        return new TradePage(items, hasNext ? Encode(items[^1]) : null);
    }
    public async Task<IReadOnlyList<BookLevel>> GetBookAsync(CancellationToken cancellationToken)
    {
        await using var connection = factory.Create(); await connection.OpenAsync(cancellationToken);
        var rows = await connection.QueryAsync<BookLevel>(new CommandDefinition("SELECT id OrderId,user_id UserId,side Side,limit_price_brl_cents PriceBrlCents,remaining_quantity RemainingQuantity,accepted_sequence AcceptedSequence FROM orders WHERE status IN ('OPEN','PARTIALLY_FILLED') ORDER BY CASE WHEN side='BUY' THEN 0 ELSE 1 END, CASE WHEN side='BUY' THEN limit_price_brl_cents END DESC, CASE WHEN side='SELL' THEN limit_price_brl_cents END ASC,accepted_sequence", cancellationToken: cancellationToken));
        return rows.ToArray();
    }

    private static Cursor? Decode(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        try
        {
            var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value)).Split('|');
            if (parts.Length != 3 || !long.TryParse(parts[0], out var sequence) || !int.TryParse(parts[1], out var ordinal) || !Guid.TryParse(parts[2], out var tradeId) || sequence < 1 || ordinal < 1)
                throw new FormatException();
            return new Cursor(sequence, ordinal, tradeId);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException)
        {
            throw new ArgumentException("Invalid cursor.", nameof(value), exception);
        }
    }

    private static string Encode(TradeHistoryResponse trade) => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{trade.AcceptedSequence}|{trade.FillOrdinal}|{trade.TradeId:D}"));
}
