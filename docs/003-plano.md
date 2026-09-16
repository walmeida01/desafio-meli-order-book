# 003 — Plano Executável de Implementação V1

> **Status:** Ready for BUILD. As unidades abaixo são o contrato de execução da V1; o agente `meli-build` deve implementar cada unidade na ordem do grafo, sem criar decisões arquiteturais novas.

## Regras de execução

- Alterar somente o escopo da unidade corrente e seus testes; preservar contratos do SDD e da arquitetura.
- Usar **Modular Monolith**, **Vertical Slice Architecture**, **Ports & Adapters/Hexagonal** e DDD tático. Cada caso de uso deve ser organizado por feature, com domínio sem dependência de ASP.NET Core, Dapper, Npgsql ou PostgreSQL.
- Stack obrigatória: .NET 10, C#, ASP.NET Core com controllers `ControllerBase`, PostgreSQL, Dapper + Npgsql, xUnit, FluentAssertions, Testcontainers, Docker Compose e K6.
- `Program` é o composition root de DI e pipeline. A regra estrutural é uma classe ou record por arquivo.
- Usar AAA nos testes. SQL deve ser explícito, parametrizado e pertencente aos adapters da slice; não criar `GenericRepository<T>`, `BaseService` ou camada genérica equivalente.
- Não alterar requisitos, ADRs, arquitetura ou SDD durante BUILD. Dúvida ou conflito bloqueante deve ser reportado, não decidido no código.
- V1 é síncrona: Single Writer, `Channel<T>` bounded 4096, Order Book em memória, uma transação PostgreSQL por comando, `READ COMMITTED`, locks ordenados por `userId`, advisory lock, retry limitado a deadlock/serialization (máximo 3), timeout de 2 s e drain de 30 s.
- Não implementar Kafka, Outbox, processamento assíncrono de negócio, múltiplos writers, escala horizontal, cancelamento, market orders, taxas, autenticação ou múltiplos ativos.

## Dependency graph

Legenda: `A → B` significa que B só pode começar após os critérios de A passarem. Uma seta representa dependência real de artefatos, contratos ou comportamento; o grafo é acíclico e está em ordem topológica.

```text
U00 Bootstrap
 └─ U01 Shared Kernel
     ├─ U02 Wallets e reservas
     └─ U03 Orders e lifecycle
         └─ U04 Order Book e Matching

U01 + U02 + U03 ──→ U05 Persistência, ports, migrations e constraints
U04 + U05 ────────→ U06 Trade, Settlement e Ledger
U05 + U06 ────────→ U07 Idempotência
U04 + U05 ────────→ U08 Advisory lock e Recovery/Rebuild
U04 + U06 + U07 + U08 ─→ U09 Single Writer e admission/backpressure
U05 + U08 ────────→ U10 Queries
U09 + U10 ────────→ U11 Contratos HTTP/ APIs
U08 + U09 + U11 ──→ U12 Observabilidade e graceful shutdown
U06 + U07 + U08 + U09 + U12 ─→ U13 Concorrência e testes de falha
U11 + U12 + U13 ──→ U14 Performance, K6 e benchmarks
U10 + U11 + U12 + U13 + U14 ─→ U15 Hardening e demo operacional
```

## Unidades de implementação

### U00 — Bootstrap compilável

#### Objetivo
Entregar somente o esqueleto compilável da Solution .NET 10, o composition root inicial, as fronteiras modulares e o ambiente local/testes.

#### Dependências
Nenhuma.

#### Escopo
Criar a Solution e projetos necessários para Modular Monolith, com organização por módulos e Vertical Slices; configurar fronteiras Ports & Adapters; configurar Dapper, Npgsql, PostgreSQL, xUnit, FluentAssertions, Testcontainers, Docker Compose, logging/metrics/tracing iniciais e build da Solution. Criar registros vazios/interfaces de infraestrutura apenas quando necessários para compilação, sem semântica de domínio.

#### Arquivos/Projetos afetados
`OrderBook.sln`; `src/OrderBook/OrderBook.Api/Program.cs`; `src/OrderBook/OrderBook.Domain/`; `src/OrderBook/OrderBook.Application/`; `src/OrderBook/OrderBook.Infrastructure/`; `src/OrderBook/OrderBook.Contracts/`; `tests/OrderBook.UnitTests/`; `tests/OrderBook.IntegrationTests/`; `tests/OrderBook.ArchitectureTests/`; `tests/OrderBook.IntegrationTests/Fixtures/`; `Dockerfile`; `compose.yaml`; `Directory.Build.props`; `Directory.Packages.props`; configuração inicial de observabilidade.

