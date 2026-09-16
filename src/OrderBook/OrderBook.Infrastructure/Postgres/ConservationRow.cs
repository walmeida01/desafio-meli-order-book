namespace OrderBook.Infrastructure.Postgres;

internal sealed class ConservationRow { public string Asset { get; init; } = ""; public long Debits { get; init; } public long Credits { get; init; } }
