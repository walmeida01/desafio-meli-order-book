# Meli Order Book — Agent Instructions

Este repositório é desenvolvido com auxílio de agentes de IA através do OpenCode.

Este arquivo estabelece as regras globais de governança, arquitetura, qualidade e execução que todos os agentes devem respeitar durante o desenvolvimento do MVP do Livro de Ofertas (Order Book).

O objetivo é permitir desenvolvimento assistido por IA sem permitir que decisões arquiteturais, requisitos ou invariantes sejam alterados silenciosamente durante a implementação.

---

# 1. Source of Truth

A ordem de autoridade dos artefatos do projeto é estrita:

1. `docs/001-requisitos.md`
2. ADRs aceitos em `docs/adr/`
3. `docs/002-arquitetura.md`
4. `docs/004-sdd.md`
5. `docs/003-plano.md`
6. Código existente no repositório

O arquivo `AGENTS.md` define as regras globais de governança e execução e deve ser respeitado por todos os agentes.

## Conflitos

Quando houver conflito entre documentos:

- não resolver silenciosamente;
- não reinterpretar requisitos por conta própria;
- não alterar decisões arquiteturais durante BUILD;
- não adaptar silenciosamente o código para contornar o conflito;
- identificar os documentos conflitantes;
- sinalizar explicitamente o problema.

Quando o conflito impedir uma implementação segura, retornar:

```text
STATUS: IMPLEMENTATION_BLOCKED
Motivo: [descrição objetiva do conflito ou lacuna]
Artefatos Afetados: [arquivos/documentos envolvidos]
Impacto: [por que a implementação não pode prosseguir com segurança]
Ação Sugerida: [decisão ou esclarecimento necessário]