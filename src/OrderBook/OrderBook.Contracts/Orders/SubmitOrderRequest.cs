namespace OrderBook.Contracts.Orders;

public sealed record SubmitOrderRequest(Guid UserId, string Side, long PriceBrlCents, long Quantity);
