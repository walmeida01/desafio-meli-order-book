namespace OrderBook.Infrastructure.Postgres;

public sealed class PostgresConnectionSettings
{
    public const string SectionName = "Postgres";
    public string ConnectionString { get; init; } = "Host=localhost;Port=5432;Database=orderbook;Username=orderbook;Password=orderbook";
    public bool ApplySeed { get; init; }
}
