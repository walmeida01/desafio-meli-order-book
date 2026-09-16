# 002 — Arquitetura

> **Status:** Accepted para a V1. Este documento materializa decisões aprovadas sem alterar `docs/004-sdd.md`.

## 1. Contexto, escopo e decisões consolidadas

O MVP negocia apenas Vibranium contra BRL, por ordens limitadas BUY/SELL. A prioridade é corretude financeira, consistência, determinismo, simplicidade, performance e escala. A solução é um **Modular Monolith**, organizado por **Vertical Slices**, com **Hexagonal/Ports & Adapters** e **DDD tático**.

Stack V1: **.NET 10, C#, ASP.NET Core com controllers `ControllerBase`, PostgreSQL, Dapper sobre Npgsql e migrations versionadas**. `Program` é o composition root de DI e pipeline. A API de negócio usa `/api/v1`; `/metrics` permanece sem versionamento. Testes usam xUnit com AAA, FluentAssertions, Testcontainers e K6. O caminho de mutation é HTTP síncrono; não há Kafka, Outbox ou processamento assíncrono de negócio na V1.

Inclui submissão, livro, matching, reservas, settlement, histórico, queries, idempotência, recovery e observabilidade. Cancelamento, market orders, múltiplos ativos, taxas, autenticação, cadastro completo e escala horizontal são fora de escopo. Kafka é somente evolução V2, condicionada a novos requisitos e ADR.

## 2. Requisitos, hipóteses e invariantes

RF01–RF12 e RNF01–RNF08 são atendidos pelas slices e gates do plano. A referência de 5.000 requests/s e 5.000 trades/s é meta de benchmark: não é capacidade garantida antes de prova em cenário representativo.

Hipóteses explícitas: usuários e saldos vêm de seed/fixture; BRL é `long` em centavos; quantidade e preço são inteiros positivos; há um processo ativo e um livro na V1; PostgreSQL é dependência obrigatória para readiness e mutations. Não tratar hipóteses como requisitos de produto.

Invariantes: `available` e `locked` nunca negativos; reserva financia uma única ordem; `remaining` não excede `original`; acceptedSequence de ordem aceita é única, monotônica e nunca reutilizada; cada efeito financeiro é aplicado uma vez; `(userId, Idempotency-Key)` não cria ordens duplicadas; BRL e Vibranium são conservados; o livro só expõe estado committed; divergência de rebuild mantém readiness falsa.

Não há open question bloqueante para a V1. Frações, taxas, cancelamento, autenticação, multi-instrumento, particionamento e mensageria exigem requisitos/ADRs próprios.

## 3. Componentes e ownership

- **HTTP/API:** controllers `ControllerBase`, DTOs, validação, versionamento `/api/v1`, códigos HTTP e correlation; não contém regra financeira. `/metrics` é endpoint técnico não versionado.
- **Submit Order:** intenção, idempotência, admissão e resultado.
- **Matching:** único escritor lógico do Order Book; estado mutável em memória e algoritmo price-time.
- **Wallet/Reservation:** regras de available/locked e reserva por lado.
- **Settlement:** Trade, Ledger e quatro efeitos financeiros.
- **Postgres adapters:** SQL explícito via Dapper/Npgsql, transactions, locks e migrations; implementam portas das slices.
- **Queries:** leitura sem autoridade sobre mutations.
- **Runtime/Observability:** Channel, advisory lock, readiness, shutdown, logs, métricas e traces.

O processo que obtém `pg_try_advisory_lock` em conexão PostgreSQL dedicada é o único writer autorizado. Uma instância sem lock fica não-ready e não processa mutations; não existe split-brain tolerado. A conexão dedicada permanece aberta durante o ownership e libera o lock ao desligar/romper a sessão. A V1 não faz failover automático de múltiplos writers.

## 4. Lifecycle de Order