#### Entregáveis
Solution .NET 10/C# que compila; referências que preservam domínio independente; `Program` como composition root registrando configuração, DI, Dapper/Npgsql e health base; projeto de testes xUnit com FluentAssertions; fixture Testcontainers PostgreSQL; Compose com API e PostgreSQL; estrutura `Modules/<Module>/<Slice>/`, `Shared/{Domain,Runtime,Observability}`, `Http`, `database/migrations`, `database/seed` e `benchmarks`; endpoint técnico de liveness sem negócio.

#### Testes obrigatórios
`dotnet restore`; `dotnet build`; execução dos projetos xUnit; teste de arquitetura que impede dependência Domain→Infrastructure/API; teste de subida do host; fixture Testcontainers capaz de iniciar/parar PostgreSQL; smoke do Compose e health técnico.

#### Critérios de conclusão
Build limpo em ambiente novo; todos os testes passam; Compose sobe API e PostgreSQL; Dapper/Npgsql e Testcontainers estão referenciados e configurados; Ports & Adapters e Vertical Slice estão visíveis na estrutura; nenhum efeito financeiro ou regra de negócio existe.

#### Fora de escopo
Entidades financeiras, value objects, matching, reservas, settlement, schema de negócio, endpoints de ordens/queries, Channel de negócio, advisory lock e recovery.

### U01 — Shared Kernel e value objects

#### Objetivo
Implementar as representações primitivas e invariantes compartilhadas, sem dependência de infraestrutura.

#### Dependências
U00.

#### Escopo
`UserId`, `OrderId`, `TradeId`, `ReservationId`, `Side`, `Instrument`, `BrlCents`, `Quantity`, `AcceptedSequence`, `OrderStatus`, `IdempotencyKey` e operações seguras contra overflow. Usar `long`, somente Vibranium, valores inteiros positivos e limites configuráveis conforme SDD.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Domain/Shared/`; `src/OrderBook/OrderBook.Contracts/Shared/`; `tests/OrderBook.UnitTests/Shared/`; teste de arquitetura.

#### Entregáveis
Value objects imutáveis, validação determinística, mensagens/erros de domínio estáveis e contratos compartilhados sem vazamento de tipos de banco ou HTTP.

#### Testes obrigatórios
AAA com FluentAssertions para validade, limites, igualdade, serialização contratual e overflow; property tests para quantidade/preço; testes que rejeitam floating point e IDs vazios.

#### Critérios de conclusão
Nenhuma representação inválida pode alcançar uma operação de domínio; todos os limites do SDD estão cobertos; os projetos compilam sem dependência de ASP.NET, Dapper ou Npgsql.

#### Fora de escopo
Wallet, Order, matching, persistência, API e regras de reserva.

### U02 — Wallets e reservas

#### Objetivo
Modelar Wallet, Reservation e as regras de available/locked necessárias para impedir double spending.

#### Dependências
U01.

#### Escopo
BUY reserva `quantity × limitPrice` em BRL; SELL reserva `quantity` de Vibranium; implementar capture, liberação de excedente pelo preço maker, liberação residual e rollback de domínio. `available` e `locked` nunca podem ser negativos.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Domain/Modules/Wallets/`; `src/OrderBook/OrderBook.Application/Modules/Wallets/ReserveFunds/`; portas da slice; `tests/OrderBook.UnitTests/Modules/Wallets/`.

#### Entregáveis
Aggregates/entidades e contratos `ReserveFunds`, `Capture`, `Release` e `Rollback`; resultado explícito de insuficiência; reserva única associável a uma ordem.

#### Testes obrigatórios
AAA para BUY/SELL, insuficiência, partial fill, capture, liberação do excedente, rollback, limites e conservação local; cenários buyer=seller sem netting prematuro.

#### Critérios de conclusão
INV-C01, INV-C02 e INV-C03 são satisfeitas no domínio; operações inválidas falham sem mutação parcial; portas não dependem de banco.

#### Fora de escopo
`SELECT FOR UPDATE`, schema, transação PostgreSQL, HTTP e settlement completo.

### U03 — Orders e lifecycle

#### Objetivo
Implementar o aggregate Order e suas transições exatas da V1.

#### Dependências
U01.

