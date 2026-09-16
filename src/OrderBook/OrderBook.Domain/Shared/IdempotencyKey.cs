namespace OrderBook.Domain.Shared;

public readonly record struct IdempotencyKey
{
    public string Value { get; }
    private IdempotencyKey(string value) => Value = value;
    public static IdempotencyKey Create(string value)
    {
        if (string.IsNullOrEmpty(value) || System.Text.Encoding.UTF8.GetByteCount(value) > 128) throw new DomainException("Invalid idempotency key.");
        return new(value);
    }
}
