using System.Diagnostics.Metrics;
using OrderBook.Infrastructure.Postgres;

namespace OrderBook.Infrastructure.Observability;

public sealed class OrderBookMetrics : IDisposable
{
    public const string MeterName = "MeliOrderBook.Metrics";
    private readonly Meter meter;

    public Counter<long> OrdersReceived { get; }
    public ObservableGauge<int> QueueDepth { get; }
    public Counter<long> QueueRejected { get; }
    public Counter<long> OrdersProcessed { get; }
    public Counter<long> TradesExecuted { get; }
    public Histogram<double> MatchingDuration { get; }
    public Histogram<double> DatabaseBatchFlushDuration { get; }
    public Histogram<double> DatabaseBatchSize { get; }

    public OrderBookMetrics(OrderCommandQueue queue)
    {
        meter = new(MeterName, "1.0.0");
        OrdersReceived = meter.CreateCounter<long>("orders_received_total");
        QueueDepth = meter.CreateObservableGauge("orderbook_queue_depth", () => queue.Count);
        QueueRejected = meter.CreateCounter<long>("orderbook_queue_rejected_total");
        OrdersProcessed = meter.CreateCounter<long>("orders_processed_total");
        TradesExecuted = meter.CreateCounter<long>("trades_executed_total");
        MatchingDuration = meter.CreateHistogram<double>("matching_duration_seconds", "s", "Matching duration");
        DatabaseBatchFlushDuration = meter.CreateHistogram<double>("database_batch_flush_duration_seconds", "s", "Individual PostgreSQL transaction duration");
        DatabaseBatchSize = meter.CreateHistogram<double>("database_batch_size", "value", "One transaction per command");
    }

    public void Dispose() => meter.Dispose();
}
