using OrderBook.Domain.Modules.Orders;
using OrderBook.Domain.Shared;

namespace OrderBook.Domain.Modules.Matching;

public sealed class MatchingEngine(OrderBook book)
{
    private static readonly Guid TradeNamespace = new("9a7fd2dc-6b37-4c58-9bf2-8a9c4abf4c9e");
    public MatchingResult Match(Order taker, int maximumFills = 1000)
    {
        var fills = new List<Fill>();
        var opposite = book.Snapshot(taker.Side == Side.BUY ? Side.SELL : Side.BUY).ToList();
        foreach (var maker in opposite)
        {
            if (!Crosses(taker, maker)) break;
            if (fills.Count == maximumFills) throw new DomainException("Maximum fills per command exceeded.");
            var quantity = Math.Min(taker.RemainingQuantity, maker.RemainingQuantity);
            var ordinal = fills.Count + 1;
            var tradeId = new TradeId(Uuid5.Create(TradeNamespace, $"{maker.Id}|{taker.Id}|{ordinal}"));
            fills.Add(new Fill(tradeId, taker.Id, maker.Id, taker.Side, quantity, maker.LimitPrice, ordinal));
            taker.ApplyFill(quantity); maker.ApplyFill(quantity);
            if (maker.RemainingQuantity == 0) book.Remove(maker);
            if (taker.RemainingQuantity == 0) break;
        }
        if (taker.RemainingQuantity > 0) book.Add(taker);
        return new MatchingResult(fills, taker.RemainingQuantity);
    }
    private static bool Crosses(Order taker, Order maker) => taker.Side == Side.BUY ? taker.LimitPrice.Value >= maker.LimitPrice.Value : taker.LimitPrice.Value <= maker.LimitPrice.Value;
}
