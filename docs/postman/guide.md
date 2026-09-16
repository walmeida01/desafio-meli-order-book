# Postman Collection

## Pré-requisitos

Suba a aplicação e aguarde o PostgreSQL, migrations, advisory lock e recovery:

```bash
docker compose up --build
```

A API ficará disponível em `http://localhost:8080`.

## Importar a collection

1. Abra o Postman.
2. Selecione **Import**.
3. Escolha `docs/postman/orderbook-collection.json`.
4. Abra a collection **Meli Order Book V1**.
5. Confirme a variável `baseUrl`, cujo padrão é `http://localhost:8080`.

## Executar

Execute as pastas na ordem:

1. `01 - Health`
2. `02 - Orders`
3. `03 - Queries`
4. `04 - Observability`

Os scripts da collection atualizam automaticamente:

- `orderId` após a criação da primeira ordem;
- `cursor` após a consulta paginada de trades.

As chaves de idempotência e os IDs de usuário estão definidos como variáveis da collection. Para repetir o cenário do zero, altere `buyKey` e `sellKey` ou use valores diferentes.

## Variáveis principais

| Variável | Padrão | Uso |
|---|---|---|
| `baseUrl` | `http://localhost:8080` | endereço da API |
| `userIdBuy` | UUID do seed | usuário da ordem BUY |
| `userIdSell` | UUID do seed | usuário da ordem SELL |
| `orderId` | preenchido automaticamente | consulta de ordem |
| `buyKey` | `demo-buy-001` | idempotência da BUY |
| `sellKey` | `demo-sell-001` | idempotência da SELL |
| `cursor` | preenchido automaticamente | paginação de trades |

## Resultados esperados

- `/api/v1/health`: `200`.
- `/api/v1/ready`: `200` quando a aplicação estiver pronta; `503` durante a inicialização.
- Nova ordem: `201`.
- Replay idêntico: `200`.
- Conflito de idempotência: `409`.
- Payload inválido: `400`.
- Consultas: `200`, `404` ou `503`, conforme estado e dados existentes.
- Cursor ou limite inválido: `400` quando a aplicação estiver pronta.
- `/metrics`: `200` com métricas customizadas e de runtime.

## Observabilidade

O endpoint `/metrics` expõe o formato Prometheus. A stack também utiliza OpenTelemetry para exportar traces e logs via Alloy.

- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3000`
- Alloy: `http://localhost:12345`

Se `/api/v1/ready` permanecer em `503`, verifique os logs do serviço `api` e o estado do PostgreSQL:

```bash
docker compose ps
docker compose logs api
docker compose logs postgres
```
