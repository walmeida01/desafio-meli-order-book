using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Npgsql;
using OrderBook.FunctionalTests.Contracts;
using OrderBook.FunctionalTests.Fixtures;
using Xunit;

namespace OrderBook.FunctionalTests.Features.Steps;

public sealed class OrderApiSteps(ApiCollectionFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public HttpResponseMessage? LastResponse { get; private set; }
    public OrderRecord? LastOrder { get; private set; }

    public Task GivenCleanScenarioAsync() => fixture.ResetAsync();

    public async Task WhenSendAsync(OrderRequest request)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        LastResponse = await fixture.Client.SendAsync(message, TestContext.Current.CancellationToken);
        LastOrder = await TryReadOrderAsync(LastResponse);
    }

    public async Task<OrderRecord> ReadOrderAsync(Guid orderId)
    {
        using var response = await fixture.Client.GetAsync($"/api/v1/orders/{orderId}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<OrderRecord>(JsonOptions, TestContext.Current.CancellationToken))!;
    }

    public async Task<WalletRecord> ReadWalletAsync(Guid userId)
    {
        using var response = await fixture.Client.GetAsync($"/api/v1/wallets/{userId}", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<WalletRecord>(JsonOptions, TestContext.Current.CancellationToken))!;
    }

    public async Task<TradePageRecord> ReadTradesAsync()
    {
        using var response = await fixture.Client.GetAsync("/api/v1/trades?limit=100", TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<TradePageRecord>(JsonOptions, TestContext.Current.CancellationToken))!;
    }

    public async Task<DatabaseCounts> ReadCountsAsync()
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await connection.QuerySingleAsync<DatabaseCounts>(new CommandDefinition("SELECT (SELECT count(*) FROM orders) Orders, (SELECT count(*) FROM reservations) Reservations, (SELECT count(*) FROM trades) Trades, (SELECT count(*) FROM ledger_entries) LedgerEntries", cancellationToken: TestContext.Current.CancellationToken));
    }

    public async Task<long> ReadLedgerCountForTradeAsync(Guid tradeId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition("SELECT count(*) FROM ledger_entries WHERE trade_id=@tradeId", new { tradeId }, cancellationToken: TestContext.Current.CancellationToken));
    }

    public async Task<IReadOnlyList<LedgerEntryRecord>> ReadLedgerEntriesForTradeAsync(Guid tradeId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var entries = await connection.QueryAsync<LedgerEntryRecord>(new CommandDefinition(
            "SELECT effect_type EffectType,user_id UserId,asset Asset,direction Direction,amount Amount FROM ledger_entries WHERE trade_id=@tradeId ORDER BY effect_type",
            new { tradeId }, cancellationToken: TestContext.Current.CancellationToken));
        return entries.AsList();
    }

    public async Task<ReservationRecord?> ReadReservationAsync(Guid orderId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ReservationRecord>(new CommandDefinition(
            "SELECT order_id OrderId,user_id UserId,side Side,asset Asset,original_amount OriginalAmount,remaining_amount RemainingAmount,status Status FROM reservations WHERE order_id=@orderId",
            new { orderId }, cancellationToken: TestContext.Current.CancellationToken));
    }

    public async Task<long> ReadRejectedOrderCountAsync(Guid orderId)
    {
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return await connection.ExecuteScalarAsync<long>(new CommandDefinition("SELECT count(*) FROM orders WHERE id=@orderId AND status='REJECTED'", new { orderId }, cancellationToken: TestContext.Current.CancellationToken));
    }

    private static async Task<OrderRecord?> TryReadOrderAsync(HttpResponseMessage response)
    {
        if (response.StatusCode is not (HttpStatusCode.Created or HttpStatusCode.OK or HttpStatusCode.Conflict)) return null;
        var text = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return text.StartsWith('{') ? JsonSerializer.Deserialize<OrderRecord>(text, JsonOptions) : null;
    }
}
