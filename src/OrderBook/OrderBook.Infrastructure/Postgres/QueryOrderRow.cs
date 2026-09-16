namespace OrderBook.Infrastructure.Postgres;

internal sealed class QueryOrderRow { public Guid OrderId { get; init; } public Guid UserId { get; init; } public string Side { get; init; } = ""; public string Status { get; init; } = ""; public long OriginalQuantity { get; init; } public long ExecutedQuantity { get; init; } public long RemainingQuantity { get; init; } public long? AcceptedSequence { get; init; } }
