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
        request.Headers.Add("Idempotency-Key", "integration-smoke");
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
}