Estados persistidos: `OPEN`, `PARTIALLY_FILLED`, `FILLED`, `REJECTED`. `QUEUED` é apenas transitório e nunca é estado committed. Transições: `QUEUED → OPEN` quando aceita sem fill; `QUEUED → PARTIALLY_FILLED` quando há fill e sobra; `QUEUED → FILLED` quando totalmente preenchida; `QUEUED → REJECTED` em falha de validação/saldo/limite antes do aceite; `OPEN → PARTIALLY_FILLED → FILLED` por fills. Uma ordem committed não volta nem é cancelável na V1.

`acceptedSequence` é obtida pelo `nextval` de uma sequence PostgreSQL dedicada dentro do processamento do comando. Sequence PostgreSQL não faz rollback: uma falha pode consumir um número que não será persistido, produzindo gap permitido e nunca reutilizado. A coluna só é gravada para ordem aceita; rejeições não recebem sequência. Retry idempotente retorna a ordem persistida e reutiliza sua sequência original.

## 5. Matching e self-trade

BUY tem prioridade por maior preço; SELL por menor preço; empate por menor `acceptedSequence`. BUY cruza SELL quando `buy.limitPrice >= sell.limitPrice`. A ordem entrante é taker; cada fill consome a melhor resting order. O preço do trade é o preço maker. Partial e multiple fills são permitidos. O restante permanece no livro e define o lifecycle.

Self-trade é permitido e tratado como trade normal; não há filtro por `userId`. Mesmo quando buyer e seller são a mesma Wallet, Trade e Ledger registram os quatro efeitos auditáveis, com locks e aplicação idempotente, sem criar/destruir ativos.

## 6. Admissão, Channel e shutdown

HTTP exige `Idempotency-Key`, valida payload e tenta `TryWrite` em `Channel<T>` bounded, `SingleReader=true`, múltiplos writers, capacidade default **4096**. `TryWrite=false` retorna **429** com `Retry-After`; nenhum comando escrito é descartado. Readiness falsa retorna 503 e impede novas mutations. O timeout default do comando/transação é **2 segundos**.

O reader único verifica idempotência, obtém sequência/estado e executa matching/settlement síncronos. Um comando e todos os seus fills pertencem a uma única transação PostgreSQL individual. `database_batch_size` é sempre registrado como 1 e `database_batch_flush_duration_seconds` mede essa transação; não há batching de aplicação nem group commit.

No shutdown: readiness fica falsa, novas admissões são interrompidas, o Channel é drenado por até **30 segundos** e o comando em execução termina ou sofre rollback. Comandos não concluídos não são marcados como aceitos fora do commit; após restart, recovery seguro e retry por idempotência resolvem o resultado.

## 7. Reservas, settlement e consistência

BUY reserva `quantity × limitPrice` centavos BRL: reduz BRL available e aumenta BRL locked. SELL reserva `quantity` Vibranium: reduz Vibranium available e aumenta Vibranium locked.

Para cada fill de quantidade `q` e preço maker `p`, o settlement captura `q × p` BRL do locked do comprador e `q` Vibranium do locked do vendedor; credita `q` Vibranium ao available do comprador e `q × p` BRL ao available do vendedor. A diferença entre o valor reservado no limite e o custo maker é liberada ao BRL available do comprador. Ao completar a ordem, o locked residual é liberado (zero após captura). Não há reembolso fora dessa liberação de excedente; falha integral faz rollback de toda reserva e efeito.

Cada fill possui quatro efeitos Ledger auditáveis: débito BRL do buyer, crédito Vibranium do buyer, débito Vibranium do seller e crédito BRL do seller. `Trade` referencia os quatro lançamentos; buyer=seller ainda gera os quatro registros, mesmo que o neto da Wallet seja zero em cada ativo. Nenhum fill parcial é confirmado isoladamente.

