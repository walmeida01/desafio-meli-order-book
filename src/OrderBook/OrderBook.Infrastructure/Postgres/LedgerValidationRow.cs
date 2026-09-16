namespace OrderBook.Infrastructure.Postgres;
internal sealed class LedgerValidationRow { public string EffectType { get; init; } = ""; public Guid UserId { get; init; } public string Asset { get; init; } = ""; public string Direction { get; init; } = ""; public long Amount { get; init; } }
