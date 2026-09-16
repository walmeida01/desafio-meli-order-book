using System.ComponentModel.DataAnnotations;

namespace OrderBook.Contracts.Orders;

public sealed record OrderResponse(
    Guid OrderId,
    Guid UserId,
    [property: RegularExpression("^(BUY|SELL)$")] string Side,
    [property: RegularExpression("^(OPEN|PARTIALLY_FILLED|FILLED|REJECTED)$")] string Status,
    [property: Range(1, 1_000_000_000)] long OriginalQuantity,
    [property: Range(0, 1_000_000_000)] long ExecutedQuantity,
    [property: Range(0, 1_000_000_000)] long RemainingQuantity,
    long? AcceptedSequence,
    IReadOnlyList<TradeResponse> Trades);