#### Escopo
LIMIT BUY/SELL, original/remaining, fills, `QUEUED` transitório, estados persistíveis `OPEN`, `PARTIALLY_FILLED`, `FILLED`, `REJECTED`, acceptedSequence apenas em ordens aceitas e gaps permitidos.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Domain/Modules/Orders/`; `src/OrderBook/OrderBook.Application/Modules/Orders/CreateOrder/`; contratos e portas da slice; testes unitários.

#### Entregáveis
Factory/aggregate, lifecycle, aplicação de fill, resultado de aceite/rejeição e contratos de `CreateOrder` sem persistência.

#### Testes obrigatórios
Transições válidas e inválidas, full/partial/multiple fill, remaining/original, sequence ausente em rejeição, limite de quantidade e invariantes C04/C05.

#### Critérios de conclusão
Nenhuma ordem excede quantidade original; não há cancelamento nem retorno de estado; a unidade pode ser testada isoladamente.

#### Fora de escopo
Matching, reserva, SQL, idempotência e endpoint.

### U04 — Order Book e Matching Engine

#### Objetivo
Implementar o livro em memória e o matching determinístico price-time.

#### Dependências
U01, U03.

#### Escopo
Single instrument; BUY por preço decrescente e SELL por preço crescente; FIFO por acceptedSequence; crossing por preço limite; maker price; múltiplos/partial fills; residual no livro; self-trade permitido; snapshot imutável.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Domain/Modules/Matching/`; `src/OrderBook/OrderBook.Application/Modules/Matching/ProcessOrder/`; contratos `MatchingResult`; testes unitários/property.

#### Entregáveis
`OrderBook` com ownership explícito, `MatchingEngine`, filas por preço, seleção da melhor contraparte e resultado ordenado de fills.

#### Testes obrigatórios
Prioridade, empate FIFO, crossing BUY/SELL, maker price, residual, partial/multiple, remoção, self-trade, determinismo e property tests de quantidade.

#### Critérios de conclusão
Mesma sequência produz os mesmos fills; nenhuma operação altera Wallet; nenhum fill supera remaining; snapshot não permite mutação externa.

#### Fora de escopo
Persistência, settlement, concorrência entre writers, Kafka e API.

### U05 — Persistência, ports, migrations e constraints

#### Objetivo
Disponibilizar os adapters PostgreSQL e o schema relacional que protege os invariantes.

#### Dependências
U01, U02, U03.

#### Escopo
Dapper sobre Npgsql, SQL parametrizado, `IOrderRepository`, `IWalletRepository`, `IReservationRepository`, `ITradeRepository`, `ILedgerRepository`, `IOrderSequence`, `IUnitOfWork`, `IIdempotencyStore`, `IReadiness`; migrations para tabelas/sequence/índices/FK/CHECK/unique; seed reproduzível; `READ COMMITTED`, timeout 2 s, `FOR UPDATE` e locks de Wallet ordenados por userId.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Application/Ports/`; `src/OrderBook/OrderBook.Infrastructure/Postgres/`; `src/OrderBook/OrderBook.Infrastructure/Postgres/Sql/`; `database/migrations/`; `database/seed/`; testes de integração.

#### Entregáveis
Schema de `wallets`, `orders`, `reservations`, `trades`, `ledger_entries`, `idempotency_records` e `accepted_order_sequence`; adapters concretos e Unit of Work transacional; configuração de conexão e migrations.

#### Testes obrigatórios
Testcontainers para migration/seed, CHECK/FK/unique, sequence com gaps, commit/rollback, timeout, isolamento READ COMMITTED, lock ordering e SQL parametrizado.

#### Critérios de conclusão
Persistência implementa exatamente os contratos do SDD; nenhum ORM alternativo; constraints rejeitam saldos/quantidades inválidos e duplicatas; transação pode ser aberta e desfeita de forma verificável.

#### Fora de escopo
Endpoints, matching, advisory lock, queries finais e broker.

### U06 — Trade, Settlement e Ledger

#### Objetivo
Aplicar todos os fills de um comando atomicamente e produzir auditoria financeira.

#### Dependências
U02, U03, U04, U05.

#### Escopo
Trade imutável, quatro Ledger entries por fill, locks de Wallet distintos em ordem crescente, captura de reservas, crédito/débito, atualização de Order/Reservation/Wallet e maker price. Buyer=seller mantém os quatro efeitos. Limite de 1.000 fills por comando.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Domain/Modules/Settlement/`; `src/OrderBook/OrderBook.Application/Modules/Settlement/SettleTrade/`; adapters Postgres correspondentes; testes unitários/integrados.

