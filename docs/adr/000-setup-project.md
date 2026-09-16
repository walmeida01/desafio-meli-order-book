# ADR-000 — Princípios arquiteturais do projeto

- **Status:** Accepted
- **Data:** 2026-09-15

## Contexto

O MVP exige Order Book concorrente, consistência financeira, rastreabilidade, testabilidade e evolução sem overengineering.

## Decisão

Adotar **Modular Monolith**, **Vertical Slice Architecture**, **Hexagonal Architecture / Ports & Adapters** e **DDD tático**. Features e casos de uso são autocontidos; portas ficam nas slices e adapters nas fronteiras. O domínio não depende de ASP.NET Core, Dapper, Npgsql ou PostgreSQL.

O domínio usa somente Entities, Value Objects, Aggregates, Domain Services/Events quando necessários e linguagem ubíqua. Não haverá `GenericRepository<T>`, `BaseService` ou `BaseController` sem justificativa específica.

## Testes

xUnit com padrão AAA, FluentAssertions para asserções, Testcontainers para integração PostgreSQL e K6 para carga. Testes de concorrência, recovery, invariantes e benchmarks são parte do aceite.

## Consequências

A organização por feature aumenta coesão e testabilidade, mas exige disciplina de boundaries e pode duplicar tipos pequenos. O monólito evita transações distribuídas na V1. A escala horizontal futura exige nova decisão.

## Critérios de aceitação

Todas as funcionalidades seguem slices; regras de negócio são independentes da infraestrutura; dependências apontam para dentro; testes usam xUnit/AAA; não existem abstrações genéricas sem benefício demonstrado.
