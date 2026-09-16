# Protocolo de benchmark U14

## Objetivo e limites

U14 mede o comportamento observado da V1 sem alterar HTTP, Single Writer,
settlement ou a transação PostgreSQL individual. `5.000 requests/s` e
`5.000 trades/s` são metas a medir, nunca capacidades declaradas por
configuração. Uma declaração exige um relatório reproduzível preenchido em
`benchmarks/report-template.md`.

## Cenários

- **smoke:** readiness, criação 201, replay 200, conflito 409, BUY/SELL e
  presença das oito métricas normativas; não declara capacidade.
- **sustained:** warm-up, taxa configurável, janela sustentada, cooldown e
  drain. O default espera zero 429 e backlog estável.
- **burst:** injeta `RATE * BURST_MULTIPLIER`, encerra a injeção e observa o
  drain por até 30s. 429 é evidência de backpressure; 503, falha de transporte
  e status inesperado são categorias distintas.

## Variáveis

`BASE_URL` (http://localhost:8080), `RATE` (10), `PRE_ALLOCATED_VUS` (20),
`MAX_VUS` (100), `DURATION` (30s), `WARMUP` (10s), `COOLDOWN` (10s),
`DRAIN_TIMEOUT` (30s), `METRICS_INTERVAL` (1s), `USER_POOL_SIZE` (2),
`QUANTITY` (1), `PRICE_BRL_CENTS` (100), `BUY_RATIO` (0.5), `REPLAY_RATIO`
(0.05), `CONFLICT_RATIO` (0.01), `HTTP_TIMEOUT` (5s), `READY_TIMEOUT` (60s),
`BURST_MULTIPLIER` (2), `SEED` (17), `USER_IDS` (lista separada por vírgulas),
`METRICS_RETRIES` (2) e `METRICS_RETRY_DELAY` (0.1s). O workload seleciona
somente os primeiros `USER_POOL_SIZE` IDs configurados e nunca inventa UUIDs.
Para o seed mínimo local, o default é `USER_POOL_SIZE=2`; para um pool maior,
informe em `USER_IDS` a lista completa de usuários seedados.

O workload usa IDs/chaves determinísticos derivados de `SEED`, VU e iteração,
com um identificador novo por execução. Ele mistura BUY/SELL, makers/takers,
replay e conflito; quantidade pequena e lados balanceados evitam esgotamento
artificial de saldo. Partial e multiple fills dependem do encontro determinístico
de ordens no livro e devem ser confirmados em trades, não inferidos da taxa.

## Execução limpa e coleta

Suba uma única instância writer e PostgreSQL limpo, aplique migration/seed e
aguarde `/api/v1/ready`. Execute smoke, descarte seu resultado de capacidade,
então sustained e burst separadamente. O K6 coleta `/metrics` antes, durante,
depois e durante o drain; valida `orders_received_total`,
`orderbook_queue_depth`, `orderbook_queue_rejected_total`,
`orders_processed_total`, `trades_executed_total`,
`matching_duration_seconds`, `database_batch_flush_duration_seconds` e
`database_batch_size`. Counter deltas e queue depth máximo/médio/final devem
ser registrados junto de matching/transações. `database_batch_size=1` é
obrigatório.

As métricas K6 separam requests, orders e trades e usam p50/p95/p99 por
categoria. Tags são somente `scenario`, `outcome`, `http_status_class`,
`side`, `role` e `phase`; IDs e chaves nunca são tags.

No dashboard Grafana, **Total de Requisicoes de Ordem** usa
`increase(orders_received_total{job="orderbook-api"}[$__range])`. Ele mede
somente chamadas de submissao de ordem e nao inclui `/metrics`, health, queries
ou Swagger. Para comparar com um resultado K6, selecione no Grafana o mesmo
intervalo da execucao salva no resumo JSON.

Cada alvo `make k6-*` grava automaticamente o resumo estruturado do K6 em
`benchmarks/results/`, com nome composto pelo cenário e timestamp UTC. Por
exemplo:

```bash
make k6-sustained RATE=500 DURATION=30s
# benchmarks/results/sustained-20260916T220000Z.json
```

Para escolher o nome do arquivo sem sobrescrever os demais resultados, informe
somente o nome em `RESULT_FILE`:

```bash
make k6-burst RESULT_FILE=burst-r500.json RATE=500
```

Os arquivos JSON de resultado não são versionados. Eles contêm o resumo final
do K6, incluindo contadores, tendências, percentis e `dropped_iterations`;
registre os valores relevantes no relatório de benchmark.

## Interpretação e limitações

Informe commit, versões, hardware, PostgreSQL, configuração, volume, mix,
taxa, latências, erros, 429, 503, transporte, backlog e drain. Relacione
GC/allocations, locks, retries e WAL/fsync com observabilidade da aplicação e
do banco. Timeout financeiro de 2s não é automaticamente um SLO HTTP. VUs,
taxa configurada ou ausência de 429 em uma amostra não provam capacidade.