#### Entregáveis
`SettlementCommand`, `Trade`, `LedgerEntry`, handler transacional e resultado com todos os trades; duplicate Trade idêntico somente como no-op quando Trade e os quatro efeitos já estiverem completos, duplicate divergente como 409 conflict com rollback e sem `REJECTED`, e nenhum efeito parcial.

#### Testes obrigatórios
Conservação BRL/Vibranium, quatro efeitos, self-trade, partial/multiple, atomicidade, rollback, rejeição persistida por saldo insuficiente, duplicate completo idêntico sem reaplicação, duplicate divergente com 409/rollback sem nova Order, locks ordenados, timeout e constraints em Testcontainers.

#### Critérios de conclusão
Nenhum efeito financeiro parcial, duplicado ou acima da reserva; commit contém todos os fills; rejeição de domínio persiste `Order=REJECTED` e resposta idempotente sem Reservation/Trade/Ledger; falha técnica integral deixa banco e memória sem publicação de mutation.

#### Fora de escopo
Saga, Outbox, Kafka, settlement assíncrono e cancelamento.

### U07 — Idempotência de comandos

#### Objetivo
Persistir o resultado de cada submissão junto de seus efeitos financeiros.

#### Dependências
U05, U06.

#### Escopo
Canonicalização do payload, hash, chave interna por submissão e registro de status/body, tudo na mesma transação. A API gera uma nova chave UUID para cada chamada.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Application/Modules/Orders/SubmitOrder/Idempotency/`; `src/OrderBook/OrderBook.Infrastructure/Postgres/Idempotency/`; testes.

#### Entregáveis
Porta/adapters para o registro interno, canonicalizer e integração com `OrderResult` persistido. O registro, a Order e os efeitos devem compartilhar a transação do comando.

#### Testes obrigatórios
Chave interna única, canonicalização, persistência de aceite e rejeição, concorrência, crash simulado pós-commit e constraint de chave primária.

#### Critérios de conclusão
INV-C07 passa sob corrida; cada submissão persiste uma única Order/Trade/reserva e seu resultado; a chave interna não é exposta pela API.

#### Fora de escopo
Idempotência de GET, mensagens duráveis e deduplicação distribuída.

### U08 — Advisory lock e Recovery/Rebuild

#### Objetivo
Estabelecer ownership de processo e reconstruir o Order Book somente de estado committed.

#### Dependências
U04, U05.

#### Escopo
Conexão PostgreSQL dedicada com `pg_try_advisory_lock`; readiness falsa sem lock; startup não-ready; migrations/schema check; carga de Orders `OPEN/PARTIALLY_FILLED` por acceptedSequence; validação de reservas, remaining, quantidades, referências, conservação e divergências.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Application/Modules/Matching/RebuildOrderBook/`; `src/OrderBook/OrderBook.Infrastructure/Postgres/WriterOwnership/`; `src/OrderBook/OrderBook/Shared/Runtime/Readiness`; testes de recovery.

#### Entregáveis
`IWriterOwnership`, `IReadiness`, acquire/release/loss handling, `RebuildOrderBook` e diagnóstico de divergência sem reparo silencioso.

#### Testes obrigatórios
Duas conexões concorrentes, perda da sessão, restart, gaps de sequence, rebuild determinístico, reservation/wallet/Trade/Ledger inconsistente, schema/banco indisponível e readiness antes/depois da recuperação. A suíte deve provar que não há reparo silencioso.

#### Critérios de conclusão
Somente o holder pode prosseguir para mutation; perda do lock interrompe admission/reader e impede publicação; divergência mantém readiness falsa e dados intactos; snapshot reconstruído equivale ao committed state.

#### Fora de escopo
Failover automático, múltiplos writers, escala horizontal e reparo automático.

### U09 — Single Writer, admission e backpressure

#### Objetivo
Compor submissão síncrona, reader único e processamento ordenado sem perder comandos admitidos.

#### Dependências
U04, U05, U06, U07, U08.

