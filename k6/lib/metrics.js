import { Counter, Gauge, Trend } from 'k6/metrics';

export const benchmarkRequests = new Counter('benchmark_requests_total');
export const benchmarkOrders = new Counter('benchmark_orders_total');
export const benchmarkTrades = new Counter('benchmark_trades_total');
export const requestLatency = new Trend('benchmark_request_latency_ms', true);
export const orderLatency = new Trend('benchmark_order_latency_ms', true);
export const tradeLatency = new Trend('benchmark_trade_latency_ms', true);
export const queueDepth = new Gauge('benchmark_queue_depth');
export const drainSeconds = new Trend('benchmark_drain_seconds', true);
export const scrapeFailures = new Counter('benchmark_prometheus_scrape_failures_total');

export function recordOrder(response, classification, context, startedAt) {
  const tags = { scenario: context.scenario, outcome: classification.outcome, http_status_class: classification.statusClass, side: context.side, role: context.role, phase: context.phase };
  const elapsed = Date.now() - startedAt;
  benchmarkRequests.add(1, tags);
  requestLatency.add(elapsed, tags);
  benchmarkOrders.add(1, tags);
  orderLatency.add(elapsed, tags);
  let trades = 0;
  try { trades = Array.isArray(response.json('trades')) ? response.json('trades').length : 0; } catch (_) { trades = 0; }
  if (trades > 0) {
    benchmarkTrades.add(trades, tags);
    tradeLatency.add(elapsed, tags);
  }
  return trades;
}
