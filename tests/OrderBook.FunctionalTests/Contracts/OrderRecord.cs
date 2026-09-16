namespace OrderBook.FunctionalTests.Contracts;

public sealed record OrderRecord(Guid OrderId, Guid UserId, string Side, string Status, long OriginalQuantity, long ExecutedQuantity, long RemainingQuantity, long? AcceptedSequence, IReadOnlyList<TradeRecord> Trades);
