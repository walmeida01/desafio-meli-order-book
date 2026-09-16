# ADR-004 — Ownership e advisory lock do Single Writer

- **Status:** Accepted
- **Data:** 2026-09-15

## Contexto

O livro em memória e a atribuição de acceptedSequence exigem uma única autoridade. O processo pode ser reiniciado e uma configuração acidental com duas instâncias não pode produzir split-brain financeiro ou ordering divergente.

## Decisão

O writer adquire `pg_try_advisory_lock` em uma conexão PostgreSQL dedicada e mantida aberta. A chave do lock identifica o Order Book V1. Somente o holder pode processar mutations, consumir o Channel e publicar estado. A instância que não obtém o lock fica não-ready e rejeita mutations; não há modo degradado nem segundo mecanismo de fencing na V1. Perda da sessão/conexão implica perda de ownership, readiness falsa e interrupção do processamento até encerramento/recovery seguro.

O lock é adquirido antes do rebuild e da abertura de readiness. Queries podem continuar conforme disponibilidade, mas nunca concedem autoridade para mutation. A V1 opera com uma instância writer; failover automático, múltiplos livros e particionamento exigem novo requisito e ADR.

## Alternativas consideradas

Lock distribuído externo, eleição em broker, lease em Redis e múltiplos writers com locks por ordem adicionariam dependências, expiração/fencing e cenários de recuperação sem necessidade demonstrada no MVP. Um lock apenas em memória não protege dois processos.

## Consequências

PostgreSQL já é dependência financeira e fornece exclusão simples, observável e liberada automaticamente com a sessão. Indisponibilidade do banco impede startup/mutations; o desenho não oferece HA horizontal V1. A prontidão precisa expor holder/perda do lock e nunca pode ser inferida apenas por processo vivo.

## Critérios de aceitação

Duas instâncias concorrentes não processam mutations simultaneamente; a sem lock retorna 503/não-ready; perda da conexão derruba readiness; lock é obtido antes de rebuild/ready; restart com lock válido reconstrói o livro e só então admite comandos; testes de integração cobrem aquisição, concorrência e perda da sessão.