#### Escopo
`Channel<SubmitOrderCommand>` bounded 4096, `SingleReader=true`, múltiplos writers, `TryWrite`, 429 + `Retry-After`, reader único, validação de readiness, timeout 2 s, retry até 3 somente para deadlock/serialization, publicação do livro somente após commit.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Application/Modules/Orders/SubmitOrder/`; `src/OrderBook/OrderBook/Shared/Runtime/CommandChannel`; composition root; testes.

#### Entregáveis
Admission service, processor, cancellation/timeout policy, integração `SubmitOrderCommand`→matching→settlement→idempotência e contratos de resultado/erro. O Channel não descarta itens admitidos e o shutdown sinaliza fechamento antes do drain.

#### Testes obrigatórios
Fila cheia, múltiplos writers, ordem do reader, não descarte após `TryWrite`, não-ready 503, rollback em timeout/limite, retry limitado, publicação pós-commit, shutdown com comando ativo e drain máximo de 30 s.

#### Critérios de conclusão
Há exatamente um reader autorizado; backpressure é observável e não corrompe comandos; nenhum estado de memória fica à frente do commit; fechamento impede novas admissões; V1 continua síncrona.

#### Fora de escopo
Kafka, Outbox, consumers adicionais, group commit como contrato e processamento assíncrono de negócio.

### U10 — Query slices

#### Objetivo
Disponibilizar leituras determinísticas exclusivamente sobre estado committed.

#### Dependências
U05, U08.

#### Escopo
`GetOrderBook`, `GetTrades` com cursor/limit, `GetOrder` e `GetWallet`; mapeamentos read-only, ordenação por preço/sequence/tradeId e snapshot do livro.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Application/Modules/Queries/{GetOrderBook,GetTrades,GetOrder,GetWallet}/`; SQL read adapters; contratos; testes.

#### Entregáveis
Ports e handlers de query sem autoridade de mutation, DTOs internos e paginação validada.

#### Testes obrigatórios
Integração com Testcontainers, ordenação, paginação, 404, snapshot imutável, ausência de registros não committed e isolamento de queries contra writer.

#### Critérios de conclusão
RF03/RF10 são consultáveis; queries não reservam, não atribuem sequence e não alteram estado.

#### Fora de escopo
Mutations, autenticação, backoffice e consistência de leitura além do committed state.

### U11 — Contratos HTTP e  APIs

#### Objetivo
Publicar a API externa exatamente conforme SDD, sem regra financeira nos endpoints.

#### Dependências
U09, U10.

