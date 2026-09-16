namespace OrderBook.Application.Ports;

public interface IUnitOfWork : IAsyncDisposable
{
    Task BeginAsync(CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
    Task RollbackAsync(CancellationToken cancellationToken);
}
