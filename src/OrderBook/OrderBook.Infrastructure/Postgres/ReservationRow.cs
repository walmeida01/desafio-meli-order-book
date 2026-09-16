namespace OrderBook.Infrastructure.Postgres;

internal sealed class ReservationRow { public Guid Id { get; init; } public Guid UserId { get; init; } public string Side { get; init; } = ""; public long OriginalAmount { get; init; } public long RemainingAmount { get; init; } }
