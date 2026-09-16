import http from 'k6/http';
import { check, sleep } from 'k6';
import { queueDepth, scrapeFailures } from './metrics.js';

const normative = [
  'orders_received_total', 'orderbook_queue_depth', 'orderbook_queue_rejected_total',
  'orders_processed_total', 'trades_executed_total', 'matching_duration_seconds',
  'database_batch_flush_duration_seconds', 'database_batch_size',
];

const exportedName = (name) => name === 'database_batch_size' ? 'database_batch_size_value' : name;

function values(text, name) {
  const result = [];
  const metricName = exportedName(name);
  for (const line of text.split('\n')) {
    if (line.startsWith('#') || !line.startsWith(metricName)) continue;
    const match = line.match(new RegExp(`^${metricName}(?:_(?:bucket|sum|count))?(?:\\{[^}]*\\})?\\s+([-+0-9.eE]+)`));
    if (match) result.push(Number(match[1]));
  }
  return result;
}

export function collectPrometheus(config, phase) {
  let response;
  let attempt = 0;
  while (attempt <= config.metricsRetries) {
    response = http.get(`${config.baseUrl}/metrics`, { timeout: config.httpTimeout, tags: { role: 'metrics', phase } });
    const transient = response.status === 0 || response.status === 408 || response.status === 429 || response.status >= 500;
    if (response.status === 200 || !transient || attempt === config.metricsRetries) break;
    attempt += 1;
    sleep(config.metricsRetryDelay);
  }
  if (response.status !== 200) {
    const statusClass = response.status === 0 ? 'transport' : `${Math.floor(response.status / 100)}xx`;
    console.log(`metrics scrape failed: phase=${phase} status=${response.status} attempts=${attempt + 1}`);
    scrapeFailures.add(1, { scenario: 'metrics', outcome: response.status === 0 ? 'transport_failure' : 'http_failure', http_status_class: statusClass, side: 'none', role: 'metrics', phase });
    return { present: {}, valid: false, phase, diagnostic: 'http_failure', status: response.status, missing: normative };
  }
  const present = {};
  for (const name of normative) present[name] = values(response.body, name);
  const missing = normative.filter((name) => present[name].length === 0);
  const valid = missing.length === 0;
  if (phase !== 'before') check(response, { 'Prometheus scrape succeeded': () => valid });
  if (!valid) {
    console.log(`metrics scrape missing normative metrics: phase=${phase} status=200 missing=${missing.join(',')}`);
    if (phase !== 'before') scrapeFailures.add(1, { scenario: 'metrics', outcome: 'missing_metric', http_status_class: '2xx', side: 'none', role: 'metrics', phase });
    return { present, valid: false, phase, diagnostic: 'missing_metric', status: 200, missing };
  }
  queueDepth.add(present.orderbook_queue_depth[present.orderbook_queue_depth.length - 1], { scenario: 'metrics', outcome: 'observed', http_status_class: '2xx', side: 'none', role: 'metrics', phase });
  return { present, valid: true, phase };
}

export function counterDelta(before, after, name) {
  const a = before && before.present[name] ? before.present[name][before.present[name].length - 1] : 0;
  const b = after && after.present[name] ? after.present[name][after.present[name].length - 1] : 0;
  return Math.max(0, b - a);
}

export { normative };
