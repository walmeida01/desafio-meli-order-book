namespace OrderBook.Contracts.Queries;

public sealed record WalletResponse(Guid UserId, long BrlAvailable, long BrlLocked, long VibraniumAvailable, long VibraniumLocked);
