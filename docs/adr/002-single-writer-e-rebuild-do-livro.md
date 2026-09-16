# ADR-002 — Single writer e reconstrução do Order Book

- **Status:** Accepted
- **Data:** 2026-09-14

## Contexto

Price-time priority precisa ser determinística sob requests concorrentes. O MVP tem um único ativo e não exige escala horizontal imediata.

## Decisão

Usar `System.Threading.Channels.Channel<T>` bounded, configurado com `SingleReader = true` e múltiplos writers, e um único consumidor por processo como autoridade lógica do livro. Atribuir `acceptedSequence` no consumidor, persistir ordens e reconstruir filas no restart a partir de ordens abertas ordenadas por essa sequência. O estado em memória só é atualizado após commit. A vazão e a contenção da fila devem ser comprovadas sob concorrência de produtores HTTP.

## Alternativas

Lock global em estruturas compartilhadas; vários writers com locks por nível; broker/particionamento distribuído. As duas primeiras tornam ordering e rollback mais difíceis; a última adiciona operação e falhas sem requisito demonstrado.

## Consequências

Corretude e determinismo ficam simples, mas o throughput de um instrumento tem teto de um writer, a fila pode rejeitar com 429 e o processo pode sofrer alocações/GC sob alta taxa. Escala futura exige particionar por instrumento com lease/fencing e novo ADR. O uso de structs ou `ObjectPool<T>` para objetos do hot path só pode ser adotado após profiling, sem compartilhar estado mutável entre operações.

## Critérios de aceitação

Testes concorrentes produzem uma única sequência; `Channel<T>` sustenta a vazão-alvo no benchmark ou documenta seu limite; fila cheia não perde comando e retorna 429; restart reproduz exatamente o livro; readiness permanece falsa durante rebuild.
