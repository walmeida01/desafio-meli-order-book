namespace OrderBook.Application.Modules.Orders.SubmitOrder;

public sealed record SubmittedTrade(Guid TradeId, long Quantity, long PriceBrlCents);
