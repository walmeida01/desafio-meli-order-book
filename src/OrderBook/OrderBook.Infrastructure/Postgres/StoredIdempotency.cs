namespace OrderBook.Infrastructure.Postgres;

internal sealed class StoredIdempotency
{
    public int Status { get; init; }
    public string Body { get; init; } = "";
    public byte[] Hash { get; init; } = [];
}
