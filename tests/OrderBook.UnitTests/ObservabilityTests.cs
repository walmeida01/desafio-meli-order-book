using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentAssertions;
using OrderBook.Infrastructure.Observability;
using OrderBook.Infrastructure.Postgres;
using OpenTelemetry.Resources;
using Xunit;

namespace OrderBook.UnitTests;

public sealed class ObservabilityTests
{
    [Fact]
    public void Default_resource_metadata_is_stable_and_local()
    {
        var options = new ObservabilityOptions();

        ObservabilityOptions.ServiceName.Should().Be("meli-order-book-api");
        options.DeploymentEnvironmentName.Should().Be("local");
        options.ServiceVersion.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Sensitive_values_are_redacted_at_the_source()
    {
        var result = SensitiveDataRedactor.Redact("Idempotency-Key=abc hash=def balance=100 password=secret");

        result.Should().NotContain("abc").And.NotContain("def").And.NotContain("100").And.NotContain("secret");
        result.Should().Contain("[REDACTED]");
    }

    [Fact]
    public void Normative_meter_has_exact_instrument_names()
    {
        var queue = new OrderCommandQueue();
        using var metrics = new OrderBookMetrics(queue);
        var names = new List<string>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, _) =>
        {
            if (instrument.Meter.Name == OrderBookMetrics.MeterName) names.Add(instrument.Name);
        };
        listener.Start();

        metrics.OrdersReceived.Add(1, new KeyValuePair<string, object?>("side", "BUY"));
        metrics.QueueRejected.Add(1);
        metrics.OrdersProcessed.Add(1, new KeyValuePair<string, object?>("status", "accepted"));

        names.Should().BeEquivalentTo(
            "orders_received_total", "orderbook_queue_depth", "orderbook_queue_rejected_total",
            "orders_processed_total", "trades_executed_total", "matching_duration_seconds",
            "database_batch_flush_duration_seconds", "database_batch_size");
    }

    [Fact]
    public void Orders_received_is_labeled_by_side()
    {
        var queue = new OrderCommandQueue();
        using var metrics = new OrderBookMetrics(queue);
        var sides = new List<string>();
        using var listener = new MeterListener();
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
        {
            if (instrument.Name != "orders_received_total") return;
            foreach (var tag in tags)
                if (tag.Key == "side") sides.Add((string)tag.Value!);
        });
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == OrderBookMetrics.MeterName) meterListener.EnableMeasurementEvents(instrument);
        };
        listener.Start();

        metrics.OrdersReceived.Add(1, new KeyValuePair<string, object?>("side", "BUY"));
        metrics.OrdersReceived.Add(1, new KeyValuePair<string, object?>("side", "SELL"));

        sides.Should().Equal("BUY", "SELL");
    }

    [Fact]
    public void Manual_activity_uses_the_registered_source_name()
    {
        using var source = new ActivitySource(ObservabilityOptions.ActivitySourceName);
        source.Name.Should().Be("MeliOrderBook");
    }

    [Fact]
    public void Resource_metadata_keeps_service_version_out_of_namespace()
    {
        var resource = ResourceBuilder.CreateDefault()
            .AddService(
                serviceName: ObservabilityOptions.ServiceName,
                serviceVersion: "1.0.0.0")
            .Build();

        resource.Attributes.Should().Contain(new KeyValuePair<string, object>(
            "service.name", ObservabilityOptions.ServiceName));
        resource.Attributes.Should().Contain(new KeyValuePair<string, object>(
            "service.version", "1.0.0.0"));
        resource.Attributes.Should().NotContain(item =>
            item.Key == "service.namespace" && Equals(item.Value, "1.0.0.0"));
    }
}
