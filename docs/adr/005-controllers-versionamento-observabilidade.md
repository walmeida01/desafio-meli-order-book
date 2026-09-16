# ADR-005 — Controllers, versionamento e observabilidade

**Status:** Accepted

## Contexto

A API V1 precisa ter um contrato HTTP estável, permitir evolução compatível e
expor observabilidade operacional sem alterar a arquitetura Modular Monolith,
Vertical Slice e Ports & Adapters. O caminho financeiro continua síncrono,
com Single Writer, Channel e uma transação PostgreSQL individual por comando.

## Decisão

- Substituir Minimal APIs por controllers tradicionais derivados de
  `ControllerBase`.
- Manter `Program` como composition root, responsável por composição de DI,
  configuração e pipeline HTTP.
- Versionar a API de negócio no prefixo `/api/v1`: `POST /api/v1/orders` e os
  demais endpoints de negócio usam esse prefixo.
- Manter `/metrics` sem versionamento por ser endpoint técnico de scraping.
- Usar o Meter `MeliOrderBook.Metrics`, OpenTelemetry Metrics, exporter
  Prometheus e os dashboards Grafana 19924/19925.
- Usar exatamente as métricas `orders_received_total`,
  `orderbook_queue_depth`, `orderbook_queue_rejected_total`,
  `orders_processed_total` com tag `status`, `trades_executed_total`,
  `matching_duration_seconds`, `database_batch_flush_duration_seconds` e
  `database_batch_size`.
- Medir a transação PostgreSQL individual e registrar
  `database_batch_size = 1`. Não implementar group commit ou batching.
- Manter a regra de uma classe/record por arquivo.

`Program` não recebe regra de negócio; controllers apenas transportam,
validam e delegam aos casos de uso das slices.

## Alternativas consideradas

1. **Minimal APIs:** rejeitada porque não atende à decisão de usar controllers
   tradicionais e torna a organização dos contratos HTTP menos explícita para
   a evolução versionada.
2. **Versionamento por host, query string ou header:** rejeitado; o prefixo
   `/api/v1` torna o contrato observável e explícito.
3. **Versionar `/metrics`:** rejeitado; o endpoint é técnico e deve manter um
   endereço estável para Prometheus.
4. **Batching/group commit ou métrica de batch maior que um:** rejeitado; a
   V1 preserva a transação individual por comando e não introduz uma mudança
   de consistência/performance não requerida.
5. **Adicionar métricas legadas ou aliases:** rejeitado para evitar contrato
   observacional ambíguo e cardinalidade/semântica duplicadas.

## Consequências

Controllers e rotas versionadas tornam o contrato HTTP explícito e facilitam
uma futura V2 sem alterar as slices de domínio. `Program` concentra a
composição, preservando a independência do domínio e dos adapters. O endpoint
`/metrics` permanece estável para scraping.

A lista de métricas fica deliberadamente pequena e normativa; dashboards,
alertas e testes devem usar somente esses nomes e a tag `status` indicada.
`database_batch_size` sempre terá valor 1 na V1, e a métrica de flush mede a
transação individual, sem group commit ou batching. A regra de arquivo único
gera mais arquivos, mas reduz ambiguidade de ownership e conflitos de edição.

## Relação com outras decisões

Esta decisão não altera Single Writer, Channel, readiness, atomicidade,
idempotência, retry, recovery ou shutdown. O contrato detalhado está
especificado em `docs/004-sdd.md`.
