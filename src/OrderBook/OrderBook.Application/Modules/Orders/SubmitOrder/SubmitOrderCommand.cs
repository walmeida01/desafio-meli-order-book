using OrderBook.Domain.Shared;

namespace OrderBook.Application.Modules.Orders.SubmitOrder;

public sealed record SubmitOrderCommand(Guid CorrelationId, UserId UserId, Side Side, BrlCents Price, Quantity Quantity, IdempotencyKey IdempotencyKey, byte[] CanonicalHash);
