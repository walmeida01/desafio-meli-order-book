using OrderBook.Domain.Shared;

namespace OrderBook.Domain.Modules.Wallets;

public sealed class Reservation
{
    private Reservation(ReservationId id, OrderId orderId, UserId userId, Side side, long amount) { Id = id; OrderId = orderId; UserId = userId; Side = side; OriginalAmount = amount; RemainingAmount = amount; }
    public ReservationId Id { get; }
    public OrderId OrderId { get; }
    public UserId UserId { get; }
    public Side Side { get; }
    public long OriginalAmount { get; }
    public long RemainingAmount { get; private set; }
    public static Reservation Create(ReservationId id, OrderId orderId, UserId userId, Side side, long amount) => amount <= 0 ? throw new DomainException("Reservation amount must be positive.") : new(id, orderId, userId, side, amount);
    public static Reservation Rehydrate(ReservationId id, OrderId orderId, UserId userId, Side side, long originalAmount, long remainingAmount)
    {
        if (originalAmount <= 0 || remainingAmount < 0 || remainingAmount > originalAmount) throw new DomainException("Invalid persisted reservation.");
        return new Reservation(id, orderId, userId, side, originalAmount) { RemainingAmount = remainingAmount };
    }
    public void Capture(long amount) { if (amount <= 0 || amount > RemainingAmount) throw new DomainException("Capture exceeds reservation."); RemainingAmount -= amount; }
    public void Release(long amount) { if (amount < 0 || amount > RemainingAmount) throw new DomainException("Release exceeds reservation."); RemainingAmount -= amount; }
}
