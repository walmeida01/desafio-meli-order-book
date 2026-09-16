namespace OrderBook.Domain.Shared;

public readonly record struct AcceptedSequence(long Value)
{
    public static AcceptedSequence Create(long value) => value <= 0 ? throw new DomainException("Accepted sequence must be positive.") : new(value);
}
