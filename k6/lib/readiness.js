import http from 'k6/http';
import { fail, sleep } from 'k6';
import { durationInSeconds } from './config.js';

export function waitForReadiness(config) {
  const deadline = Date.now() + durationInSeconds(config.readyTimeout) * 1000;
  let lastStatus = 0;
  while (Date.now() < deadline) {
    const response = http.get(`${config.baseUrl}/api/v1/ready`, { timeout: config.httpTimeout, tags: { role: 'readiness', phase: 'setup' } });
    lastStatus = response.status;
    if (response.status === 200) return;
    sleep(1);
  }
  fail(`readiness timeout after ${config.readyTimeout}; last status=${lastStatus}`);
}