#### Escopo
Controllers tradicionais `ControllerBase` e contratos versionados em `/api/v1`: `POST /api/v1/orders`, `GET /api/v1/order-book`, `GET /api/v1/trades`, `GET /api/v1/orders/{id}`, `GET /api/v1/wallets/{userId}`, `/api/v1/health`, `/api/v1/ready`, DTOs, validação, correlation ID, OpenAPI e mapeamento 201/400/404/409/429/503 com `Retry-After`. `/metrics` permanece sem versionamento.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Api/Controllers/`; `OrderBook.Contracts/`; testes de contrato/integrados.

#### Entregáveis
Controllers por slice, error envelope com código estável/correlation, serialização do `OrderResult` persistido e documentação OpenAPI. `Program` permanece fora das slices e concentra composição, DI e pipeline.

#### Testes obrigatórios
WebApplicationFactory/integração para payload inválido, BUY/SELL, submissões repetidas distintas, saldo insuficiente persistido, 429, 503, queries, cursor/limit válido e inválido, health/readiness e headers `Retry-After` e correlation.

#### Critérios de conclusão
Contratos de API do SDD passam sem acesso direto a repositórios; endpoints somente transportam, validam e delegam aos handlers; nenhum status é escolhido apenas por conveniência do endpoint.

#### Fora de escopo
UI, autenticação/autorização, cadastro, cancelamento e market order.

### U12 — Observabilidade e graceful shutdown

#### Objetivo
Tornar o runtime diagnosticável e seu encerramento previsível.

#### Dependências
U08, U09, U11.

#### Escopo
Logs JSON estruturados, correlation/request IDs, traces HTTP→Channel→command→DB, Meter `MeliOrderBook.Metrics`, OpenTelemetry Metrics, exporter Prometheus, dashboards Grafana 19924/19925, health/readiness, readiness false no shutdown e drain de até 30 s. Usar exatamente as métricas `orders_received_total`, `orderbook_queue_depth`, `orderbook_queue_rejected_total`, `orders_processed_total` com tag `status`, `trades_executed_total`, `matching_duration_seconds`, `database_batch_flush_duration_seconds` e `database_batch_size`.

#### Arquivos/Projetos afetados
`src/OrderBook/OrderBook.Infrastructure/Observability/`; `src/OrderBook/OrderBook.Api/`; runtime/shutdown; exporters; `deploy/observability/`; dashboards e runbook operacional.

#### Entregáveis
Nomes/buckets/labels conforme a lista normativa acima, redaction de Idempotency-Key e dados sensíveis, liveness/readiness completos, endpoint `/metrics` não versionado e lifecycle de drain. `database_batch_size` deve registrar 1 e `database_batch_flush_duration_seconds` deve medir a transação PostgreSQL individual; não implementar group commit ou batching.

#### Testes obrigatórios
Captura de logs/métricas/traces, labels sem cardinalidade proibida, readiness por lock/DB/schema/recovery, shutdown com readiness falsa, bloqueio de nova admission, drenagem, timeout e comando em execução.

#### Critérios de conclusão
É possível observar recebimento, profundidade e rejeições da fila, processamento por status, trades executados, matching e transação individual por coleta, além do estado ready/lock; shutdown não aceita novas mutations e não transforma comando não committed em aceito.

#### Fora de escopo
SIEM, autenticação, observabilidade distribuída V2 e alteração do modelo de negócio.

### U13 — Concorrência, falhas e recovery verification

#### Objetivo
Provar as invariantes e os casos críticos sob carga concorrente e falhas controladas.

#### Dependências
U06, U07, U08, U09, U12.

#### Escopo
Testes de mesmo usuário, múltiplos writers HTTP, ordering, deadlock/serialization retry, banco indisponível, crash antes/depois commit, restart/rebuild, divergência, shutdown, self-trade e conservação.

Como parte pequena e funcional da unidade, incluir uma suíte BDD contra a API real no projeto separado `tests/OrderBook.FunctionalTests`. Os cenários serão mantidos em Gherkin em português como documentação e terão execução efetiva por testes xUnit equivalentes, usando `WebApplicationFactory`, `HttpClient` e PostgreSQL Testcontainers. Reqnroll é opcional e somente poderá ser usado se os arquivos `.feature` forem executáveis, com geração e descoberta dos testes confirmadas no gate; SpecFlow não deve ser adicionado nem combinado com Reqnroll. A suíte não cria uma nova arquitetura nem substitui as suítes especializadas.

##### BDD funcional de alto valor

- **Objetivo:** provar, pelo contrato HTTP e pelas pós-condições committed, os fluxos financeiros essenciais do MVP com o menor conjunto de cenários representativos.
- **Dependências:** U11 e U12, além de U05–U09 para migrations, seed, settlement, idempotência, ownership e readiness; PostgreSQL real e Docker disponíveis para a execução aprovada.
- **Escopo:** `WebApplicationFactory<Program>` com `HttpClient` real; PostgreSQL real por Testcontainers; espera explícita por `GET /api/v1/ready` antes dos cenários; migrations e seed determinísticos; uma única factory/instância writer por coleção de cenários; reset/isolamento determinístico entre cenários; steps finos que somente preparam dados, chamam HTTP e observam respostas; DTOs de teste modelados como `record`.
- **Arquivos/projetos afetados:** `tests/OrderBook.FunctionalTests/Features/*.feature`; `tests/OrderBook.FunctionalTests/Features/Steps/`; `tests/OrderBook.FunctionalTests/Fixtures/ApiCollectionFixture.cs`; `tests/OrderBook.FunctionalTests/Contracts/`; configuração do projeto `tests/OrderBook.FunctionalTests/` para xUnit, FluentAssertions, Testcontainers e, opcionalmente, Reqnroll. `tests/OrderBook.IntegrationTests/` permanece reservado aos testes técnicos de integração. Não criar handlers, repositories ou acesso de infraestrutura dentro dos steps.
- **Entregáveis:** coleção/factory compartilhada com lifetime controlado, container PostgreSQL, aplicação das migrations, seed determinístico, espera por readiness, reset entre cenários, records de request/response e features em português. A consulta direta ao banco fica restrita a invariantes técnicas/auditoria (por exemplo, contagens de Orders/Reservations/Trades/Ledger, status persistido e exatamente quatro lançamentos por Trade).
- **Testes obrigatórios:** as features devem cobrir os seguintes cenários de alto valor, com Arrange/Act/Assert preservado nos steps/fixtures:
   - **BDD-01:** reserva de uma ordem BUY;
   - **BDD-02:** reserva de uma ordem SELL;
   - **BDD-03:** saldo insuficiente: HTTP 409, `Order=REJECTED` persistida e ausência de `Reservation`, `Trade` e `Ledger`;
   - **BDD-06:** BUY/SELL compatíveis: existência de `Trade` e preço igual ao da maker;
   - **BDD-07:** prioridade FIFO price-time;
   - **BDD-08:** partial fill;
   - **BDD-09:** multiple fills;
   - **BDD-10:** self-trade permitido;
   - **BDD-11:** settlement/conservação de BRL e Vibranium, incluindo os quatro efeitos de Ledger por Trade.
- **Critérios de conclusão:** cada cenário é independente, reproduzível e passa contra a API hospedada pela `WebApplicationFactory`, sem invocar handlers/repositories diretamente; a coleção usa uma única instância writer e não inicia writers paralelos; `/api/v1/ready` somente é aceito após migrations, seed, advisory lock e rebuild; os asserts usam FluentAssertions e verificam resposta HTTP, DTOs e pós-condições committed. Os contratos HTTP permanecem os do SDD: 201 para criação, 200 para replay, 400 para payload inválido, 409 para rejeição/conflicto, 429 para backpressure e 503 para não-ready/dependência indisponível, sem criar status alternativo.
- **Fora de escopo:** detalhes de value objects, locks SQL, deadlocks, crash injection, recovery profundo, shutdown interno e performance/capacidade K6. Esses comportamentos permanecem nas suítes apropriadas de U01/U05/U08/U13/U14; a BDD funcional não deve duplicá-los nem consultá-los por APIs internas.
- **Rastreabilidade:** BDD-01–BDD-11 cobrem RF01–RF12 (com ênfase nos fluxos de ordem, execução, saldos e histórico), RNF04, RNF05, RNF06 e RNF07; INV-C01–INV-C08 e INV-009, com foco em reservas, lifecycle `REJECTED`, idempotência, price-time, atomicidade, quatro efeitos e conservação; casos críticos 3–6 e 10 de `docs/001-requisitos.md`. O isolamento/readiness rastreia RNF02/RNF05; a auditoria direta rastreia RF10/RNF04. U01/U05/U08/U13/U14 continuam donos dos detalhes e falhas explicitamente fora desta suíte.

#### Arquivos/Projetos afetados
`tests/OrderBook.IntegrationTests/` (suítes técnicas existentes, incluindo os testes de concorrência e recovery quando U13 for implementada); `tests/OrderBook.FunctionalTests/Features/`; `tests/OrderBook.FunctionalTests/Features/Steps/`; `tests/OrderBook.FunctionalTests/Fixtures/ApiCollectionFixture.cs`; fixtures Testcontainers; hooks de teste do runtime. Suítes ou projetos dedicados de concorrência e recovery ainda não existem e poderão ser criados no escopo de U13, sem representar projetos presentes na Solution nesta etapa.

#### Entregáveis
Suíte repetível xUnit/AAA/FluentAssertions, property/invariant checks para C01–C08 e cenários de duas instâncias provando ausência de split-brain. A cobertura técnica de concorrência e recovery deve ser entregue como suítes em `tests/OrderBook.IntegrationTests/` ou, mediante criação durante U13, em projetos dedicados; a cobertura funcional permanece em `tests/OrderBook.FunctionalTests/`.

#### Testes obrigatórios
Todos os casos 1–10 dos requisitos; sem double spending, efeitos duplicados, race em Wallet, divergência silenciosa ou retry indevido. Os testes devem usar barreiras/sinais explícitos, não sleeps frágeis, e separar falha ambiental de falha do sistema. `tests/OrderBook.FunctionalTests` deve executar efetivamente os cenários BDD-01–BDD-11 por testes xUnit equivalentes contra HTTP real e PostgreSQL real; os `.feature` em Gherkin permanecem documentação quando Reqnroll não for adotado. A suíte funcional não pode substituir os testes técnicos de `tests/OrderBook.IntegrationTests`, concorrência, locks, crash, recovery e K6.

#### Critérios de conclusão
Suíte passa repetidamente em ambiente limpo com PostgreSQL real; BDD-01–BDD-11 passam por testes xUnit equivalentes no projeto `tests/OrderBook.FunctionalTests`; os testes técnicos existentes em `tests/OrderBook.IntegrationTests/` e as suítes técnicas de concorrência/recovery entregues por U13 também passam. Se forem criados projetos dedicados para essas duas últimas suítes, eles deverão estar incluídos na Solution e no gate de U13; sua ausência atual não reduz a cobertura exigida nem autoriza aprovar a unidade sem a verificação técnica correspondente. Se Reqnroll for utilizado, os `.feature` também devem ser executáveis e descobertos pelo xUnit, sem SpecFlow simultâneo. Qualquer falha deixa rollback/readiness/diagnóstico conforme SDD; sem Docker, o resultado fica explicitamente não executado e não aprovado; nenhum teste depende de timing não determinístico sem sincronização explícita.

#### Fora de escopo
Benchmark de capacidade, otimização prematura e failover horizontal.

### U14 — Performance, K6 e benchmarks

#### Objetivo
Medir, sem transformar hipótese em garantia, a capacidade e os gargalos da V1.

#### Dependências
U11, U12, U13.

#### Escopo
Cenários K6 sustained e burst; requests/orders/trades por segundo, p50/p95/p99, erros/429, backlog, drain, Channel, GC/allocations, locks, retries, WAL/fsync e latências observadas pelas métricas normativas; microbenchmarks do engine. A transação PostgreSQL é individual, com `database_batch_size = 1`; não há group commit nem batching.

#### Arquivos/Projetos afetados
`benchmarks/OrderBook.Load/`; `benchmarks/OrderBook.Benchmarks/`; `k6/`; `docs/benchmark.md`; dashboards/queries de métricas.

#### Entregáveis
Scripts parametrizados, smoke de carga, relatório reproduzível com commit, versões, hardware, configuração, volume, mix BUY/SELL e limites observados.

#### Testes obrigatórios
Smoke K6, cenário sustentado sem backlog crescente, burst acima da capacidade mostrando 429/drain, benchmark unitário do matching e validação dos p50/p95/p99/error rate. O relatório deve separar requests/s, orders/s e trades/s e registrar hardware, versões, volume, mix e configuração.

#### Critérios de conclusão
Relatório separa request/s de trade/s e só declara 5.000 quando medido no cenário documentado; gargalos, backlog, 429, capacidade do Channel e limitações ambientais estão registrados; benchmark não altera arquitetura nem contrato.

#### Fora de escopo
SLO garantido sem medição, escala horizontal, group commit como contrato e mudança de arquitetura para atingir números.

### U15 — Hardening e demo operacional

#### Objetivo
Entregar uma execução limpa, reproduzível e demonstrável do MVP.

#### Dependências
U10, U11, U12, U13, U14.

#### Escopo
Configuração final, validação de limites, migrations controladas, seed explícito, Docker Compose, documentação de execução/demo, readiness, restart seguro, backup/compatibilidade e checklist de release.

#### Arquivos/Projetos afetados
`compose.yaml`; `Dockerfile`; `deploy/`; `database/migrations/`; `database/seed/`; `README.md`; `docs/demo.md`; `docs/runbook.md`; scripts de smoke; `tests/OrderBook.IntegrationTests/` (testes técnicos); `tests/OrderBook.FunctionalTests/` (BDD funcional BDD-01–BDD-11).

#### Entregáveis
Comando documentado para subir, migrar, seedar, executar ordens, consultar trades/wallet/book, reiniciar e verificar recovery; checklist de rollback de aplicação sem desfazer settlement.

#### Testes obrigatórios
Clean deploy em ambiente novo, migrations/seed determinísticos, demo BUY/SELL com partial/multiple/self-trade, restart/rebuild, lock ownership, readiness real, smoke API, backup/compatibilidade documentados, execução dos testes técnicos de `tests/OrderBook.IntegrationTests` e execução dos BDD-01–BDD-11 por `tests/OrderBook.FunctionalTests`.

#### Critérios de conclusão
RF01–RF12, RNF05–RNF07 e invariantes estão demonstráveis; BDD-01–BDD-11 passam no projeto funcional separado; `OrderBook.IntegrationTests` contém somente testes técnicos; uma instância writer é a única ativa; seed/demo/restart são reproduzíveis; blockers ambientais ou de implementação permanecem explicitamente reportados e impedem declarar o gate verde.

#### Fora de escopo
Autoscaling, múltiplos writers, failover horizontal, Kafka, Outbox, processamento assíncrono de negócio e alterações destrutivas de schema sem expansão/contração.

## Gates de passagem

O agente só inicia uma unidade quando todas as dependências têm build/testes verdes. U00–U04 liberam o domínio; U05–U09 liberam persistência, settlement, idempotência e ingestão; U10–U12 liberam contratos e operação; U13 prova corretude sob falha; U14 mede capacidade; U15 fecha o MVP. Falha de invariantes, atomicidade, idempotência, advisory lock, recovery ou readiness bloqueia a unidade seguinte.

## Evolução V2 (fora do grafo e sem implementação V1)

Kafka/Outbox, processamento assíncrono, particionamento do Order Book, múltiplos writers e escala horizontal somente podem ser avaliados após novo requisito, definição de ownership/ordering/consistência e ADR aprovado. Nenhum artefato de V2 é dependência deste plano.
