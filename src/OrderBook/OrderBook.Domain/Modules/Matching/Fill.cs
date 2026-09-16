using OrderBook.Domain.Shared;

namespace OrderBook.Domain.Modules.Matching;

public sealed record Fill(TradeId TradeId, OrderId TakerOrderId, OrderId MakerOrderId, Side TakerSide, long Quantity, BrlCents Price, int FillOrdinal);
