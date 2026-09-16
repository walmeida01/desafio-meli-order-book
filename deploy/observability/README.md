# Observabilidade

O host expõe `GET /metrics` sem versionamento. O Compose principal sobe API,
PostgreSQL, Alloy, Prometheus, Loki, Tempo e Grafana na mesma rede. Prometheus
faz pull de `api:8080/metrics`; traces e logs fazem push OTLP para Alloy
(gRPC `4317`, HTTP `4318`), que encaminha para Tempo e Loki. Métricas também
fazem push OTLP para Alloy, que usa o exporter Prometheus e remote-write.

Os dashboards versionados `19924.json` e `19925.json` identificam os dashboards
Grafana 19924/19925. A fonte consultada foi a Grafana API
(`https://grafana.com/api/dashboards/<id>/revisions/latest/download`); o datasource
foi adaptado para o UID local `orderbook-prometheus`. Datasources Prometheus,
Loki e Tempo têm UIDs estáveis.

As consultas devem usar somente as métricas normativas:
`orders_received_total`, `orderbook_queue_depth`,
`orderbook_queue_rejected_total`, `orders_processed_total`,
`trades_executed_total`, `matching_duration_seconds`,
`database_batch_flush_duration_seconds` e `database_batch_size`.

O resource compartilhado usa `service.name=meli-order-book-api`,
`SERVICE_VERSION` (ou a versão do assembly), `DEPLOYMENT_ENVIRONMENT_NAME`
(default `local`) e `SERVICE_INSTANCE_ID` opcional. OTLP usa
`OTEL_EXPORTER_OTLP_ENDPOINT` (default `http://alloy:4317`). O pull `/metrics`
continua mantido para compatibilidade. Não se deve somar séries OTLP e pull na
mesma consulta: o pull é a origem do job `orderbook-api`; OTLP é a origem
remote-write para instalações sem scraping.

Logs preservam JSON Console e incluem scopes/correlation e Activity quando
disponíveis. Chaves de idempotência, hashes, saldos, payloads, secrets e
connection strings devem ser redigidos na origem; a indisponibilidade do Alloy
é assíncrona e não bloqueia o caminho financeiro.
