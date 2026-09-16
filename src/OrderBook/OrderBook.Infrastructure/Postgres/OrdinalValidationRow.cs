namespace OrderBook.Infrastructure.Postgres;

internal sealed class OrdinalValidationRow { public Guid TakerOrderId { get; init; } public int Count { get; init; } public int FirstOrdinal { get; init; } public int LastOrdinal { get; init; } }
