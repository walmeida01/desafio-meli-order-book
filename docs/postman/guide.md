# Postman Collection

## Pré-requisitos

Para executar todos os asserts do fluxo guiado, reinicie a base local para os
saldos seedados e aguarde PostgreSQL, migrations, advisory lock e recovery:

```bash
make down-clean && make up-d
```

A API ficará disponível em `http://localhost:8080`.

## Importar a collection

1. Abra o Postman.
2. Selecione **Import**.
3. Escolha `docs/postman/orderbook-collection.json`.
4. Abra a collection **Meli Order Book V1 - Fluxo Simples**.
5. Confirme a variável `baseUrl`, cujo padrão é `http://localhost:8080`.

## Executar

Execute as pastas na ordem:

1. `00 - Verificar API`
2. `01 - Compra de Vibranium`
3. `02 - Venda de Vibranium`

Os scripts da collection atualizam automaticamente `orderIdCompra`,
`orderIdVenda` e `tradeId`, usados para confirmar o processamento, o livro de
ofertas e o historico de negocios.

Os IDs de usuário estão definidos como variáveis da collection. Cada execução
cria novas ordens; o fluxo não usa `Idempotency-Key`.

## Variáveis principais

| Variável | Padrão | Uso |
|---|---|---|
| `baseUrl` | `http://localhost:8080` | endereço da API |
| `userIdComprador` | UUID do seed | usuário da ordem BUY |
| `userIdVendedor` | UUID do seed | usuário da ordem SELL |
| `orderIdCompra` | preenchido automaticamente | confirmação da ordem BUY |
| `orderIdVenda` | preenchido automaticamente | confirmação da ordem SELL |
| `tradeId` | preenchido automaticamente | confirmação no histórico |

## Resultados esperados

- `/api/v1/ready`: `200`.
- Compra criada e aberta: `201`, com BRL bloqueado.
- Venda compatível executada: `201`, com status `FILLED`.
- Consulta de carteira e ordem: `200`.
- Livro de ofertas: mostra a BUY aberta e remove as ordens após o matching.
- Histórico de negócios: mostra o trade de 1 Vibranium por R$ 1.500,00.

O passo a passo completo e os valores esperados de cada saldo estao em
`docs/guia-uso-api.md`.

Se `/api/v1/ready` permanecer em `503`, verifique os logs do serviço `api` e o estado do PostgreSQL:

```bash
docker compose ps
docker compose logs api
docker compose logs postgres
```
