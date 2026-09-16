function hash(seed, value) {
  let result = seed >>> 0;
  for (let i = 0; i < value.length; i += 1) result = Math.imul(result ^ value.charCodeAt(i), 16777619);
  return (result >>> 0) / 4294967296;
}

function userId(index) {
  return `00000000-0000-0000-0000-${String((index % 999999999999999) + 1).padStart(12, '0')}`;
}

export function orderWorkload(config, executionId, iteration, vu, scenario) {
  const index = vu * 1000000 + iteration;
  const random = hash(config.seed, `${executionId}:${index}`);
  const side = random < config.buyRatio ? 'BUY' : 'SELL';
  const role = random < 0.35 ? 'maker' : 'taker';
  const isReplay = random >= 0.35 && random < 0.35 + config.replayRatio;
  const isConflict = !isReplay && random < 0.35 + config.replayRatio + config.conflictRatio;
  const pairPrice = role === 'maker' ? (side === 'BUY' ? config.price - 1 : config.price + 1) : config.price;
  const pair = Math.floor(index / 2);
  const keyBase = `${executionId}-${scenario}-${pair}`;
  const idempotencyKey = isReplay || isConflict ? keyBase : `${keyBase}-${index}`;
  return {
    userId: userId(index % Math.max(1, config.userPoolSize)),
    side, role, idempotencyKey, isReplay, isConflict,
    body: { userId: userId(index % Math.max(1, config.userPoolSize)), side, priceBrlCents: pairPrice + (isConflict ? 1 : 0), quantity: config.quantity },
  };
}

export function executionId(config) { return __ENV.EXECUTION_ID || `${config.seed}-${Date.now()}-${Math.floor(Math.random() * 1000000)}`; }
