namespace OrderBook.Application.Modules.Orders.SubmitOrder;

public sealed record SubmitOrderResult(Guid OrderId, Guid UserId, string Side, string Status, long OriginalQuantity, long ExecutedQuantity, long RemainingQuantity, long? AcceptedSequence, IReadOnlyList<SubmittedTrade> Trades, int HttpStatus, bool Replay);
