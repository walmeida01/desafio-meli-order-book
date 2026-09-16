using Npgsql;
using Microsoft.Extensions.Logging;
using OrderBook.Application.Ports;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresWriterOwnership(PostgresConnectionFactory factory, Microsoft.Extensions.Logging.ILogger<PostgresWriterOwnership> logger, long lockKey = 7_310_001) : IWriterOwnership, IAsyncDisposable
{
    private NpgsqlConnection? connection;
    public bool IsHeld { get; private set; }
    public async Task<bool> CheckAsync(CancellationToken cancellationToken)
    {
        if (!IsHeld || connection is null || connection.State != System.Data.ConnectionState.Open) { IsHeld = false; return false; }
        try
        {
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(cancellationToken);
            return true;
        }
        catch (Exception exception) when (exception is NpgsqlException or InvalidOperationException or ObjectDisposedException)
        {
            IsHeld = false;
            logger.LogError(exception, "PostgreSQL writer ownership session was lost.");
            return false;
        }
    }
    public async Task<bool> AcquireAsync(CancellationToken cancellationToken)
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
    public async Task ReleaseAsync(CancellationToken cancellationToken)
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
    public async ValueTask DisposeAsync() => await ReleaseAsync(CancellationToken.None);
}
