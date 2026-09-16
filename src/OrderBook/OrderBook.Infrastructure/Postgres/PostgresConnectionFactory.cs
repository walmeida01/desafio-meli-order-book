using Microsoft.Extensions.Options;
using Npgsql;

namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresConnectionFactory(IOptions<PostgresConnectionSettings> settings)
{
    public NpgsqlConnection Create() => new(settings.Value.ConnectionString);
}
