namespace OrderBook.Domain.Shared;

public readonly record struct TradeId(Guid Value)
{
    public static TradeId Create(Guid value) => value == Guid.Empty ? throw new DomainException("TradeId cannot be empty.") : new(value);
}
