# 001 — Requisitos

## 1. Contexto

O Mercado Livre deseja disponibilizar um Livro de Ofertas (Order Book)
no qual usuários possam enviar ordens de compra e venda de ativos.

Como MVP, existe um único ativo negociável:

- Vibranium.

A negociação ocorre contra valores em Reais (BRL).

Cada usuário possui uma Wallet que representa pelo menos:

- saldo em BRL;
- quantidade de Vibranium.

O foco do desafio é:

- funcionamento correto do Order Book;
- créditos e débitos;
- performance;
- concorrência;
- escala;
- resiliência;
- observabilidade;
- rastreabilidade.

---

# 2. Escopo funcional confirmado

## RF01 — Enviar ordem BUY

O sistema deve permitir que um usuário envie uma ordem de compra de Vibranium.

## RF02 — Enviar ordem SELL

O sistema deve permitir que um usuário envie uma ordem de venda de Vibranium.

## RF03 — Manter Order Book

Ordens que não forem imediatamente executadas devem permanecer no Livro de Ofertas.

## RF04 — Executar negociações

Ordens compatíveis devem produzir compra/venda de Vibranium.

## RF05 — Crédito do comprador

Quando uma compra for efetivada:

- Vibranium deve ser creditado ao comprador.

## RF06 — Débito do comprador

Quando uma compra for efetivada:

- o valor correspondente em BRL deve ser debitado do comprador.

## RF07 — Débito do vendedor

Quando uma venda for efetivada:

- Vibranium deve ser debitado do vendedor.

## RF08 — Crédito do vendedor

Quando uma venda for efetivada:

- BRL correspondente deve ser creditado ao vendedor.

## RF09 — Valores em negociação

O sistema deve distinguir valores já efetivamente negociados de valores
associados a ordens ainda em negociação.

## RF10 — Histórico

As transações devem possuir rastreabilidade.

Deve ser possível obter histórico dos negócios realizados.

## RF11 — API

A solução deve disponibilizar API através da qual os avaliadores possam
realizar ordens de compra e venda.

A API de negócio deve usar controllers tradicionais derivados de
`ControllerBase`, com versionamento no prefixo `/api/v1`. O endpoint de
submissão é `POST /api/v1/orders` e os demais endpoints de negócio também
devem usar o mesmo prefixo. `/metrics` é endpoint técnico e permanece sem
versionamento. `Program` permanece como composition root, responsável por DI
e pelo pipeline.

## RF12 — Validação dos saldos

Ao final de uma sequência de operações os saldos devem permanecer corretos.

---

# 3. Requisitos não funcionais confirmados

## RNF01 — Escala

A solução deve ser capaz de escalar para pelo menos:

5.000 requests por segundo.

O enunciado também menciona expectativa inicial na ordem de
5.000 trades por segundo.

A arquitetura deve tratar ambos como importante requisito de capacidade
e documentar claramente o cenário de benchmark adotado.

## RNF02 — Alta concorrência

Muitos usuários e robôs podem enviar ordens simultaneamente.

A arquitetura deve tratar explicitamente:

- concorrência;
- ordering;
- sincronização;
- consistência.

## RNF03 — Performance

O caminho de ingestão e matching deve possuir baixa sobrecarga compatível
com a meta de throughput.

## RNF04 — Rastreabilidade

Transações precisam possuir histórico auditável.

## RNF05 — Resiliência

Deve existir comportamento conhecido diante da falha dos componentes.

## RNF06 — Observabilidade

A solução deve possuir capacidade de diagnóstico e observação operacional.

## RNF07 — Testabilidade

O avaliador deve conseguir subir e testar a solução facilmente.

## RNF08 — Evolução

A solução deve considerar crescimento futuro sem exigir que toda
infraestrutura futura seja implementada no MVP.

---

# 4. Fora de escopo confirmado

Não é necessário implementar:

- interface gráfica;
- backoffice;
- autenticação;
- credenciais;
- cadastro completo de usuários.

O desafio é um MVP.

Evitar overengineering.

---

# 5. Decisões arquiteturais do MVP

O PDF não define explicitamente o algoritmo de matching nem o modelo de
reserva de saldos. Para manter o MVP simples e permitir uma interpretação
consistente do Order Book, as decisões mínimas adotadas são H01 a H05.

H06 e H07 permanecem como propriedades e opções de implementação a serem
avaliadas pelo `meli-arch`, sem exigir infraestrutura distribuída ou
overengineering.

## H01 — Price-Time Priority

Decisão do MVP:

- maior preço BUY possui prioridade;
- menor preço SELL possui prioridade;
- em mesmo preço, ordem mais antiga possui prioridade.

Essa prioridade será formalizada e aplicada pelo Matching Engine.

## H02 — LIMIT orders

Decisão do MVP:

o MVP trabalha somente com ordens limitadas por preço.

## H03 — Partial Fill

