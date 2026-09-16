using System.ComponentModel.DataAnnotations;

namespace OrderBook.Contracts.Queries;

public sealed record WalletResponse(Guid UserId, [property: Range(0, long.MaxValue)] long BrlAvailable, [property: Range(0, long.MaxValue)] long BrlLocked, [property: Range(0, long.MaxValue)] long VibraniumAvailable, [property: Range(0, long.MaxValue)] long VibraniumLocked);
