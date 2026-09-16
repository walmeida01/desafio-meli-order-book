# Benchmarks

Use `k6/smoke.js` for a contract smoke and `k6/sustained.js`/`k6/burst.js` for
capacity observations:

```bash
k6 run -e BASE_URL=http://localhost:8080 k6/smoke.js
k6 run -e BASE_URL=http://localhost:8080 -e RATE=10 -e DURATION=30s k6/sustained.js
k6 run -e BASE_URL=http://localhost:8080 -e RATE=100 -e BURST_MULTIPLIER=2 k6/burst.js
```

Registre separadamente requests/s, orders/s e trades/s, status, 429, 503,
falhas de transporte, p50/p95/p99, queue depth e drain. Correlacione com GC,
locks, retries e WAL/fsync observados fora do K6. O script não transforma a
meta de throughput em garantia.
