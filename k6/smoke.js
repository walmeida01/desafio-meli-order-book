import http from 'k6/http';
import { check } from 'k6';
import { configuration } from './lib/config.js';
import { waitForReadiness } from './lib/readiness.js';
import { classifyResponse } from './lib/classify.js';
import { collectPrometheus } from './lib/prometheus.js';
import { recordOrder } from './lib/metrics.js';

const config = configuration();
export const options = { vus: 1, iterations: 1 };
export function setup() { waitForReadiness(config); return { before: collectPrometheus(config, 'before'), executionId: `${Date.now()}` }; }
export default function (data) {
  const one = (userId, side, price, key) => {
    const started = Date.now();
    const response = http.post(`${config.baseUrl}/api/v1/orders`, JSON.stringify({ userId, side, priceBrlCents: price, quantity: 1 }), { timeout: config.httpTimeout, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key }, tags: { scenario: 'smoke', side, role: 'smoke', phase: 'load' } });
    const classification = classifyResponse(response);
    recordOrder(response, classification, { scenario: 'smoke', side, role: 'smoke', phase: 'load' }, started);
    return response;
  };
  const user1 = '00000000-0000-0000-0000-000000000001';
  const user2 = '00000000-0000-0000-0000-000000000002';
  const created = one(user1, 'BUY', 100, `${data.executionId}-buy`);
  const replay = one(user1, 'BUY', 100, `${data.executionId}-buy`);
  const conflict = one(user1, 'BUY', 101, `${data.executionId}-buy`);
  const sell = one(user2, 'SELL', 100, `${data.executionId}-sell`);
  check(created, { 'smoke creation is accepted': (r) => r.status === 201 });
  check(replay, { 'smoke replay is 200': (r) => r.status === 200 });
  check(conflict, { 'smoke conflict is 409': (r) => r.status === 409 });
  check(sell, { 'smoke BUY/SELL path is accepted': (r) => r.status === 201 });
  const ready = http.get(`${config.baseUrl}/api/v1/ready`, { timeout: config.httpTimeout });
  check(ready, { 'readiness remains true': (r) => r.status === 200 });
  const health = http.get(`${config.baseUrl}/api/v1/health`, { timeout: config.httpTimeout });
  const book = http.get(`${config.baseUrl}/api/v1/order-book`, { timeout: config.httpTimeout });
  const trades = http.get(`${config.baseUrl}/api/v1/trades?limit=1`, { timeout: config.httpTimeout });
  check(health, { 'health is live': (r) => r.status === 200 });
  check(book, { 'order book query is available': (r) => r.status === 200 });
  check(trades, { 'trades query is available': (r) => r.status === 200 });
  const metrics = collectPrometheus(config, 'during');
  check(metrics, { 'normative metrics are present': (m) => m && m.valid });
}
export function teardown(data) { collectPrometheus(config, 'after'); }
