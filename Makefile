SOLUTION ?= OrderBook.sln
COMPOSE ?= docker compose
BASE_URL ?= http://localhost:8080
K6 ?= k6
K6_MODE ?= auto
K6_IMAGE ?= grafana/k6:latest
K6_DOCKER_NETWORK ?= host
RESULT_DIR ?= benchmarks/results
RESULT_FILE ?=
K6_RESULT_TIMESTAMP := $(shell date -u +%Y%m%dT%H%M%SZ)
HOST_UID := $(shell id -u)
HOST_GID := $(shell id -g)

RATE ?= 10
DURATION ?= 30s
WARMUP ?= 10s
COOLDOWN ?= 10s
BURST_MULTIPLIER ?= 2
USER_POOL_SIZE ?= 2
USER_IDS ?=
PRE_ALLOCATED_VUS ?= 20
MAX_VUS ?= 100
METRICS_RETRIES ?= 2
METRICS_RETRY_DELAY ?= 0.1

.DEFAULT_GOAL := help

.PHONY: help restore build test test-unit test-integration test-functional clean \
	compose-check up up-d down down-clean logs status smoke k6-smoke \
	k6-sustained k6-burst

define run-k6
	@case "$(K6_MODE)" in \
		local) \
			if ! command -v "$(K6)" >/dev/null 2>&1; then \
				printf '%s\n' "K6_MODE=local, mas o executável '$(K6)' não foi encontrado." >&2; \
				exit 1; \
			fi; \
			"$(K6)" run $(1);; \
		docker) \
			if ! command -v docker >/dev/null 2>&1; then \
				printf '%s\n' "K6_MODE=docker, mas o comando docker não foi encontrado." >&2; \
				exit 1; \
			fi; \
			docker run --rm --user "$(HOST_UID):$(HOST_GID)" --network "$(K6_DOCKER_NETWORK)" -v "$(CURDIR):/work:ro" -v "$(CURDIR)/$(RESULT_DIR):/work/$(RESULT_DIR):rw" -w /work "$(K6_IMAGE)" run $(1);; \
		auto) \
			if command -v "$(K6)" >/dev/null 2>&1; then \
				"$(K6)" run $(1); \
			elif command -v docker >/dev/null 2>&1; then \
				printf '%s\n' "K6 não encontrado; executando via Docker ($(K6_IMAGE))."; \
			docker run --rm --user "$(HOST_UID):$(HOST_GID)" --network "$(K6_DOCKER_NETWORK)" -v "$(CURDIR):/work:ro" -v "$(CURDIR)/$(RESULT_DIR):/work/$(RESULT_DIR):rw" -w /work "$(K6_IMAGE)" run $(1); \
			else \
				printf '%s\n' "K6 não encontrado e Docker não está disponível." >&2; \
				exit 1; \
			fi;; \
		*) \
			printf '%s\n' "K6_MODE inválido: $(K6_MODE). Use auto, local ou docker." >&2; \
			exit 1;; \
	esac
endef

define result-file
$(RESULT_DIR)/$(if $(RESULT_FILE),$(RESULT_FILE),$(1)-$(K6_RESULT_TIMESTAMP).json)
endef

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

k6-smoke: ## Executa o smoke test K6 e salva um resumo JSON
	@mkdir -p "$(RESULT_DIR)"
	@printf '%s\n' "Resumo K6: $(call result-file,smoke)"
	$(call run-k6,--summary-export "$(call result-file,smoke)" -e BASE_URL="$(BASE_URL)" -e USER_POOL_SIZE="$(USER_POOL_SIZE)" -e USER_IDS="$(USER_IDS)" -e METRICS_RETRIES="$(METRICS_RETRIES)" -e METRICS_RETRY_DELAY="$(METRICS_RETRY_DELAY)" k6/smoke.js)

k6-sustained: ## Executa o benchmark sustentado K6 e salva um resumo JSON
	@mkdir -p "$(RESULT_DIR)"
	@printf '%s\n' "Resumo K6: $(call result-file,sustained)"
	$(call run-k6,--summary-export "$(call result-file,sustained)" -e BASE_URL="$(BASE_URL)" -e RATE="$(RATE)" -e DURATION="$(DURATION)" -e WARMUP="$(WARMUP)" -e COOLDOWN="$(COOLDOWN)" -e USER_POOL_SIZE="$(USER_POOL_SIZE)" -e USER_IDS="$(USER_IDS)" -e PRE_ALLOCATED_VUS="$(PRE_ALLOCATED_VUS)" -e MAX_VUS="$(MAX_VUS)" -e METRICS_RETRIES="$(METRICS_RETRIES)" -e METRICS_RETRY_DELAY="$(METRICS_RETRY_DELAY)" k6/sustained.js)

k6-burst: ## Executa o benchmark burst/stress K6 e salva um resumo JSON
	@mkdir -p "$(RESULT_DIR)"
	@printf '%s\n' "Resumo K6: $(call result-file,burst)"
	$(call run-k6,--summary-export "$(call result-file,burst)" -e BASE_URL="$(BASE_URL)" -e RATE="$(RATE)" -e DURATION="$(DURATION)" -e BURST_MULTIPLIER="$(BURST_MULTIPLIER)" -e USER_POOL_SIZE="$(USER_POOL_SIZE)" -e USER_IDS="$(USER_IDS)" -e PRE_ALLOCATED_VUS="$(PRE_ALLOCATED_VUS)" -e MAX_VUS="$(MAX_VUS)" -e METRICS_RETRIES="$(METRICS_RETRIES)" -e METRICS_RETRY_DELAY="$(METRICS_RETRY_DELAY)" k6/burst.js)
