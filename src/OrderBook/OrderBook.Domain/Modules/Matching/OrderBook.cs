using OrderBook.Domain.Modules.Orders;
using OrderBook.Domain.Shared;

namespace OrderBook.Domain.Modules.Matching;

public sealed class OrderBook
{
    private readonly List<Order> buys = [];
    private readonly List<Order> sells = [];
    public void Add(Order order) { if (order.Status is not (OrderStatus.OPEN or OrderStatus.PARTIALLY_FILLED)) throw new DomainException("Only open orders can enter the book."); SideOf(order.Side).Add(order); }
    public void Remove(Order order) => SideOf(order.Side).Remove(order);
    public IReadOnlyList<Order> Snapshot(Side side) => SideOf(side).OrderBy(o => o, Comparer<Order>.Create(Compare)).ToArray();
    public OrderBook Copy()
    {
        var copy = new OrderBook();
        foreach (var order in buys.Concat(sells)) copy.Add(Order.Rehydrate(order.Id, order.UserId, order.Side, order.LimitPrice, order.OriginalQuantity, order.RemainingQuantity, order.Status, order.AcceptedSequence));
        return copy;
    }
    public void ReplaceWith(OrderBook candidate)
    {
        buys.Clear(); sells.Clear();
        foreach (var order in candidate.buys) buys.Add(order);
        foreach (var order in candidate.sells) sells.Add(order);
    }
    internal List<Order> SideOf(Side side) => side == Side.BUY ? buys : sells;
    private static int Compare(Order a, Order b) => a.Side == Side.BUY ? b.LimitPrice.Value.CompareTo(a.LimitPrice.Value) != 0 ? b.LimitPrice.Value.CompareTo(a.LimitPrice.Value) : a.AcceptedSequence!.Value.Value.CompareTo(b.AcceptedSequence!.Value.Value) : a.LimitPrice.Value.CompareTo(b.LimitPrice.Value) != 0 ? a.LimitPrice.Value.CompareTo(b.LimitPrice.Value) : a.AcceptedSequence!.Value.Value.CompareTo(b.AcceptedSequence!.Value.Value);
}
