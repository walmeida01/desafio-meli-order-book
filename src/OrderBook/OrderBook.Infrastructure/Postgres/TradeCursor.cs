namespace OrderBook.Infrastructure.Postgres;

internal sealed record Cursor(long Sequence, int Ordinal, Guid TradeId);
