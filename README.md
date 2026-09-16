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
