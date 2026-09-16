# Guia de Testes

Este documento apresenta a estratégia de testes do desafio e indica o que cada
suíte demonstra. Os testes são separados por nível de responsabilidade: regras
de arquitetura, regras locais de domínio, integração técnica e fluxos completos
pela API.

## Visão Geral

| Suíte | Projeto | Dependências externas | Objetivo principal |
|---|---|---|---|
| Arquitetura | `tests/OrderBook.ArchitectureTests` | Nenhuma | Impedir dependências indevidas no domínio. |
| Unidade | `tests/OrderBook.UnitTests` | Nenhuma | Validar regras locais de domínio, contratos e observabilidade. |
| Integração | `tests/OrderBook.IntegrationTests` | PostgreSQL Testcontainers nos testes de banco | Validar infraestrutura, host HTTP e integração com PostgreSQL. |
| Funcional | `tests/OrderBook.FunctionalTests` | PostgreSQL Testcontainers e API em memória | Validar cenários de negócio pela API, com persistência real. |

Na versão atual, são 38 testes: 1 de arquitetura, 17 unitários, 10 de
integração e 10 funcionais.

## Desenvolvimento IA First

O projeto foi desenvolvido com apoio de IA por meio do OpenCode. A pasta
`.opencode/` reúne as configurações e agentes usados nesse processo de
desenvolvimento assistido, enquanto `AGENTS.md` define as regras de trabalho e
qualidade adotadas no repositório.

## Testes de Arquitetura

`OrderBook.ArchitectureTests` protege a direção de dependências prevista pela
arquitetura hexagonal. O teste inspeciona as referências do assembly de domínio
e confirma que ele não depende de `OrderBook.Api`, `OrderBook.Infrastructure`,
`Dapper` ou `Npgsql`.

Esse teste é rápido e não sobe serviços. Seu propósito é evitar que uma regra
de negócio passe a conhecer detalhes de HTTP ou PostgreSQL por acidente.

## Testes Unitários

`OrderBook.UnitTests` executa somente código em memória. Não abre conexões nem
inicia containers. Ele cobre, de forma isolada:

- Canonicalização do payload de ordem e hash determinístico.
- Matching com múltiplos fills, prioridade de preço/tempo e self-trade.
- Liquidação: exatamente quatro lançamentos contábeis por trade e limite de
  1.000 fills por comando.
- Detecção de trade duplicado, incluindo diferença de conteúdo ou ledger
  incompleto.
- Contrato de bootstrap do domínio.
- Observabilidade: metadados do serviço, redaction de dados sensíveis, nomes
  normativos das métricas, tag `side` com `BUY` e `SELL` e nome do trace source.

Esses testes localizam falhas em regras pequenas com feedback rápido, antes de
envolver HTTP, filas ou banco de dados.

## Testes de Integração

`OrderBook.IntegrationTests` valida componentes que dependem da infraestrutura
ou da composição do host.

O grupo de smoke tests verifica que:

- Liveness responde.
- Mutação é recusada enquanto a API não está pronta.
- `/metrics` permanece fora da rota versionada.
- Swagger UI referencia o documento OpenAPI versionado.
- O contrato OpenAPI expõe os schemas, summaries e rotas esperados, sem expor
  `Idempotency-Key` no `POST /api/v1/orders`.

O teste da fila confirma a capacidade de 4.096 comandos, a rejeição do próximo
comando e o encerramento controlado das submissões pendentes.

Os testes PostgreSQL sobem um container efêmero e validam:

- Aplicação repetível de migration e seed, sem duplicar carteiras.
- Constraints do banco contra saldo negativo.
- Exclusividade do advisory lock PostgreSQL.
- Segurança de chamadas concorrentes de verificação do ownership do writer na
  mesma conexão dedicada.

## Testes Funcionais

`OrderBook.FunctionalTests` é a principal evidência de comportamento para o
avaliador. Ele sobe PostgreSQL real via Testcontainers e hospeda a API com
`WebApplicationFactory`. Cada cenário chama as rotas públicas e depois observa
o resultado por endpoints de consulta. SQL direto é usado apenas para auditar
invariantes persistidas, como reservas, contagens e lançamentos de ledger.

Antes de cada cenário, o fixture interrompe a API, limpa o estado transacional,
reinicia a aplicação, aguarda `/api/v1/ready` e só então executa o fluxo. Isso
mantém os cenários independentes e reproduzíveis.

Os cenários cobertos são:

| Cenário | Evidência verificada |
|---|---|
| Reserva BUY | Bloqueia BRL correto e mantém a ordem aberta. |
| Reserva SELL | Bloqueia a quantidade correta de Vibranium. |
| Saldo insuficiente | Retorna `409`, persiste ordem `REJECTED` e não cria reserva, trade ou ledger. |
| Submissões repetidas | Cada `POST` cria uma ordem distinta, pois a API gera a chave interna por submissão. |
| Preço maker | Trade compatível é executado no preço da ordem que já estava no livro. |
| FIFO preço-tempo | Makers no mesmo preço são consumidas por ordem de aceite. |
| Fill parcial | Mantém o residual e a reserva correspondente. |
| Múltiplos fills | Uma taker consome múltiplas makers e cada trade possui quatro lançamentos. |
| Self-trade | O mesmo usuário pode cruzar sua própria ordem, com liquidação auditável. |
| Settlement e conservação | Conserva BRL e Vibranium, libera reservas e grava os quatro efeitos contábeis esperados. |

## Como Executar

Pré-requisitos:

- .NET SDK 10.
- Docker em execução, necessário para os containers PostgreSQL dos testes de
  integração e funcionais.

Para executar tudo:

```bash
dotnet test OrderBook.sln --no-restore
```

Para executar uma suíte específica:

```bash
dotnet test tests/OrderBook.ArchitectureTests/OrderBook.ArchitectureTests.csproj --no-restore
dotnet test tests/OrderBook.UnitTests/OrderBook.UnitTests.csproj --no-restore
dotnet test tests/OrderBook.IntegrationTests/OrderBook.IntegrationTests.csproj --no-restore
dotnet test tests/OrderBook.FunctionalTests/OrderBook.FunctionalTests.csproj --no-restore
```

O sucesso das suítes não substitui testes de carga. Os cenários de performance
e capacidade são tratados separadamente pelos scripts K6 em `benchmarks/`.
