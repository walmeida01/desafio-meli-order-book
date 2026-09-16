namespace OrderBook.Domain.Shared;

public readonly record struct UserId(Guid Value)
{
    public static UserId Create(Guid value) => value == Guid.Empty ? throw new DomainException("UserId cannot be empty.") : new(value);
    public override string ToString() => Value.ToString("D");
}
