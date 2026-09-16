namespace OrderBook.Domain.Shared;

public readonly record struct BrlCents
{
    public long Value { get; }
    private BrlCents(long value) => Value = value;
    public static BrlCents Create(long value) => value <= 0 ? throw new DomainException("BRL cents must be positive.") : new(value);
    public static BrlCents Zero => new(0);
    public static BrlCents operator +(BrlCents a, BrlCents b) => FromChecked(a.Value, b.Value, true);
    public static BrlCents operator -(BrlCents a, BrlCents b) => a.Value < b.Value ? throw new DomainException("BRL cents cannot be negative.") : new(a.Value - b.Value);
    public static BrlCents Multiply(long quantity, BrlCents price) => quantity <= 0 ? throw new DomainException("Quantity must be positive.") : FromChecked(quantity, price.Value, false);
    private static BrlCents FromChecked(long a, long b, bool add) { try { return new(checked(add ? a + b : a * b)); } catch (OverflowException) { throw new DomainException("BRL arithmetic overflow."); } }
    public override string ToString() => Value.ToString();
}
