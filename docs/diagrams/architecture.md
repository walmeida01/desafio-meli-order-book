# Arquitetura da V1

## Visão Geral

A V1 é estruturada como um monólito modular de alta consistência. A API recebe as ordens, valida o formato e as enfileira; um processador exclusivo (**Single Writer**) consome essa fila sequencialmente para garantir execução determinística sem contenção de concorrência.

O processador executa o **matching** (casamento entre ordens compatíveis no livro) e o **settlement** (liquidação financeira com atualização de saldos). Toda a operação é persistida dentro de uma transação atômica no PostgreSQL. Para evitar *dirty states*, o **snapshot do livro em memória só é atualizado estritamente após o commit** bem-sucedido no banco.

```mermaid
flowchart LR
    Cliente["Cliente"] --> API["API<br/>valida e admite"]
    API --> Fila["Fila de comandos<br/>Channel limitado"]
    Fila --> Writer["Processador único<br/>Single Writer"]
    Writer --> Caso["Casos de uso<br/>matching e settlement"]
    Caso --> Dominio["Domínio<br/>ordens, livro e carteiras"]
    Caso --> Portas["Portas da aplicação"]
    Portas --> Adaptador["Adaptador PostgreSQL<br/>Dapper / Npgsql"]
    Adaptador --> Banco[("PostgreSQL<br/>estado confirmado")]
    Banco -->|"commit"| Writer
    Writer -->|"após o commit"| Livro["Snapshot do livro<br/>em memória"]

    Ownership["Ownership do processo<br/>advisory lock"] -. autoriza .-> Writer

    classDef fluxo fill:#e8f1ff,stroke:#2563eb,stroke-width:2px;
    classDef dominio fill:#eaf7ea,stroke:#2f855a,stroke-width:2px;
    classDef ownership fill:#fff3cd,stroke:#b7791f,stroke-width:2px;
    classDef banco fill:#f3e8ff,stroke:#7c3aed,stroke-width:2px;

    class Cliente,API,Fila,Writer,Caso,Portas,Adaptador,Livro fluxo;
    class Dominio dominio;
    class Ownership ownership;
    class Banco banco;
```

O cliente envia uma ordem via API, que realiza a validação de formato/payload e enfileira o comando. Um processador exclusivo (**Single Writer**) consome essa fila sequencialmente, garantindo execução determinística sem contenção de concorrência. Ele executa o matching (casamento entre ordens compatíveis no livro) e o settlement (liquidação financeira com atualização de saldos).

O domínio isola as regras de negócio de ordens, livro e carteiras sem dependência direta de infraestrutura, comunicando-se com a camada de persistência através de Portas implementadas por um Adaptador PostgreSQL. Toda a operação é executada dentro de uma transação atômica no banco de dados. O snapshot do livro em memória é atualizado estritamente após o commit bem-sucedido.

Para garantir alta disponibilidade sem risco de duas instâncias alterarem o livro simultaneamente, a aplicação utiliza um **Advisory Lock** — um bloqueio exclusivo gerenciado no próprio PostgreSQL. Esse mecanismo funciona como uma eleição de líder (Leader Election): apenas a instância que obtém essa trava ganha a autorização para processar mutações de estado. Em cenários de startup ou recuperação de falhas (failover), a nova instância líder assume o controle, reidrata o livro em memória lendo o estado confirmado no banco de dados e só então passa a processar novas ordens da fila.
---

## Fronteiras e Responsabilidades

A aplicação adota os princípios de **Hexagonal Architecture** combinados com **Vertical Slices**. O domínio encapsula as regras de negócio puras (ordens, livro e carteiras) e não possui dependências diretas de bibliotecas ou infraestrutura externa.

```mermaid
flowchart TB
    subgraph HTTP["HTTP / API"]
        H["Transporte, validação,<br/>status HTTP, correlação"]
    end

    subgraph Application["Aplicação / Slices verticais"]
        A1["Submissão de ordem"]
        A2["Matching"]
        A3["Settlement"]
        A4["Consultas"]
        A5["Recuperação / prontidão"]
    end

    subgraph Domain["Domínio"]
        D1["Livro de ofertas"]
        D2["Ordem"]
        D3["Carteira / reserva"]
        D4["Trade / Ledger"]
    end

    subgraph Ports["Portas"]
        P1["Repositórios"]
        P2["Unidade de trabalho"]
        P3["Ownership do writer"]
        P4["Prontidão"]
    end

    subgraph Infra["Infraestrutura"]
        I1["Dapper / Npgsql"]
        I2["PostgreSQL"]
        I3["Channel / Worker / Advisory Lock"]
        I4["OpenTelemetry / Logs"]
    end

    H --> A1
    H --> A4
    H -. telemetria .-> I4
    A1 --> D1
    A1 --> D2
    A2 --> D1
    A2 --> D2
    A3 --> D3
    A3 --> D4
    A1 --> P1
    A3 --> P1
    A3 --> P2
    A4 --> P1
    A5 --> P3
    A5 --> P4
    P1 --> I1
    P2 --> I1
    P3 --> I3
    P4 --> I3
    I1 --> I2
```

As dependências seguem rigorosamente a direção `HTTP/Aplicação -> Domínio e Portas -> Infraestrutura`. A camada de domínio é isolada e não referencia ASP.NET Core, Dapper, Npgsql ou PostgreSQL.

---

## Observabilidade

A infraestrutura de telemetria combina rastreamento distribuído (Traces), métricas e logs estruturados em um pipeline desacoplado do caminho de execução financeira.

```mermaid
flowchart LR
    Runtime["Runtime da API"] --> OTel["OpenTelemetry<br/>Meter + ActivitySource"]
    Runtime --> Scrape["/metrics<br/>scrape do Prometheus"]
    OTel --> Alloy["Grafana Alloy<br/>OTLP 4317/4318"]
    Scrape --> Prom["Prometheus"]
    Alloy -->|"logs"| Loki["Loki"]
    Alloy -->|"traces"| Tempo["Tempo"]
    Alloy -->|"métricas OTLP"| Prom
    Prom --> Grafana["Grafana<br/>dashboards"]
    Loki --> Grafana
    Tempo --> Grafana
```

O *scrape* da rota `/metrics` via Prometheus e o envio assíncrono OTLP/remote-write operam de forma distinta. As duas fontes não devem ser somadas em uma mesma consulta para evitar duplicidade de telemetria. Todo o pipeline de observabilidade é não-bloqueante em relação às transações do Order Book.
