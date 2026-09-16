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
const injectionSeconds = durationInSeconds(config.duration);
let drainStartedAt = 0;
let drainRecorded = false;
export const options = {
  scenarios: {
    pressure: { executor: 'constant-arrival-rate', rate: Math.max(1, Math.ceil(config.rate * config.burstMultiplier)), timeUnit: '1s', duration: config.duration, preAllocatedVUs: config.preAllocatedVUs, maxVUs: config.maxVUs, exec: 'pressure' },
    observe: { executor: 'constant-vus', vus: 1, duration: seconds(injectionSeconds + durationInSeconds(config.drainTimeout)), exec: 'observe' },
  },
  thresholds: {},
};
export function setup() { waitForReadiness(config); return { before: collectPrometheus(config, 'before'), executionId: executionId(config) }; }
export function pressure(data) {
  const workload = orderWorkload(config, data.executionId, __ITER, __VU, 'burst');
  const started = Date.now();
  const response = http.post(`${config.baseUrl}/api/v1/orders`, JSON.stringify(workload.body), { timeout: config.httpTimeout, headers: { 'Content-Type': 'application/json', 'Idempotency-Key': workload.idempotencyKey }, tags: { scenario: 'burst', side: workload.side, role: workload.role, phase: 'pressure' } });
  const classification = classifyResponse(response);
  recordOrder(response, classification, { scenario: 'burst', side: workload.side, role: workload.role, phase: 'pressure' }, started);
  check(response, { 'burst response received': (r) => r.status > 0, 'backpressure is distinguished': (r) => [200, 201, 409, 429, 503].includes(r.status) });
}
export function observe() {
  const phase = exec.scenario.progress >= injectionSeconds / (injectionSeconds + durationInSeconds(config.drainTimeout)) ? 'drain' : 'pressure';
  const snapshot = collectPrometheus(config, phase);
  if (phase === 'drain' && drainStartedAt === 0) drainStartedAt = Date.now();
  const queueValues = snapshot && snapshot.present.orderbook_queue_depth;
  const queueValue = queueValues && queueValues.length > 0 ? queueValues[queueValues.length - 1] : undefined;
  if (phase === 'drain' && !drainRecorded && snapshot && snapshot.valid && queueValue <= 0) {
    drainSeconds.add((Date.now() - drainStartedAt) / 1000, { scenario: 'burst', outcome: 'drained', http_status_class: '2xx', side: 'none', role: 'drain', phase: 'drain' });
    drainRecorded = true;
  }
  sleep(durationInSeconds(config.metricsInterval));
}
export function teardown(data) {
  const after = collectPrometheus(config, 'after');
  if (!drainRecorded) drainSeconds.add(durationInSeconds(config.drainTimeout), { scenario: 'burst', outcome: 'timeout', http_status_class: '2xx', side: 'none', role: 'drain', phase: 'drain' });
  if (after && after.valid) {
    console.log(JSON.stringify({
      scenario: 'burst',
      counterDeltas: {
        ordersReceived: counterDelta(data.before, after, 'orders_received_total'),
        queueRejected: counterDelta(data.before, after, 'orderbook_queue_rejected_total'),
        ordersProcessed: counterDelta(data.before, after, 'orders_processed_total'),
        tradesExecuted: counterDelta(data.before, after, 'trades_executed_total'),
      },
    }));
  }
}
