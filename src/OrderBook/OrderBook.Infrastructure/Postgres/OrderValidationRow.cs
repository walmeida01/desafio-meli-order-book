namespace OrderBook.Infrastructure.Postgres;

internal sealed class OrderValidationRow { public Guid OrderId { get; init; } public Guid UserId { get; init; } public string Side { get; init; } = ""; public string Instrument { get; init; } = ""; public long Price { get; init; } public long OriginalQuantity { get; init; } public long RemainingQuantity { get; init; } public string Status { get; init; } = ""; public long? AcceptedSequence { get; init; } }