Uma ordem poderá ser parcialmente executada quando não houver quantidade
suficiente disponível na contraparte.

## H04 — Multiple Fills

Uma única ordem poderá produzir múltiplos Trades ao consumir várias ordens
compatíveis do lado oposto.

## H05 — Saldo disponível e bloqueado

Para distinguir valores efetivados de valores ainda em trade, a Wallet do MVP
deve separar saldo disponível de saldo bloqueado:

- available;
- locked.

O bloqueio deve impedir que o mesmo saldo financie mais de uma ordem.

## H06 — Matching determinístico

Como propriedade desejável para testes e rastreabilidade, a mesma sequência de
ordens deve produzir a mesma sequência de resultados.

## H07 — Single Writer

Opção de implementação:

uma única autoridade lógica altera determinado Order Book, serializando as
alterações do livro sem impedir o recebimento concorrente de requisições.

Deve ser comparado com alternativas simples durante a arquitetura.

---

# 6. Invariantes candidatas

Estas invariantes devem ser validadas e refinadas pela arquitetura.

## INV-C01

Saldo disponível não pode ser negativo.

## INV-C02

Saldo bloqueado não pode ser negativo.

## INV-C03

O mesmo saldo não pode financiar duas ordens simultaneamente.

## INV-C04

Quantidade restante de uma ordem não pode ser negativa.

## INV-C05

Uma ordem não pode ser executada acima de sua quantidade original.

## INV-C06

Um Trade não pode alterar saldos duas vezes.

## INV-C07

Uma requisição idempotente não deve criar ordens duplicadas.

## INV-C08

Não devem existir criação ou destruição acidental de BRL ou Vibranium durante trades.

---

# 7. Casos críticos para arquitetura

A arquitetura deve definir o comportamento esperado para os seguintes casos.
No MVP, isso não implica implementar recuperação distribuída; implica
preservar os saldos, evitar duplicidade e permitir recuperação segura após
falhas.

## Casos de corretude

1. duas ordens chegando simultaneamente;
2. múltiplas ordens para o mesmo usuário;
3. saldo insuficiente;
4. execução parcial;
5. uma ordem consumindo diversas contrapartes;
6. requisição repetida ou processamento duplicado de Trade.

## Casos de falha e recuperação

7. timeout após o aceite de uma ordem;
8. falha ou reinício da aplicação durante o processamento ou settlement;
9. banco indisponível, rejeitando a operação sem alterar saldos;
10. reconstrução do Order Book após o reinício da aplicação.

---

# 8. Observabilidade esperada

A arquitetura deve definir pelo menos:

- structured logging;
- correlation identifiers;
- métricas de HTTP;
- métricas do Matching Engine;
- quantidade de ordens;
- quantidade de trades;
- erros;
- latency;
- queue depth quando aplicável;
- health;
- readiness.

A instrumentação deve usar o Meter `MeliOrderBook.Metrics`, OpenTelemetry
Metrics, exporter Prometheus e dashboards Grafana 19924/19925. As métricas
normativas são exatamente:

- `orders_received_total`;
- `orderbook_queue_depth`;
- `orderbook_queue_rejected_total`;
- `orders_processed_total`, com tag `status`;
- `trades_executed_total`;
- `matching_duration_seconds`;
- `database_batch_flush_duration_seconds`;
- `database_batch_size`.

Não adicionar métricas normativas alternativas ou aliases. A V1 mede a
transação PostgreSQL individual e registra `database_batch_size = 1`; não há
group commit nem batching de aplicação.

---

# 9. Testes esperados

A estratégia deve considerar:

- unit tests;
- integration tests;
- invariant/property tests;
- concurrency tests;
- recovery tests;
- performance/load tests.

---

# 10. Performance

A solução deverá possuir benchmark reproduzível.

O relatório deverá informar quando possível:

- requests/s;
- trades/s;
- p50;
- p95;
- p99;
- error rate;
- cenário;
- volume;
- hardware/ambiente.

---

# 11. Restrições de projeto

Priorizar:

1. corretude;
2. consistência;
3. determinismo;
4. simplicidade;
5. performance;
6. escalabilidade.

Não sacrificar consistência financeira para atingir throughput.

Cada classe ou record deve existir em seu próprio arquivo.

---

# 12. Open Questions

Devem ser decididas na fase de arquitetura:

- Qual algoritmo de matching será utilizado?
- Qual será a definição exata de prioridade?
- Qual será a semântica do preço de execução?
- Como será realizada a reserva de saldo?
- Quem possui ownership da Wallet?
- Quem possui ownership do Order Book?
- Qual será a estratégia de persistência?
- Como o Order Book será reconstruído?
- Como garantir idempotência?
- Como Trade e Settlement serão acoplados?
- Existe necessidade real de mensageria externa?
- Qual será o limite transacional?
- Qual será a estratégia de backpressure?
- Qual plataforma será utilizada: .NET ou Java?
- Qual acesso a dados será utilizado?
