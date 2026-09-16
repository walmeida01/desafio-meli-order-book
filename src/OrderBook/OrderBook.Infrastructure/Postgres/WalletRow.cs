namespace OrderBook.Infrastructure.Postgres;

internal sealed class WalletRow { public long BrlAvailable { get; init; } public long BrlLocked { get; init; } public long VibraniumAvailable { get; init; } public long VibraniumLocked { get; init; } }
