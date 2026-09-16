namespace OrderBook.Contracts.Orders;

public sealed record OrderResponse(Guid OrderId, Guid UserId, string Side, string Status, long OriginalQuantity, long ExecutedQuantity, long RemainingQuantity, long? AcceptedSequence, IReadOnlyList<TradeResponse> Trades);