Há uma transação PostgreSQL por comando, `READ COMMITTED`, timeout de 2s, `SELECT ... FOR UPDATE` e locks de wallets distintos ordenados por `userId`. Deadlock ou serialization failure pode ser repetido no máximo 3 vezes; nenhuma outra falha recebe retry automático. Cada tentativa recalcula/valida de forma idempotente e só publica memória após commit. Limites default: no máximo **1.000 fills por comando** e quantidade máxima configurável **1.000.000.000**. Exceder limite ou timeout provoca rollback integral e rejeição, sem estado publicado.

## 8. Schema mínimo e contratos DB

Todas as quantidades/preços são inteiros e têm `CHECK` de faixa/não negatividade; IDs são UUID; timestamps são UTC.

- `wallets(id, user_id UNIQUE, brl_available, brl_locked, vibranium_available, vibranium_locked, version, updated_at)`.
- `orders(id, user_id, side, limit_price_brl_cents, original_quantity, remaining_quantity, status, accepted_sequence UNIQUE NULLABLE, created_at, updated_at)`, com índice `(status, side, limit_price_brl_cents, accepted_sequence)` e `accepted_sequence` nula para rejeitadas.
- `reservations(id, order_id UNIQUE, user_id, side, asset, original_amount, remaining_amount, status, created_at, updated_at)`; amount restante acompanha fills.
- `trades(id UNIQUE, taker_order_id, maker_order_id, buyer_user_id, seller_user_id, quantity, price_brl_cents, accepted_sequence, created_at)`.
- `ledger_entries(id UNIQUE, trade_id, effect_type, user_id, asset, direction, amount, created_at)`, unique `(trade_id,effect_type)` e exatamente os quatro tipos por trade.
- `idempotency_records(user_id, idempotency_key, canonical_payload_hash, order_id, response_status, response_body, created_at, updated_at, PRIMARY KEY(user_id,idempotency_key))`.

Além das tabelas, a migration cria a sequence `accepted_order_sequence`; seu avanço não participa do rollback do comando, justamente para preservar monotonicidade sem reutilização.

Foreign keys, unique constraints e checks protegem referências, duplicate trade, duplicate effects e saldos. Migrations versionadas e seed explícito são a única evolução de schema da V1; SQL é parametrizado.

## 9. Contratos API e internos

`POST /api/v1/orders` recebe `{userId, side, priceBrlCents, quantity}` e header `Idempotency-Key`. Novo resultado 201; replay idêntico 200; payload diferente para a mesma tupla retorna 409; inválido 400; saldo/limite 409; Channel cheio 429 com `Retry-After`; não-ready 503. Resposta persistida contém `orderId`, status, original/executed/remaining, `acceptedSequence` quando aceita e lista de `{tradeId, quantity, priceBrlCents}`.

Queries: `GET /api/v1/order-book`, `GET /api/v1/trades` (ordem determinística e paginação), `GET /api/v1/orders/{id}`, `GET /api/v1/wallets/{userId}`. São read-only e podem observar apenas estado committed; não há autenticação V1. `/api/v1/health` e `/api/v1/ready` são endpoints técnicos.

Contratos imutáveis por slice: `SubmitOrderCommand`, `MatchingResult`, `SettlementCommand` e `OrderResult`. Eventos `OrderAccepted`, `OrderPartiallyFilled`, `OrderFilled` e `TradeSettled` são internos e pós-commit, para telemetria/handlers locais; não são mensagens duráveis nem broker. Falha de handler pós-commit não desfaz settlement.

## 10. Recovery e failure matrix

No startup, readiness começa falsa; valida schema, adquire advisory lock, carrega Orders committed `OPEN/PARTIALLY_FILLED` por `acceptedSequence`, reconstrói filas, valida reservas e invariantes (incluindo correspondência de remaining/locked). Divergência mantém readiness falsa e exige diagnóstico; não há reparo silencioso.

