namespace OrderBook.FunctionalTests.Contracts;

public sealed record OrderRequest(Guid UserId, string Side, long PriceBrlCents, long Quantity);
