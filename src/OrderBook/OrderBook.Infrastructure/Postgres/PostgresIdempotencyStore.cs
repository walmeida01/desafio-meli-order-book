using Dapper;
using OrderBook.Application.Ports;
using OrderBook.Domain.Shared;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresIdempotencyStore(PostgresUnitOfWork unitOfWork) : IIdempotencyStore
{
    public async Task<IdempotencyRecord?> GetAsync(UserId userId, IdempotencyKey key, CancellationToken cancellationToken)
    {
        _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for idempotency.");
        var row = await unitOfWork.Connection!.QuerySingleOrDefaultAsync<IdempotencyRow>(new CommandDefinition("SELECT canonical_payload_hash Hash,order_id OrderId,response_status Status,response_body::text Body FROM idempotency_records WHERE user_id=@userId AND idempotency_key=@key FOR UPDATE", new { userId = userId.Value, key = key.Value }, unitOfWork.Transaction, cancellationToken: cancellationToken));
        return row is null ? null : new IdempotencyRecord(userId, key, row.Hash, OrderId.Create(row.OrderId), row.Status, row.Body);
    }
    public async Task SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken)
    {
        _ = unitOfWork.Transaction ?? throw new InvalidOperationException("A transaction is required for idempotency.");
        await unitOfWork.Connection!.ExecuteAsync(new CommandDefinition("INSERT INTO idempotency_records(user_id,idempotency_key,canonical_payload_hash,order_id,response_status,response_body) VALUES(@user,@key,@hash,@order,@status,CAST(@body AS jsonb))", new { user = record.UserId.Value, key = record.Key.Value, hash = record.Hash, order = record.OrderId.Value, status = record.Status, body = record.Body }, unitOfWork.Transaction, cancellationToken: cancellationToken));
    }
}