| Cenário | Comportamento | Recovery |
|---|---|---|
| Payload inválido/saldo insuficiente | 400/409, sem aceite | corrigir requisição/saldo |
| Channel cheio | 429 + Retry-After, comando não escrito | retry com mesma chave |
| Timeout/erro antes do commit | rollback integral, sem publicação | retry idempotente; só deadlock/serialization até 3 retries |
| Crash após commit antes da resposta | estado committed existe | retry retorna resposta persistida |
| Crash durante settlement | PostgreSQL faz rollback da transação interrompida | rebuild/retry |
| Trade duplicado | unique/ledger key impede reaplicação | no-op seguro ou conflito |
| PostgreSQL indisponível | 503, readiness falsa, sem mutation | restaurar dependência e recovery |
| Perda/restart do processo | memória descartada | rebuild; ready apenas após validação |
| Dois writers | processo sem advisory lock não-ready e não processa | manter/recuperar lock único |
| Divergência no rebuild | não-ready, dados preservados | investigação explícita |
| Shutdown | readiness falsa, interrompe admissão e drena até 30s | retry não concluído por idempotência |

## 11. NFRs, observabilidade e segurança

Performance deve medir throughput de requests/orders/trades, p50/p95/p99, error rate, backlog e tempo de drain. A instrumentação normativa usa somente as métricas definidas na seção de observabilidade; `database_batch_size` é 1 e `database_batch_flush_duration_seconds` mede a transação PostgreSQL individual. Não há batching de aplicação nem group commit. O cenário sustentado deve não acumular backlog; o burst deve mostrar 429 e drenagem. Não declarar 5.000 requests/s ou trades/s garantidos antes de benchmark sustentado e representativo, com volume, hardware, versões e configuração registrados.

Concorrência é serializada no livro; writers HTTP são paralelos; SQL locks são ordenados. READ COMMITTED + locks + constraints e idempotência evitam double spending e retries duplicados. Resiliência é dada por rollback, restart/rebuild, readiness e retry limitado.

Logs JSON estruturados incluem correlation ID, request/Idempotency hash redigido, order/trade/sequence, status, duração, queue e erro sem segredos/saldos completos. O Meter é `MeliOrderBook.Metrics`, com OpenTelemetry Metrics, exporter Prometheus e dashboards Grafana 19924/19925. As métricas normativas são exatamente `orders_received_total`, `orderbook_queue_depth`, `orderbook_queue_rejected_total`, `orders_processed_total` com tag `status`, `trades_executed_total`, `matching_duration_seconds`, `database_batch_flush_duration_seconds` e `database_batch_size`; esta última registra sempre 1. Traces cobrem HTTP→Channel→command→DB. `/metrics` não é versionado; `/api/v1/health` é liveness; `/api/v1/ready` exige advisory lock, PostgreSQL/schema, recovery concluído e nenhuma divergência.

Validar ranges/tamanho/paginação, parametrizar SQL e limitar cardinalidade de labels. TLS, autenticação e autorização permanecem fora da V1; não registrar chaves ou dados sensíveis.

## 12. Migração, rollback e trade-offs

Deploy V1 usa uma instância writer e PostgreSQL; migrations controladas e seed reproduzível. Rollback de aplicação exige schema compatível. Migration destrutiva usa expansão/contração e backup. Rollback de código nunca desfaz settlement financeiro; divergência preserva dados e bloqueia readiness.

Single Writer sacrifica paralelismo do livro por determinismo; memória reduz custo de matching por exigir rebuild; PostgreSQL síncrono limita latência/throughput por preservar atomicidade. Foram descartados locks concorrentes no livro, microserviços, Redis como fonte financeira, saga, event sourcing e broker na V1. Kafka somente V2, com novo requisito, particionamento/ownership e ADR.

## 13. ADRs relacionados

- ADR-000: princípios, slices, hexagonal, DDD e testes.
- ADR-001: stack e acesso a dados.
- ADR-002: Channel, ordering e rebuild.
- ADR-003: settlement relacional atômico.
- ADR-004: ownership e advisory lock do Single Writer.
- ADR-005: controllers, versionamento e observabilidade.
