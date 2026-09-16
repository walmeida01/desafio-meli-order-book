import http from 'k6/http';
import { check } from 'k6';
import { queueDepth, scrapeFailures } from './metrics.js';

const normative = [
  'orders_received_total', 'orderbook_queue_depth', 'orderbook_queue_rejected_total',
  'orders_processed_total', 'trades_executed_total', 'matching_duration_seconds',
  'database_batch_flush_duration_seconds', 'database_batch_size',
];

function values(text, name) {
  const result = [];
  for (const line of text.split('\n')) {
    if (line.startsWith('#') || !line.startsWith(name)) continue;
    const match = line.match(new RegExp(`^${name}(?:_(?:bucket|sum|count))?(?:\\{[^}]*\\})?\\s+([-+0-9.eE]+)`));
    if (match) result.push(Number(match[1]));
  }
  return result;
}

export function collectPrometheus(config, phase) {
  const response = http.get(`${config.baseUrl}/metrics`, { timeout: config.httpTimeout, tags: { role: 'metrics', phase } });
  if (response.status !== 200) { scrapeFailures.add(1, { scenario: 'metrics', outcome: 'unexpected', http_status_class: `${Math.floor(response.status / 100)}xx`, side: 'none', role: 'metrics', phase }); return null; }
  const present = {};
  for (const name of normative) present[name] = values(response.body, name);
  const valid = normative.every((name) => present[name].length > 0);
  check(response, { 'Prometheus scrape succeeded': () => valid });
  if (!valid) { scrapeFailures.add(1, { scenario: 'metrics', outcome: 'unexpected', http_status_class: '2xx', side: 'none', role: 'metrics', phase }); return { present, valid: false, phase }; }
  queueDepth.add(present.orderbook_queue_depth[present.orderbook_queue_depth.length - 1], { scenario: 'metrics', outcome: 'observed', http_status_class: '2xx', side: 'none', role: 'metrics', phase });
  return { present, valid: true, phase };
}

export function counterDelta(before, after, name) {
  const a = before && before.present[name] ? before.present[name][before.present[name].length - 1] : 0;
  const b = after && after.present[name] ? after.present[name][after.present[name].length - 1] : 0;
  return Math.max(0, b - a);
}

export { normative };
