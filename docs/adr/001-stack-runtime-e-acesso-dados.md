# ADR-001 — Stack de runtime e acesso a dados

- **Status:** Accepted
- **Data:** 2026-09-15

## Contexto

O MVP precisa de API enxuta, concorrência de entrada e transação financeira relacional auditável sem distribuir componentes.

## Decisão

Usar **.NET 10**, **C#**, **ASP.NET Core  APIs**, **PostgreSQL** e **Dapper sobre Npgsql**, com migrations versionadas. SQL explícito, locks e transações permanecem visíveis no caminho financeiro. Dapper sobre Npgsql é o único padrão de acesso a dados da V1.

## Consequências

Dapper reduz mapeamento repetitivo sem ocultar o comportamento transacional. A operação assume PostgreSQL, migrations e profiling de allocations/GC, locks, batch e group commit. A V1 permanece síncrona; Kafka fica restrito à evolução assíncrona V2.

## Critérios de aceitação

Aplicação e testes sobem por comandos documentados; integração cobre locks, constraints e rollback; benchmark registra versões, PostgreSQL hot path, allocations/GC, contenção do `Channel<T>` e batch/group commit com Dapper/Npgsql.
