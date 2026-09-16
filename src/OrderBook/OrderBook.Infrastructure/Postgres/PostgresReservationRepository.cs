using Dapper;
using OrderBook.Application.Ports;
using OrderBook.Domain.Modules.Wallets;
using OrderBook.Domain.Shared;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresReservationRepository(PostgresUnitOfWork unitOfWork) : IReservationRepository
{
    public async Task<Reservation?> LockByOrderAsync(OrderId orderId, CancellationToken cancellationToken)
    {
        _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for reservation locking.");
        var row = await unitOfWork.Connection!.QuerySingleOrDefaultAsync<ReservationRow>(new CommandDefinition("SELECT id,user_id,side,original_amount,remaining_amount FROM reservations WHERE order_id=@orderId FOR UPDATE", new { orderId = orderId.Value }, unitOfWork.Transaction, cancellationToken: cancellationToken));
        return row is null ? null : Reservation.Rehydrate(ReservationId.Create(row.Id), orderId, UserId.Create(row.UserId), Enum.Parse<Side>(row.Side), row.OriginalAmount, row.RemainingAmount);
    }
    public async Task SaveAsync(Reservation reservation, CancellationToken cancellationToken)
    {
        _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for reservation persistence.");
        await unitOfWork.Connection!.ExecuteAsync(new CommandDefinition("UPDATE reservations SET remaining_amount=@remaining,status=CASE WHEN @remaining=0 THEN 'RELEASED' ELSE 'ACTIVE' END,updated_at=now() WHERE id=@id", new { id = reservation.Id.Value, remaining = reservation.RemainingAmount }, unitOfWork.Transaction, cancellationToken: cancellationToken));
    }
}
