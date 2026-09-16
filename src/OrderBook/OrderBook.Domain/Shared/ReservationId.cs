namespace OrderBook.Domain.Shared;

public readonly record struct ReservationId(Guid Value)
{
    public static ReservationId Create(Guid value) => value == Guid.Empty ? throw new DomainException("ReservationId cannot be empty.") : new(value);
}
