using OrderBook.Domain.Shared;

namespace OrderBook.Application.Ports;

public interface IIdempotencyStore
{
    Task<IdempotencyRecord?> GetAsync(UserId userId, IdempotencyKey key, CancellationToken cancellationToken);
    Task SaveAsync(IdempotencyRecord record, CancellationToken cancellationToken);
}
