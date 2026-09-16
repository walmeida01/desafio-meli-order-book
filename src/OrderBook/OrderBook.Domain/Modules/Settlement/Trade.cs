using OrderBook.Domain.Shared;

namespace OrderBook.Domain.Modules.Settlement;

public sealed record Trade(TradeId Id, OrderId TakerOrderId, OrderId MakerOrderId, UserId BuyerUserId, UserId SellerUserId, long Quantity, BrlCents Price, AcceptedSequence AcceptedSequence, int FillOrdinal);
