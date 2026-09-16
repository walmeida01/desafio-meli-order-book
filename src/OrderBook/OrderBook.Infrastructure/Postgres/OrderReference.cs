namespace OrderBook.Infrastructure.Postgres;

internal sealed class OrderReference { public Guid UserId { get; init; } public long AcceptedSequence { get; init; } public long OriginalQuantity { get; init; } }
