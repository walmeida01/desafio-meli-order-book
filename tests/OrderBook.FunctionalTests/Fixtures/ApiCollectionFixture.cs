using System.Net;
using Dapper;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderBook.FunctionalTests.Fixtures;

public sealed class ApiCollectionFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder().Build();
    private ApiFactory? factory;
    private HttpClient? client;

    public string ConnectionString => container.GetConnectionString();
    public HttpClient Client => client ?? throw new InvalidOperationException("A API ainda não foi iniciada.");

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync();
        factory = new ApiFactory(ConnectionString);
        client = factory.CreateClient();
        await WaitUntilReadyAsync();
        await ResetAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopApplicationAsync();
        }
        finally
        {
            await container.DisposeAsync();
        }
    }

    public async Task ResetAsync()
    {
        await StopApplicationAsync();

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await connection.ExecuteAsync(new CommandDefinition("TRUNCATE TABLE ledger_entries, trades, reservations, idempotency_records, orders RESTART IDENTITY CASCADE; ALTER SEQUENCE accepted_order_sequence RESTART WITH 1; UPDATE wallets SET brl_available=100000000, brl_locked=0, vibranium_available=100000, vibranium_locked=0, version=0, updated_at=now();", cancellationToken: TestContext.Current.CancellationToken));

        factory = new ApiFactory(ConnectionString);
        client = factory.CreateClient();
        await WaitUntilReadyAsync();
    }

    private async Task StopApplicationAsync()
    {
        var currentClient = client;
        client = null;
        currentClient?.Dispose();

        var currentFactory = factory;
        factory = null;
        if (currentFactory is not null) await currentFactory.DisposeAsync();
    }

    private async Task WaitUntilReadyAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken, timeout.Token);
        while (true)
        {
            using var response = await Client.GetAsync("/api/v1/ready", cancellation.Token);
            if (response.StatusCode == HttpStatusCode.OK) return;
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellation.Token);
        }
    }

}
