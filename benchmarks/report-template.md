# Order Book benchmark report

Complete this file only after a real run. Do not infer capacity from configuration.

## Environment

- Commit:
- Date:
- .NET/runtime:
- PostgreSQL version/configuration:
- Host/CPU/RAM:
- API configuration:
- VUs and duration:
- Mix BUY/SELL:

## Results

| Scenario | requests/s | orders/s | trades/s | p50 | p95 | p99 | errors | 429 | max queue depth |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| sustained | — | — | — | — | — | — | — | — | — |
| burst | — | — | — | — | — | — | — | — | — |

## Interpretation

Record matching/settlement latency, retries, database lock waits, GC/allocation observations, WAL/fsync observations and drain time. A result is not a V1 capacity guarantee unless the scenario and measured hardware are included.
