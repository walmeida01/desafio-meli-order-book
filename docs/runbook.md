# Runbook V1

1. `docker compose up --build` sobe API (`8080`), PostgreSQL (`5432`), Alloy
   (`4317`/`4318` OTLP, UI `12345`), Prometheus (`9090`), Loki (`3100`), Tempo
   (`3200`) e Grafana (`3000`).
2. Verifique `GET /api/v1/health` (liveness), `/metrics` (Prometheus pull) e
   `GET /api/v1/ready` (somente após PostgreSQL, ownership e recovery).
3. O writer aplica migration e seed configurado no Compose; não há group commit
   nem batching e `database_batch_size` permanece 1.
4. Traces/logs seguem API → Alloy via OTLP → Tempo/Loki. Métricas seguem API →
   Prometheus por scraping. Dashboards locais estão em
   `deploy/observability/grafana/dashboards/`.
5. Rode os cenários K6 somente após o serviço estar pronto:
   `k6 run -e BASE_URL=http://localhost:8080 k6/smoke.js`, depois
   `k6 run -e BASE_URL=http://localhost:8080 -e RATE=10 k6/sustained.js` e,
   para pressão acima da referência, `k6 run -e BASE_URL=http://localhost:8080
   -e RATE=100 -e BURST_MULTIPLIER=2 k6/burst.js`. Sustained deve terminar sem
   backlog crescente e, por padrão, sem 429; burst separa 429, 503 e falhas de
   transporte e observa o drain por até 30 segundos.

## Protocolo de carga

Os scripts fazem uma espera ativa por `GET /api/v1/ready` e falham após
`READY_TIMEOUT`. O smoke não mede capacidade. Sustained usa warm-up, taxa fixa,
cooldown e drain; burst interrompe a injeção ao fim de `DURATION` e observa a
fila durante `DRAIN_TIMEOUT`. Em todos os casos `/metrics` é coletado antes,
durante, depois e durante o drain. A transação continua individual e
`database_batch_size` deve permanecer 1.

`k6/orders.js` é apenas um alias de compatibilidade para `k6/smoke.js`.

A V1 deve operar com um único writer. Falha de PostgreSQL, ownership ou recovery mantém `/api/v1/ready` em 503.

## Troubleshooting

- Alloy/Loki/Tempo indisponíveis: confirme containers e portas; exportação é
  não bloqueante e não deve impedir commit, rollback, admission, worker ou
  readiness. O diagnóstico financeiro continua em PostgreSQL e `/metrics`.
- Sem dados no Grafana: valide `api:8080/metrics`, o target do Prometheus e os
  UIDs `orderbook-prometheus`, `orderbook-loki` e `orderbook-tempo`.
- Não exponha secrets versionados. Endpoints OTLP e `SERVICE_VERSION` são
  configurados por variáveis de ambiente; `deployment.environment.name` local
  é `local` por padrão.
