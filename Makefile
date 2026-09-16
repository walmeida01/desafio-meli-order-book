SOLUTION ?= OrderBook.sln
COMPOSE ?= docker compose
BASE_URL ?= http://localhost:8080
K6 ?= k6

RATE ?= 10
DURATION ?= 30s
WARMUP ?= 10s
COOLDOWN ?= 10s
BURST_MULTIPLIER ?= 2

.DEFAULT_GOAL := help

.PHONY: help restore build test test-unit test-integration test-functional clean \
	compose-check up up-d down down-clean logs status smoke k6-smoke \
	k6-sustained k6-burst

help: ## Lista os comandos disponíveis
	@awk 'BEGIN {FS = ":.*##"; printf "Uso: make <alvo> [VAR=valor]\n\nAlvos:\n"} /^[a-zA-Z0-9_-]+:.*##/ {printf "  %-18s %s\n", $$1, $$2}' $(MAKEFILE_LIST)

restore: ## Restaura as dependências .NET
	dotnet restore $(SOLUTION)

build: ## Compila a solução
	dotnet build $(SOLUTION)

test: ## Executa todos os testes
	dotnet test $(SOLUTION)

test-unit: ## Executa os testes unitários
	dotnet test tests/OrderBook.UnitTests/OrderBook.UnitTests.csproj

test-integration: ## Executa os testes de integração
	dotnet test tests/OrderBook.IntegrationTests/OrderBook.IntegrationTests.csproj

test-functional: ## Executa os testes funcionais
	dotnet test tests/OrderBook.FunctionalTests/OrderBook.FunctionalTests.csproj

clean: ## Remove os artefatos de build .NET
	dotnet clean $(SOLUTION)

compose-check: ## Valida a configuração do Docker Compose
	$(COMPOSE) config --quiet

up: ## Sobe a aplicação e os componentes em foreground
	$(COMPOSE) up --build

up-d: ## Sobe a aplicação e os componentes em background
	$(COMPOSE) up --build -d

down: ## Para os componentes sem remover volumes
	$(COMPOSE) down

down-clean: ## Para os componentes e remove volumes persistidos
	$(COMPOSE) down -v

logs: ## Acompanha os logs dos componentes
	$(COMPOSE) logs -f

status: ## Mostra o status dos componentes
	$(COMPOSE) ps

smoke: ## Executa o smoke test HTTP
	BASE_URL="$(BASE_URL)" ./scripts/smoke.sh

k6-smoke: ## Executa o smoke test K6
	$(K6) run -e BASE_URL="$(BASE_URL)" k6/smoke.js

k6-sustained: ## Executa o benchmark sustentado K6
	$(K6) run -e BASE_URL="$(BASE_URL)" -e RATE="$(RATE)" -e DURATION="$(DURATION)" -e WARMUP="$(WARMUP)" -e COOLDOWN="$(COOLDOWN)" k6/sustained.js

k6-burst: ## Executa o benchmark burst/stress K6
	$(K6) run -e BASE_URL="$(BASE_URL)" -e RATE="$(RATE)" -e DURATION="$(DURATION)" -e BURST_MULTIPLIER="$(BURST_MULTIPLIER)" k6/burst.js
