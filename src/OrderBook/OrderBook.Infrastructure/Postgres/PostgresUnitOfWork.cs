using Npgsql;
using OrderBook.Application.Ports;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresUnitOfWork(PostgresConnectionFactory factory) : IUnitOfWork
{
    public NpgsqlConnection? Connection { get; private set; }
    public NpgsqlTransaction? Transaction { get; private set; }
    public async Task BeginAsync(CancellationToken cancellationToken)
    {
        Connection = factory.Create();
        await Connection.OpenAsync(cancellationToken);
        Transaction = await Connection.BeginTransactionAsync(cancellationToken);
        await using var isolation = new NpgsqlCommand("SET TRANSACTION ISOLATION LEVEL READ COMMITTED; SET LOCAL statement_timeout = 2000", Connection, Transaction);
        await isolation.ExecuteNonQueryAsync(cancellationToken);
    }
    public async Task CommitAsync(CancellationToken cancellationToken) { if (Transaction is null) throw new InvalidOperationException("Transaction has not started."); await Transaction.CommitAsync(cancellationToken); }
    public async Task RollbackAsync(CancellationToken cancellationToken) { if (Transaction is not null) await Transaction.RollbackAsync(cancellationToken); }
    public async ValueTask DisposeAsync() { if (Transaction is not null) await Transaction.DisposeAsync(); if (Connection is not null) await Connection.DisposeAsync(); }
}
