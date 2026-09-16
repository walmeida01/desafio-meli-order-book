# language: pt
Funcionalidade: Fluxos financeiros essenciais do livro de ofertas
  Como consumidor da API do Order Book
  Quero validar os fluxos committed do MVP contra PostgreSQL real
  Para garantir reservas, matching, idempotência e settlement auditável

  Cenário: BDD-01 reservar BRL para uma ordem BUY
    Dado que a API está pronta e o seed determinístico foi aplicado
    Quando envio pela API uma ordem BUY com body {userId,side,priceBrlCents,quantity}
    Então a resposta HTTP deve ser 201 e a wallet deve mostrar BRL bloqueado pelo limite

  Cenário: BDD-02 reservar Vibranium para uma ordem SELL
    Dado que a API está pronta e o seed determinístico foi aplicado
    Quando envio pela API uma ordem SELL com body {userId,side,priceBrlCents,quantity}
    Então a resposta HTTP deve ser 201 e a wallet deve mostrar Vibranium bloqueado pela quantidade

  Cenário: BDD-03 rejeitar ordem quando o saldo é insuficiente
    Dado que a API está pronta e o seed determinístico foi aplicado
    Quando envio uma ordem válida sem saldo suficiente
    Então a resposta HTTP deve ser 409 e a Order persistida deve estar REJECTED
    E não devem existir Reservation, Trade ou Ledger para a tentativa

  Cenário: BDD-04 repetir uma ordem idêntica sem duplicação
    Dado que a API está pronta e o seed determinístico foi aplicado
    Quando envio duas vezes o mesmo body com a mesma Idempotency-Key
    Então as respostas HTTP devem ser 201 e depois 200 com o mesmo orderId
    E as contagens committed não devem aumentar no replay

  Cenário: BDD-05 rejeitar payload divergente para a mesma chave
    Dado que uma ordem foi aceita com uma Idempotency-Key
    Quando reutilizo a chave com payload diferente
    Então a resposta HTTP deve ser 409 e nenhuma mutation nova deve existir

  Cenário: BDD-06 executar ordens compatíveis pelo preço da maker
    Dado que existe uma ordem maker resting no livro
    Quando envio uma ordem compatível pela API
    Então a resposta deve conter Trade com preço igual ao da maker

  Cenário: BDD-07 respeitar FIFO price-time
    Dado que duas makers do mesmo lado e preço foram aceitas em sequência
    Quando envio uma taker que consome ambas
    Então a primeira maker aceita deve ser consumida primeiro

  Cenário: BDD-08 executar parcialmente uma ordem
    Dado que a quantidade da maker é menor que a quantidade da taker
    Quando envio a taker compatível pela API
    Então a resposta deve mostrar PARTIALLY_FILLED e remaining positivo

  Cenário: BDD-09 executar uma ordem em múltiplos fills
    Dado que existem várias makers compatíveis
    Quando envio uma taker que consome todas
    Então deve existir um Trade por fill, em ordem determinística
    E cada Trade deve possuir exatamente quatro Ledger entries

  Cenário: BDD-10 permitir self-trade
    Dado que um usuário possui uma SELL resting
    Quando o mesmo usuário envia uma BUY compatível
    Então a operação deve ser aceita e gerar Trade normal com quatro efeitos

  Cenário: BDD-11 liquidar e conservar BRL e Vibranium
    Dado que capturei as wallets antes de ordens compatíveis
    Quando o settlement é confirmado pela API
    Então BRL e Vibranium devem ser conservados antes e depois
    E cada Trade deve possuir exatamente quatro Ledger entries
