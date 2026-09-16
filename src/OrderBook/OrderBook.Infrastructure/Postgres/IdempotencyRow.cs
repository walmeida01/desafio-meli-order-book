namespace OrderBook.Infrastructure.Postgres;

internal sealed class IdempotencyRow { public byte[] Hash { get; init; } = []; public Guid OrderId { get; init; } public int Status { get; init; } public string Body { get; init; } = ""; }
