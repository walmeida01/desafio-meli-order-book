using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using Xunit;

namespace OrderBook.IntegrationTests;

public sealed class HostSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient client;
    public HostSmokeTests(WebApplicationFactory<Program> factory) => client = factory.CreateClient();

    [Fact]
    public async Task Liveness_is_available()
    {
        var response = await client.GetAsync("/api/v1/health", TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Order_mutation_is_unavailable_without_readiness()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/orders");
        request.Content = JsonContent.Create(new { userId = Guid.NewGuid(), side = "BUY", priceBrlCents = 100, quantity = 1 });
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(System.Net.HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Metrics_endpoint_is_not_versioned()
    {
        var response = await client.GetAsync("/metrics", TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
    }

    [Fact]
    public async Task Swagger_ui_uses_the_versioned_openapi_document()
    {
        var response = await client.GetAsync("/swagger/index.html", TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("Meli Order Book API");

        var configuration = await client.GetAsync("/swagger/index.js", TestContext.Current.CancellationToken);
        configuration.IsSuccessStatusCode.Should().BeTrue();
        (await configuration.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().Contain("/openapi/v1.json");
    }

    [Fact]
    public async Task OpenApi_document_describes_versioned_business_contract()
    {
        var response = await client.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        response.IsSuccessStatusCode.Should().BeTrue();

        using var document = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken));
        var root = document.RootElement;
        var createOrder = root.GetProperty("paths").GetProperty("/api/v1/orders").GetProperty("post");
        if (createOrder.TryGetProperty("parameters", out var createOrderParameters))
            createOrderParameters.EnumerateArray().Should().NotContain(parameter => parameter.GetProperty("name").GetString() == "Idempotency-Key");
        var requestSchema = root.GetProperty("components").GetProperty("schemas").GetProperty("SubmitOrderRequest");
        requestSchema.GetProperty("additionalProperties").GetBoolean().Should().BeFalse();
        requestSchema.GetProperty("properties").GetProperty("side").GetProperty("enum").EnumerateArray().Select(value => value.GetString())
            .Should().Equal("BUY", "SELL");
        requestSchema.GetProperty("properties").GetProperty("quantity").GetProperty("minimum").GetInt64().Should().Be(1);
        requestSchema.GetProperty("properties").GetProperty("quantity").GetProperty("maximum").GetInt64().Should().Be(1_000_000_000);
        root.GetProperty("components").GetProperty("schemas").GetProperty("OrderResponse").GetProperty("properties").GetProperty("status")
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString())
            .Should().Equal("OPEN", "PARTIALLY_FILLED", "FILLED", "REJECTED");
        root.GetProperty("paths").GetProperty("/api/v1/orders").GetProperty("post").GetProperty("summary").GetString()
            .Should().Be("Cria uma ordem de compra ou venda");
        root.GetProperty("paths").GetProperty("/api/v1/order-book").GetProperty("get").GetProperty("summary").GetString()
            .Should().Be("Consulta o livro de ofertas");
        root.GetProperty("paths").GetProperty("/api/v1/trades").GetProperty("get").GetProperty("summary").GetString()
            .Should().Be("Consulta o historico de negocios");
        root.GetProperty("paths").GetProperty("/api/v1/wallets/{userId}").GetProperty("get").GetProperty("summary").GetString()
            .Should().Be("Consulta uma carteira");
        var limitSchema = root.GetProperty("paths").GetProperty("/api/v1/trades").GetProperty("get").GetProperty("parameters")
            .EnumerateArray().Single(parameter => parameter.GetProperty("name").GetString() == "limit").GetProperty("schema");
        limitSchema.GetProperty("type").EnumerateArray().Select(type => type.GetString()).Should().Contain("integer");
        root.GetProperty("paths").EnumerateObject().Should().NotContain(path => path.Name == "/metrics");
    }
}
