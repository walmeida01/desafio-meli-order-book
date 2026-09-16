namespace OrderBook.Contracts.Queries;

public sealed record TradePage(IReadOnlyList<TradeHistoryResponse> Items, string? NextCursor);
