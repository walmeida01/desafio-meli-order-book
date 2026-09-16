namespace OrderBook.FunctionalTests.Contracts;

public sealed record TradeRecord(Guid TradeId, long Quantity, long PriceBrlCents);
