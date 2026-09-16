namespace OrderBook.FunctionalTests.Contracts;

public sealed record TradeHistoryRecord(Guid TradeId, Guid TakerOrderId, Guid MakerOrderId, long Quantity, long PriceBrlCents, long AcceptedSequence, int FillOrdinal);
