namespace OrderBook.FunctionalTests.Contracts;

public sealed record WalletRecord(Guid UserId, long BrlAvailable, long BrlLocked, long VibraniumAvailable, long VibraniumLocked);
