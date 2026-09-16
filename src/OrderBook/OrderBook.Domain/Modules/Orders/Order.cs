using OrderBook.Domain.Shared;

namespace OrderBook.Domain.Modules.Orders;

public sealed class Order
{
    private Order(OrderId id, UserId userId, Side side, BrlCents price, Quantity quantity)
    { Id = id; UserId = userId; Side = side; LimitPrice = price; OriginalQuantity = quantity; RemainingQuantity = quantity.Value; Status = OrderStatus.QUEUED; }
    public OrderId Id { get; }
    public UserId UserId { get; }
    public Side Side { get; }
    public BrlCents LimitPrice { get; }
    public Quantity OriginalQuantity { get; }
    public long RemainingQuantity { get; private set; }
    public long ExecutedQuantity => OriginalQuantity.Value - RemainingQuantity;
    public OrderStatus Status { get; private set; }
    public AcceptedSequence? AcceptedSequence { get; private set; }

    public static Order Create(OrderId id, UserId userId, Side side, BrlCents price, Quantity quantity) => new(id, userId, side, price, quantity);
    public static Order Rehydrate(OrderId id, UserId userId, Side side, BrlCents price, Quantity original, long remaining, OrderStatus status, AcceptedSequence? sequence)
    {
        if (remaining < 0 || remaining > original.Value || status == OrderStatus.QUEUED) throw new DomainException("Invalid persisted order.");
        var order = new Order(id, userId, side, price, original) { RemainingQuantity = remaining, Status = status, AcceptedSequence = sequence };
        if (status != OrderStatus.REJECTED && sequence is null || status == OrderStatus.REJECTED && sequence is not null) throw new DomainException("Invalid persisted sequence.");
        return order;
    }
    public void Accept(AcceptedSequence sequence) { if (Status != OrderStatus.QUEUED) throw new DomainException("Only queued orders can be accepted."); AcceptedSequence = sequence; Status = OrderStatus.OPEN; }
    public void Reject() { if (Status != OrderStatus.QUEUED) throw new DomainException("Only queued orders can be rejected."); Status = OrderStatus.REJECTED; }
    public void ApplyFill(long quantity)
    {
        if (quantity <= 0 || quantity > RemainingQuantity) throw new DomainException("Fill exceeds remaining quantity.");
        if (Status is not (OrderStatus.OPEN or OrderStatus.PARTIALLY_FILLED)) throw new DomainException("Order cannot be filled in its current state.");
        RemainingQuantity -= quantity; Status = RemainingQuantity == 0 ? OrderStatus.FILLED : OrderStatus.PARTIALLY_FILLED;
    }
}
