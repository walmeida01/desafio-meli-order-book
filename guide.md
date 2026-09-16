# Meli Order Book V1

## Comandos locais

O `Makefile` centraliza os comandos de desenvolvimento. Execute `make help` para
listar todos os alvos disponíveis.

### Aplicação e componentes

```bls -ash
make up-d
make status
make smoke
make logs
```

O processo só fica pronto depois de migration, PostgreSQL, advisory lock e recovery.
`/api/v1/health` é liveness; `/api/v1/ready` é readiness. Sem PostgreSQL, mutations e queries retornam `503`.

Para encerrar os componentes:

```bash
make down
```

Para remover também os volumes persistidos do PostgreSQL e do Tempo:

```bash
make down-clean
```

### Testes

```bash
make restore
make build
make test
```

Os testes de integração usam PostgreSQL real via Testcontainers quando Docker está disponível.

Também é possível executar cada grupo separadamente com `make test-unit`,
`make test-integration` ou `make test-functional`.

### Benchmark K6

```bash
make k6-smoke
make k6-sustained RATE=10 DURATION=30s
make k6-burst RATE=100 BURST_MULTIPLIER=2
```

Os cenários aguardam `/api/v1/ready`, coletam `/metrics` e observam o drain. O
cenário smoke valida o contrato (criação, replay, conflito, BUY/SELL e métricas);
nenhum cenário declara capacidade de 5.000 requests/s ou trades/s sem relatório
reproduzível.


📈 Order Book & Financial Settlement Engine
Módulo central de Livro de Ofertas e Liquidação Financeira (Settlement) focado em alta consistência, idempotência e auditoria contábil. O sistema gerencia o ciclo de vida completo de ordens de compra e venda do par Vibranium / BRL, garantindo a conservação de ativos e consistência em ambiente PostgreSQL real.

🎯 Visão Geral do Sistema
A API implementa a execução e liquidação de ordens financeiras com garantia de consistência estrita através dos seguintes pilares:

Reserva Preventiva de Saldo: Bloqueio e liberação imediata de ativos (BRL para compras, Vibranium para vendas) para evitar double spending.

Motor de Matching Determinístico: Casamento de ofertas priorizando Preço/Tempo (FIFO) e garantindo o preço do Maker.

Contabilidade de Partidas Dobradas (Ledger): Liquidação auditável onde cada Trade gera exatamente 4 lançamentos contábeis.

Garantia de Idempotência: Chaves únicas por operação que previnem duplicação de ordens ou alterações inconsistentes de payload.

⚙️ Regras de Negócio e Fluxos Operacionais
1. Gestão de Carteira e Reservas (BDD-01, BDD-02, BDD-03)
Antes de entrar no livro de ofertas, o saldo do usuário é validado e reservado no banco de dados.

Ordem BUY: Reserva o limite total em BRL (preço * quantidade).

Ordem SELL: Reserva o montante em Vibranium.

Saldo Insuficiente: A ordem é rejeitada com status HTTP 409 Conflict, gravada com status REJECTED, e nenhuma alteração (Reservation, Trade ou Ledger) é criada.

2. Idempotência (BDD-04, BDD-05)
Todas as operações de escrita aceitam o cabeçalho Idempotency-Key para garantir que retentivas de rede sejam seguras.

Replay de requisição: O reenvio do mesmo payload com a mesma chave retorna HTTP 200 OK e reaproveita o orderId original sem duplicar mutações no banco.

Conflito de Payload: Usar a mesma chave com um payload diferente aciona HTTP 409 Conflict e bloqueia a operação.

3. Motor de Matching e Livro de Ofertas (BDD-06 a BDD-10)
O motor processa ordens Taker contra ordens Maker que estão na pedra (resting).

   [Taker Order] ───► [ Matcher Engine ] ───► Preço do Maker
                             │
            ┌────────────────┴────────────────┐
            ▼                                 ▼
   [ Execução Parcial ]               [ Múltiplos Fills ]
   PARTIALLY_FILLED                   1 Trade por Fill (FIFO)
Preço do Executante (Price Priority): O valor da transação é fixado sempre no preço do Maker (ordem que já estava no livro).

Prioridade Temporal (FIFO): Ordens Maker no mesmo patamar de preço são consumidas em ordem cronológica de chegada.

Execução Parcial (Partial Fills): Se a ordem Taker for maior que a Maker, ela assume o status PARTIALLY_FILLED, mantendo o saldo restante (remaining) no livro para novos casamentos.

Self-Trade: A API aceita cruzamento de ordens do mesmo usuário (BUY vs SELL própria), processando e liquidando com lançamento de livro normal.

4. Liquidação e Conservação de Ativos (BDD-09, BDD-11)
O processo de liquidação (settlement) garante que nenhum centavo de BRL ou fração de Vibranium seja criado ou destruído sem rastreamento contábil.

Regra dos 4 Registros de Ledger
Cada transação (Trade) confirmada gera exatamente 4 entradas no Ledger em uma única transação atômica:

Débito BRL da carteira do Comprador (liberando o saldo reservado).

Crédito BRL na carteira do Vendedor.

Débito Vibranium da carteira do Vendedor (liberando o ativo reservado).

Crédito Vibranium na carteira do Comprador.

Princípio de Conservação: A soma de todo o BRL e todo o Vibranium no sistema permanece constante antes e depois da liquidação de cada Trade.