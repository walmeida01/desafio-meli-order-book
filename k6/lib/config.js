const durationInSeconds = (value, fallback) => {
  const text = String(value || fallback).trim();
  const match = text.match(/^(\d+(?:\.\d+)?)(ms|s|m|h)?$/);
  if (!match) throw new Error(`invalid duration: ${text}`);
  const amount = Number(match[1]);
  const unit = match[2] || 's';
  return unit === 'ms' ? amount / 1000 : unit === 'm' ? amount * 60 : unit === 'h' ? amount * 3600 : amount;
};

const seconds = (value) => `${Math.max(0, Math.ceil(value))}s`;

export function configuration() {
  return {
    baseUrl: (__ENV.BASE_URL || 'http://localhost:8080').replace(/\/$/, ''),
    rate: Number(__ENV.RATE || 10),
    preAllocatedVUs: Number(__ENV.PRE_ALLOCATED_VUS || 20),
    maxVUs: Number(__ENV.MAX_VUS || 100),
    duration: __ENV.DURATION || '30s',
    warmup: __ENV.WARMUP || '10s',
    cooldown: __ENV.COOLDOWN || '10s',
    drainTimeout: __ENV.DRAIN_TIMEOUT || '30s',
    metricsInterval: __ENV.METRICS_INTERVAL || '1s',
    userPoolSize: Number(__ENV.USER_POOL_SIZE || 1000),
    quantity: Number(__ENV.QUANTITY || 1),
    price: Number(__ENV.PRICE_BRL_CENTS || 100),
    buyRatio: Number(__ENV.BUY_RATIO !== undefined ? __ENV.BUY_RATIO : 0.5),
    replayRatio: Number(__ENV.REPLAY_RATIO !== undefined ? __ENV.REPLAY_RATIO : 0.05),
    conflictRatio: Number(__ENV.CONFLICT_RATIO !== undefined ? __ENV.CONFLICT_RATIO : 0.01),
    httpTimeout: __ENV.HTTP_TIMEOUT || '5s',
    readyTimeout: __ENV.READY_TIMEOUT || '60s',
    burstMultiplier: Number(__ENV.BURST_MULTIPLIER || 2),
    seed: Number(__ENV.SEED || 17),
  };
}

export function scenarioSeconds(config, includeDrain = true) {
  const load = durationInSeconds(config.warmup) + durationInSeconds(config.duration) + durationInSeconds(config.cooldown);
  return load + (includeDrain ? durationInSeconds(config.drainTimeout) : 0);
}

export { durationInSeconds, seconds };
