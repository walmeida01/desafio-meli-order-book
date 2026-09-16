using System.ComponentModel.DataAnnotations;

namespace OrderBook.Contracts.Queries;

public sealed record BookLevel(Guid OrderId, Guid UserId, [property: RegularExpression("^(BUY|SELL)$")] string Side, [property: Range(1, long.MaxValue)] long PriceBrlCents, [property: Range(1, 1_000_000_000)] long RemainingQuantity, [property: Range(1, long.MaxValue)] long AcceptedSequence);
