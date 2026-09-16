using Testcontainers.PostgreSql;
using Xunit;

namespace OrderBook.IntegrationTests.Fixtures;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder().Build();
    public string ConnectionString => container.GetConnectionString();
    public async ValueTask InitializeAsync() => await container.StartAsync();
    public ValueTask DisposeAsync() => container.DisposeAsync();
}
