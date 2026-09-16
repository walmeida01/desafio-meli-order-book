namespace OrderBook.Domain.Shared;

public readonly record struct OrderId(Guid Value)
{
    public static OrderId Create(Guid value) => value == Guid.Empty ? throw new DomainException("OrderId cannot be empty.") : new(value);
}
