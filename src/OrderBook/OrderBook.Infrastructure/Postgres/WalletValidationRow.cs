namespace OrderBook.Infrastructure.Postgres;

internal sealed class WalletValidationRow { public Guid UserId { get; init; } public long BrlLocked { get; init; } public long VibraniumLocked { get; init; } public long BrlReserved { get; init; } public long VibraniumReserved { get; init; } }
