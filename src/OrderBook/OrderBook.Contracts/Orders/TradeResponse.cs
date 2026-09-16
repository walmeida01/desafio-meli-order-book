namespace OrderBook.Contracts.Orders;

public sealed record TradeResponse(Guid TradeId, long Quantity, long PriceBrlCents);
