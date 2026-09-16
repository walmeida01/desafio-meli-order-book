using Dapper;
using OrderBook.Application.Ports;
using OrderBook.Domain.Modules.Orders;
using OrderBook.Domain.Shared;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresOrderRepository(PostgresUnitOfWork unitOfWork) : IOrderRepository
{
    public async Task<Order?> GetAsync(OrderId id, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        var row = await unitOfWork.Connection!.QuerySingleOrDefaultAsync<OrderRow>(new CommandDefinition("SELECT id OrderId,user_id UserId,side Side,limit_price_brl_cents Price,original_quantity OriginalQuantity,remaining_quantity RemainingQuantity,status Status,accepted_sequence AcceptedSequence FROM orders WHERE id=@id FOR UPDATE", new { id = id.Value }, unitOfWork.Transaction, cancellationToken: cancellationToken));
        return row is null ? null : Order.Rehydrate(OrderId.Create(row.OrderId), UserId.Create(row.UserId), Enum.Parse<Side>(row.Side), BrlCents.Create(row.Price), Quantity.Create(row.OriginalQuantity), row.RemainingQuantity, Enum.Parse<OrderStatus>(row.Status), row.AcceptedSequence is null ? null : AcceptedSequence.Create(row.AcceptedSequence.Value));
    }
    public async Task SaveAsync(Order order, CancellationToken cancellationToken)
    {
        EnsureTransaction();
        await unitOfWork.Connection!.ExecuteAsync(new CommandDefinition("UPDATE orders SET remaining_quantity=@remaining,status=@status,accepted_sequence=@sequence,updated_at=now() WHERE id=@id", new { id = order.Id.Value, remaining = order.RemainingQuantity, status = order.Status.ToString(), sequence = order.AcceptedSequence?.Value }, unitOfWork.Transaction, cancellationToken: cancellationToken));
    }
    private void EnsureTransaction() => _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for order persistence.");
}
