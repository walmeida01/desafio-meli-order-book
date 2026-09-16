import http from 'k6/http';
import { check, sleep } from 'k6';
import exec from 'k6/execution';
import { configuration, durationInSeconds, seconds } from './lib/config.js';
import { waitForReadiness } from './lib/readiness.js';
import { classifyResponse } from './lib/classify.js';
import { collectPrometheus, counterDelta } from './lib/prometheus.js';
import { recordOrder, drainSeconds } from './lib/metrics.js';
import { executionId, orderWorkload } from './lib/workload.js';

const config = configuration();
const loadSeconds = durationInSeconds(config.warmup) + durationInSeconds(config.duration) + durationInSeconds(config.cooldown);
let drainStartedAt = 0;
let drainRecorded = false;
export const options = {
  scenarios: {
    orders: { executor: 'ramping-arrival-rate', startRate: 0, timeUnit: '1s', preAllocatedVUs: config.preAllocatedVUs, maxVUs: config.maxVUs, stages: [{ duration: config.warmup, target: config.rate }, { duration: config.duration, target: config.rate }, { duration: config.cooldown, target: 0 }], exec: 'orders' },
    observe: { executor: 'constant-vus', vus: 1, duration: seconds(loadSeconds + durationInSeconds(config.drainTimeout)), exec: 'observe', startTime: '0s' },
  },
  thresholds: { benchmark_requests_total: ['count>0'] },
};
export function setup() { waitForReadiness(config); return { before: collectPrometheus(config, 'before'), executionId: executionId(config) }; }
export function orders(data) {
  const workload = orderWorkload(config, data.executionId, __ITER, __VU, 'sustained');
  const phase = exec.scenario.progress < durationInSeconds(config.warmup) / (loadSeconds || 1) ? 'warmup' : exec.scenario.progress > (loadSeconds - durationInSeconds(config.cooldown)) / (loadSeconds || 1) ? 'cooldown' : 'sustained';
  const started = Date.now();
  const response = http.post(`${config.baseUrl}/api/v1/orders`, JSON.stringify(workload.body), { timeout: config.httpTimeout, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': workload.idempotencyKey }, tags: { scenario: 'sustained', side: workload.side, role: workload.role, phase } });
  const classification = classifyResponse(response);
  recordOrder(response, classification, { scenario: 'sustained', side: workload.side, role: workload.role, phase }, started);
  check(response, { 'sustained response received': (r) => r.status > 0, 'sustained status is classified': (r) => [200, 201, 409, 503].includes(r.status), 'sustained has no backpressure': (r) => r.status !== 429 });
}
export function observe(data) {
  const progress = exec.scenario.progress;
  const phase = progress < durationInSeconds(config.warmup) / ((loadSeconds + durationInSeconds(config.drainTimeout)) || 1) ? 'warmup' : progress > loadSeconds / ((loadSeconds + durationInSeconds(config.drainTimeout)) || 1) ? 'drain' : 'sustained';
  const snapshot = collectPrometheus(config, phase);
  if (phase === 'drain' && drainStartedAt === 0) drainStartedAt = Date.now();
  const queueValues = snapshot && snapshot.present.orderbook_queue_depth;
  const queueValue = queueValues && queueValues.length > 0 ? queueValues[queueValues.length - 1] : undefined;
  if (phase === 'drain' && !drainRecorded && snapshot && snapshot.valid && queueValue <= 0) {
    drainSeconds.add((Date.now() - drainStartedAt) / 1000, { scenario: 'sustained', outcome: 'drained', http_status_class: '2xx', side: 'none', role: 'drain', phase: 'drain' });
    drainRecorded = true;
  }
  sleep(durationInSeconds(config.metricsInterval));
}
export function teardown(data) {
  const after = collectPrometheus(config, 'after');
  const elapsed = Date.now();
  if (!drainRecorded) drainSeconds.add(Math.min(durationInSeconds(config.drainTimeout), elapsed / 1000), { scenario: 'sustained', outcome: 'timeout', http_status_class: '2xx', side: 'none', role: 'drain', phase: 'drain' });
  check(after, { 'all normative metrics collected after load': (m) => m && m.valid, 'orders counter is observable': (m) => counterDelta(data.before, m, 'orders_received_total') >= 0 });
  if (after && after.valid) {
    console.log(JSON.stringify({
      scenario: 'sustained',
      counterDeltas: {
        ordersReceived: counterDelta(data.before, after, 'orders_received_total'),
        queueRejected: counterDelta(data.before, after, 'orderbook_queue_rejected_total'),
        ordersProcessed: counterDelta(data.before, after, 'orders_processed_total'),
        tradesExecuted: counterDelta(data.before, after, 'trades_executed_total'),
      },
    }));
  }
}
