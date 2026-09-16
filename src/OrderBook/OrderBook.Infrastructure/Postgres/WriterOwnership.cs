using Npgsql;
using Microsoft.Extensions.Logging;
using OrderBook.Application.Ports;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresWriterOwnership(PostgresConnectionFactory factory, Microsoft.Extensions.Logging.ILogger<PostgresWriterOwnership> logger, long lockKey = 7_310_001) : IWriterOwnership, IAsyncDisposable
{
    private readonly SemaphoreSlim connectionGate = new(1, 1);
    private NpgsqlConnection? connection;
    public bool IsHeld { get; private set; }
    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        await connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (!IsHeld || connection is null || connection.State != System.Data.ConnectionState.Open)
            {
                logger.LogError("PostgreSQL writer ownership check failed: held={OwnershipHeld}, connectionState={ConnectionState}.", IsHeld, connection?.State.ToString() ?? "null");
                IsHeld = false;
                return false;
            }

            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or ObjectDisposedException)
        {
            IsHeld = false;
            logger.LogError(exception, "PostgreSQL writer ownership session was lost: sqlState={SqlState}, connectionState={ConnectionState}.", exception is NpgsqlException postgresException ? postgresException.SqlState : null, connection?.State.ToString() ?? "null");
            return false;
        }
        finally { connectionGate.Release(); }
    }
    public async Task<bool> AcquireAsync(CancellationToken cancellationToken)
    {
        await connectionGate.WaitAsync(cancellationToken);
        try
        {
            if (IsHeld) return true;
            connection = factory.Create();
            await connection.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(@key)", connection);
            command.Parameters.AddWithValue("key", lockKey);
            IsHeld = (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
            if (!IsHeld) await connection.DisposeAsync();
            return IsHeld;
        }
        finally { connectionGate.Release(); }
    }
    public async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        await connectionGate.WaitAsync(CancellationToken.None);
        try
        {
            var ownedConnection = connection;
            connection = null;
            IsHeld = false;
            if (ownedConnection is null) return;
            try
            {
                if (ownedConnection.State == System.Data.ConnectionState.Open)
                {
                    await using var command = new NpgsqlCommand("SELECT pg_advisory_unlock(@key)", ownedConnection);
                    command.Parameters.AddWithValue("key", lockKey);
                    await command.ExecuteScalarAsync(cancellationToken);
                }
            }
            finally { await ownedConnection.DisposeAsync(); }
        }
        finally { connectionGate.Release(); }
    }
    public async ValueTask DisposeAsync() => await ReleaseAsync(CancellationToken.None);
}
