# ADR-003 — Settlement transacional relacional

- **Status:** Accepted
- **Data:** 2026-09-15

## Contexto

Um comando pode alterar duas wallets, reservas, ordens, trades e ledger em múltiplos fills. Falha intermediária não pode criar/destruir saldo nem aplicar trade duas vezes.

## Decisão

O comando e todos os seus fills usam uma única transação PostgreSQL síncrona, com `READ COMMITTED`, timeout default de 2 segundos, Dapper sobre Npgsql, `SELECT ... FOR UPDATE` e locks de wallets distintos ordenados por userId. Apenas deadlock/serialization failure pode ser repetido, no máximo 3 vezes. Batch é otimização interna da mesma transação; group commit é somente comportamento a medir do PostgreSQL.

BUY reserva `quantity × limitPrice` em BRL; SELL reserva quantity em Vibranium. Fill de `q` a `p` captura `q×p` BRL e `q` Vibranium, credita Vibranium ao buyer e BRL ao seller, e libera ao buyer a diferença entre limite reservado e preço maker. O residual locked é liberado ao completar a ordem. Exceder 1.000 fills por comando ou quantity máxima configurável de 1.000.000.000 causa rollback integral.

Cada Trade tem quatro Ledger entries auditáveis: débito BRL buyer, crédito Vibranium buyer, débito Vibranium seller e crédito BRL seller. Buyer=seller continua gerando os quatro efeitos. Chaves únicas tornam retry/duplicate trade no-op seguro ou conflito; o livro só publica após commit.

## Alternativas

Saga, serviços separados, débito em memória com persistência posterior, Outbox, Kafka e event sourcing ampliariam inconsistência e recovery. Kafka e processamento assíncrono ficam restritos à evolução V2.

## Consequências

Atomicidade, locks, retry idempotente e auditoria são fortes, mas PostgreSQL fica no caminho crítico e transações grandes podem limitar trades/s. O benchmark mede WAL/fsync, flush, locks, batch interno, group commit e contenção sem quebrar a atomicidade.

## Critérios de aceitação

Falha/timeout antes do commit não altera saldos; todos os fills comitam juntos; retry após commit retorna resposta persistida; ledger tem quatro efeitos balanceados inclusive self-trade; constraints impedem negativos e duplicação; Testcontainers cobre locks, rollback, timeout e duplicate trade.
