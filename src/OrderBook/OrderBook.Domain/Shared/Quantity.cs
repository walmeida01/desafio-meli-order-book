namespace OrderBook.Domain.Shared;

public readonly record struct Quantity
{
    public const long DefaultMaximum = 1_000_000_000;
    public long Value { get; }
    private Quantity(long value) => Value = value;
    public static Quantity Create(long value, long maximum = DefaultMaximum) => value is < 1 || value > DefaultMaximum || value > maximum ? throw new DomainException("Quantity is outside the configured range.") : new(value);
    public static Quantity FromPositive(long value) => Create(value);
}
