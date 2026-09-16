namespace OrderBook.Contracts.Queries;

public sealed record BookLevel(Guid OrderId, Guid UserId, string Side, long PriceBrlCents, long RemainingQuantity, long AcceptedSequence);
