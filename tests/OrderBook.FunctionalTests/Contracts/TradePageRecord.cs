namespace OrderBook.FunctionalTests.Contracts;

public sealed record TradePageRecord(IReadOnlyList<TradeHistoryRecord> Items, string? NextCursor);
