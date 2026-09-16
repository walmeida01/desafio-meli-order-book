# Fluxos da V1

## Submissao, matching e settlement

```mermaid
sequenceDiagram
    autonumber
    participant C as Cliente
    participant API as Controller /api/v1/orders
    participant Q as Channel bounded 4096
    participant W as Single Writer
    participant DB as PostgreSQL
    participant M as Matching Engine
    participant S as Settlement
    participant O as Order Book

    C->>API: POST order (payload)
    API->>API: Validar payload, gerar chave interna e correlation
    API->>Q: TryWrite(command)
    alt Channel cheio
        Q-->>API: false
        API-->>C: 429 + Retry-After: 1
    else Comando admitido
        Q-->>API: true
        API-->>C: Aguarda resultado síncrono
        W->>DB: Abrir transação READ COMMITTED
        W->>DB: Reservar saldo e obter acceptedSequence
        W->>M: Processar ordem
        M->>O: Selecionar melhor contraparte
        M-->>W: Fills ordenados / residual
        W->>S: Liquidar todos os fills
        S->>DB: Trade + 4 Ledger entries + Wallets
        S->>DB: Atualizar Orders/Reservations/registro interno
        DB-->>W: Commit
        W->>O: Publicar snapshot committed
        W-->>API: OrderResult
        API-->>C: 201
    end
```

## Chave interna e falhas transacionais

```mermaid
flowchart TD
    Start[Comando recebido] --> Generated[Gerar chave UUID interna]
    Generated --> Tx[Iniciar transacao PostgreSQL]
    Tx --> Domain{Validacao de dominio e saldo}
    Domain -- Falha de negocio --> Reject[Persistir Order REJECTED\n+ resposta idempotente\nretornar 409]
    Domain -- Sucesso --> Match[Matching e settlement]
    Match --> Technical{Falha tecnica / timeout?}
    Technical -- Sim --> Rollback[Rollback integral\nsem publicar memoria]
    Rollback --> Retry{Deadlock ou serialization?}
    Retry -- Sim e tentativas < 3 --> Tx
    Retry -- Nao --> Error[Erro tecnico\nretry posterior seguro]
    Technical -- Nao --> Commit[Commit Order + Trade + Ledger\n+ Wallet + idempotencia]
    Commit --> Publish[Publicar snapshot do livro]
    Publish --> Created[201 novo aceite]
```

## Startup, recovery e readiness

```mermaid
stateDiagram-v2
    [*] --> NotReady
    NotReady --> CheckingDependencies: iniciar processo
    CheckingDependencies --> NotReady: DB/schema indisponivel
    CheckingDependencies --> AcquiringLock: dependencias validas
    AcquiringLock --> NotReady: advisory lock negado
    AcquiringLock --> Rebuilding: lock adquirido
    Rebuilding --> NotReady: divergencia ou invariantes invalidas
    Rebuilding --> Ready: rebuild validado
    Ready --> NotReady: perda de DB, lock ou recovery
    Ready --> Draining: shutdown iniciado
    Draining --> Stopped: drain <= 30s
    Draining --> Stopped: timeout / rollback comando ativo
    Stopped --> [*]
```

## Shutdown e observabilidade

```mermaid
flowchart LR
    Signal[SIGTERM / shutdown] --> False[Readiness = false]
    False --> StopAdmission[Bloquear novas admissions]
    StopAdmission --> Close[Fechar entrada do Channel]
    Close --> Drain[Drenar comandos admitidos\naté 30 segundos]
    Drain --> Active{Comando ativo?}
    Active -- Nao --> Stop[Encerrar processo]
    Active -- Sim --> Finish[Concluir e commitar\nou cancelar e rollback]
    Finish --> Stop

    Runtime[API runtime] --> Metrics[8 métricas normativas]
    Runtime --> Logs[Logs JSON redigidos]
    Runtime --> Traces[Traces HTTP → Channel → DB]
    Metrics --> Scrape[/metrics]
    Scrape --> Prom[Prometheus]
    Traces --> Alloy[Alloy OTLP]
    Logs --> Alloy
    Alloy --> Tempo[Tempo]
    Alloy --> Loki[Loki]
    Prom --> Grafana[Grafana 19924/19925]
    Tempo --> Grafana
    Loki --> Grafana
```

Exportação para Alloy, Loki e Tempo é assíncrona e não pode bloquear o caminho
financeiro. `database_batch_size` permanece sempre igual a `1`.
