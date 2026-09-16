# Guia Simples de Uso da API

Este guia apresenta dois fluxos curtos: criar uma compra e observar o BRL
bloqueado; depois criar uma venda compativel e observar o credito ao vendedor.

## Preparacao

Para executar os exemplos com os resultados exatos abaixo, reinicie a base
local antes de comecar:

```bash
make down-clean && make up-d
```

Espere a API ficar pronta:

```bash
curl -i http://localhost:8080/api/v1/ready
```

A resposta deve ser `200 OK`.

## Dados Usados

| Papel | userId | Saldo inicial |
|---|---|---|
| Comprador | `00000000-0000-0000-0000-000000000001` | R$ 1.000.000,00 e 100000 Vibranium |
| Vendedor | `00000000-0000-0000-0000-000000000002` | R$ 1.000.000,00 e 100000 Vibranium |

Os valores em BRL usam centavos: R$ 1.500,00 e enviado como `150000` em
`priceBrlCents`.

Cada chamada ao `POST /api/v1/orders` cria uma nova submissão. A API gera a
chave interna necessária para persistir o resultado; não envie `Idempotency-Key`.

## Como o Processamento Funciona

Nao existe uma rota para consultar a fila. O `POST /api/v1/orders` aguarda o
Single Writer processar a ordem e devolve o resultado. Em seguida,
`GET /api/v1/orders/{orderId}` confirma o estado persistido da ordem.

Os status possiveis sao:

| Status | Significado |
|---|---|
| `OPEN` | Ordem processada, com saldo reservado, aguardando contraparte |
| `PARTIALLY_FILLED` | Parte da ordem foi executada; existe saldo reservado restante |
| `FILLED` | Ordem executada por completo; nao ha reserva restante |
| `REJECTED` | Ordem rejeitada, por exemplo por saldo insuficiente |

## Cenario 1: Compra de Vibranium

### 1. Consultar saldo inicial do comprador

```bash
curl -s http://localhost:8080/api/v1/wallets/00000000-0000-0000-0000-000000000001
```

Resultado esperado: `200 OK` com saldo disponivel e bloqueado separados.

```json
{
  "brlAvailable": 100000000,
  "brlLocked": 0,
  "vibraniumAvailable": 100000,
  "vibraniumLocked": 0
}
```

### 2. Enviar ordem de compra

```bash
curl -i -X POST http://localhost:8080/api/v1/orders \
  -H 'Content-Type: application/json' \
  --data '{
    "userId": "00000000-0000-0000-0000-000000000001",
    "side": "BUY",
    "priceBrlCents": 150000,
    "quantity": 1
  }'
```

Resultado esperado: `201 Created`. Como ainda nao ha venda compativel, a
resposta possui `status: "OPEN"`, `executedQuantity: 0` e
`remainingQuantity: 1`. Guarde o `orderId` retornado.

### 3. Confirmar processamento da compra

```bash
curl -s http://localhost:8080/api/v1/orders/{orderIdDaCompra}
```

Resultado esperado: `200 OK` e `status: "OPEN"`. Isso confirma que a ordem
saiu da fila, foi persistida e esta no livro aguardando uma venda.

### 4. Consultar o livro de ofertas

```bash
curl -s http://localhost:8080/api/v1/order-book
```

Resultado esperado: `200 OK`. A resposta contem a compra aberta com o
`orderId` retornado, `side: "BUY"`, `priceBrlCents: 150000` e
`remainingQuantity: 1`.

### 5. Consultar saldo apos a compra

```bash
curl -s http://localhost:8080/api/v1/wallets/00000000-0000-0000-0000-000000000001
```

Resultado esperado: `brlAvailable` igual a `99850000` e `brlLocked` igual a
`150000`. Os R$ 1.500,00 estao reservados para a ordem aberta; nao houve trade
ainda.

## Cenario 2: Venda de Vibranium

### 1. Consultar saldo inicial do vendedor

```bash
curl -s http://localhost:8080/api/v1/wallets/00000000-0000-0000-0000-000000000002
```

Resultado esperado: `200 OK`, com `vibraniumAvailable: 100000` e
`vibraniumLocked: 0`.

### 2. Enviar ordem de venda compativel

```bash
curl -i -X POST http://localhost:8080/api/v1/orders \
  -H 'Content-Type: application/json' \
  --data '{
    "userId": "00000000-0000-0000-0000-000000000002",
    "side": "SELL",
    "priceBrlCents": 150000,
    "quantity": 1
  }'
```

Resultado esperado: `201 Created`. A venda encontra a compra aberta do cenario
anterior, portanto a resposta possui `status: "FILLED"`,
`executedQuantity: 1` e `remainingQuantity: 0`. Guarde o `orderId` retornado.

### 3. Confirmar processamento da venda

```bash
curl -s http://localhost:8080/api/v1/orders/{orderIdDaVenda}
```

Resultado esperado: `200 OK` e `status: "FILLED"`. A compra pendente tambem
foi executada no mesmo matching.

### 4. Consultar o livro de ofertas apos a venda

```bash
curl -s http://localhost:8080/api/v1/order-book
```

Resultado esperado: `200 OK`. A compra e a venda nao aparecem mais no livro,
pois ambas foram executadas por completo.

### 5. Consultar historico de negocios

```bash
curl -s 'http://localhost:8080/api/v1/trades?limit=50'
```

Resultado esperado: `200 OK`, com `items` contendo o negocio de quantidade 1
e `priceBrlCents: 150000`. Cada item tambem informa `tradeId`, `takerOrderId`,
`makerOrderId`, `acceptedSequence` e `fillOrdinal`.

### 6. Consultar saldo apos a venda

```bash
curl -s http://localhost:8080/api/v1/wallets/00000000-0000-0000-0000-000000000002
```

Resultado esperado:

```json
{
  "brlAvailable": 100150000,
  "brlLocked": 0,
  "vibraniumAvailable": 99999,
  "vibraniumLocked": 0
}
```

O vendedor recebeu R$ 1.500,00 e vendeu 1 Vibranium. Como a venda foi
completamente executada, nenhum Vibranium permanece bloqueado.

## Rotas Usadas no Fluxo

| Objetivo | Rota |
|---|---|
| Verificar que a API esta pronta | `GET /api/v1/ready` |
| Abrir a documentacao interativa | `GET /swagger/index.html` |
| Consultar saldo da carteira | `GET /api/v1/wallets/{userId}` |
| Criar compra ou venda | `POST /api/v1/orders` |
| Confirmar processamento da ordem | `GET /api/v1/orders/{orderId}` |
| Consultar ordens abertas | `GET /api/v1/order-book` |
| Consultar historico de negocios | `GET /api/v1/trades?limit=50` |
