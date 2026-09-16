using Npgsql;

namespace OrderBook.Infrastructure.Postgres;

public sealed class MigrationRunner(PostgresConnectionFactory factory, Microsoft.Extensions.Options.IOptions<PostgresConnectionSettings> settings)
{
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        await using var connection = factory.Create();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_migrations(version text PRIMARY KEY, applied_at timestamptz NOT NULL DEFAULT now());
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var migrationDirectory = Path.Combine(AppContext.BaseDirectory, "database", "migrations");
        foreach (var migrationPath in Directory.EnumerateFiles(migrationDirectory, "*.sql").OrderBy(path => path, StringComparer.Ordinal))
        {
            var version = Path.GetFileNameWithoutExtension(migrationPath);
            var applied = await new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM schema_migrations WHERE version=@version)", connection, transaction)
            { Parameters = { new NpgsqlParameter("version", version) } }.ExecuteScalarAsync(cancellationToken);
            if (applied is true) continue;
            await using var apply = new NpgsqlCommand(await File.ReadAllTextAsync(migrationPath, cancellationToken), connection, transaction);
            await apply.ExecuteNonQueryAsync(cancellationToken);
            await using var mark = new NpgsqlCommand("INSERT INTO schema_migrations(version) VALUES (@version)", connection, transaction);
            mark.Parameters.AddWithValue("version", version);
            await mark.ExecuteNonQueryAsync(cancellationToken);
        }
        if (settings.Value.ApplySeed)
        {
            foreach (var seedPath in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "database", "seed"), "*.sql").OrderBy(path => path, StringComparer.Ordinal))
            {
                await using var seed = new NpgsqlCommand(await File.ReadAllTextAsync(seedPath, cancellationToken), connection, transaction);
                await seed.ExecuteNonQueryAsync(cancellationToken);
            }
        }
        await transaction.CommitAsync(cancellationToken);
    }
}
