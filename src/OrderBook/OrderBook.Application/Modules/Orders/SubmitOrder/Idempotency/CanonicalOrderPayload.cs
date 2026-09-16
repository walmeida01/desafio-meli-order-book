namespace OrderBook.Application.Modules.Orders.SubmitOrder.Idempotency;

public sealed record CanonicalOrderPayload(Guid UserId, string Side, long PriceBrlCents, long Quantity);
