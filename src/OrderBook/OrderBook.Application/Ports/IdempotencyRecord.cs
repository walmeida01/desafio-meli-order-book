using OrderBook.Domain.Shared;

namespace OrderBook.Application.Ports;

public sealed record IdempotencyRecord(UserId UserId, IdempotencyKey Key, byte[] Hash, OrderId OrderId, int Status, string Body);
