namespace OrderBook.Contracts.Queries;

public sealed record TradeHistoryResponse(Guid TradeId, Guid TakerOrderId, Guid MakerOrderId, long Quantity, long PriceBrlCents, long AcceptedSequence, int FillOrdinal);
