using FluentAssertions;
using Npgsql;
using OrderBook.IntegrationTests.Fixtures;
using Xunit;

namespace OrderBook.IntegrationTests;

public sealed class PostgresRealTests(PostgresFixture fixture) : IClassFixture<PostgresFixture>
{
    private CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrations_and_explicit_seed_are_repeatable()
    {
        await ApplySchemaAsync();
        await ApplySeedAsync();
        await ApplySeedAsync();

        await using var connection = await OpenAsync(CancellationToken);
        var walletCount = (long)(await new NpgsqlCommand("SELECT count(*) FROM wallets", connection).ExecuteScalarAsync(CancellationToken) ?? 0L);
        walletCount.Should().Be(2);
        var migrationCount = (long)(await new NpgsqlCommand("SELECT count(*) FROM schema_migrations", connection).ExecuteScalarAsync(CancellationToken) ?? 0L);
        migrationCount.Should().Be(1);
    }

    [Fact]
    public async Task Wallet_constraints_reject_negative_balance()
    {
        await ApplySchemaAsync();
        await using var connection = await OpenAsync(CancellationToken);
        var act = async () => await new NpgsqlCommand("INSERT INTO wallets(id,user_id,brl_available,brl_locked,vibranium_available,vibranium_locked) VALUES(gen_random_uuid(),gen_random_uuid(),-1,0,0,0)", connection).ExecuteNonQueryAsync(CancellationToken);
        await act.Should().ThrowAsync<PostgresException>().WithMessage("*wallets_brl_available_check*");
    }

    [Fact]
    public async Task Advisory_lock_has_single_owner()
    {
        await using var first = await OpenAsync(CancellationToken);
        await using var second = await OpenAsync(CancellationToken);
        var firstResult = await LockAsync(first, CancellationToken);
        var secondResult = await LockAsync(second, CancellationToken);
        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();
    }

    private async Task ApplySchemaAsync()
    {
        await using var connection = await OpenAsync(CancellationToken);
        await using (var schema = new NpgsqlCommand("CREATE TABLE IF NOT EXISTS schema_migrations(version text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now())", connection))
            await schema.ExecuteNonQueryAsync(CancellationToken);
        var sql = await File.ReadAllTextAsync(Path.Combine(ProjectRoot(), "database", "migrations", "001_initial.sql"), CancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(CancellationToken);
        await using (var migration = new NpgsqlCommand(sql, connection, transaction))
            await migration.ExecuteNonQueryAsync(CancellationToken);
        await using (var mark = new NpgsqlCommand("INSERT INTO schema_migrations(version) VALUES ('001_initial') ON CONFLICT DO NOTHING", connection, transaction))
            await mark.ExecuteNonQueryAsync(CancellationToken);
        await transaction.CommitAsync(CancellationToken);
    }

    private async Task ApplySeedAsync()
    {
        await using var connection = await OpenAsync(CancellationToken);
        var sql = await File.ReadAllTextAsync(Path.Combine(ProjectRoot(), "database", "seed", "001_wallets.sql"), CancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(CancellationToken);
    }

    private async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task<bool> LockAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("SELECT pg_try_advisory_lock(7310001)", connection);
        return (bool)(await command.ExecuteScalarAsync(cancellationToken) ?? false);
    }

    private static string ProjectRoot() => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
}
