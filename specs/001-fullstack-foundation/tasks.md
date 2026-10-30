---

description: "Task list for feature implementation (Incremento I1 — Backend, Persistência e Isolamento por Instalação)"
---

# Tasks: Fundação full-stack reutilizável (Recorte I1)

**Input**: Design documents from `/specs/001-fullstack-foundation/` (`plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`, `quickstart.md`)
**Prerequisites**: `plan.md` (required), `spec.md` (required), `research.md`, `data-model.md`, `contracts/openapi.json`, `contracts/operations.md`

**Tests**: Incluídos — exigidos explicitamente pela especificação (`FR-025`, `FR-026`, `FR-027`, `FR-034` e `SC-009`).

**Organization**: As tarefas estão agrupadas em fases pequenas e ordenadas por User Story para permitir implementação e validação incremental sem gerar um lote único extenso. O escopo executável deste `tasks.md` cobre integralmente o **Incremento 1 (I1)**: backend `.NET 10`, modelo/migrations de usuários e permissões no banco exclusivo da instalação, preflight/marcador `TenantDatabaseIdentity`, superfície HTTP mínima (`/health/live`, `/health/ready`, `/openapi/v1.json`), Docker Compose local e testes reais com PostgreSQL (Testcontainers). Login HTTP (`I2`), frontend React (`I3`) e automação de deploy no Dokploy/backups (`I4`) pertencem aos ciclos seguintes conforme `plan.md`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Pode ser executada em paralelo (arquivos distintos, sem dependência de tarefas incompletas)
- **[Story]**: User Story atendida pela tarefa (`[US1]` a `[US7]`)
- Todos os itens incluem o caminho exato do arquivo alvo

---

## Phase 1: Setup (Inicialização da Solution e Ferramentas)

**Purpose**: Criar a estrutura enxuta do repositório para o backend `.NET 10` com versões fixadas e ferramentas de qualidade.

- [X] T001 Criar `global.json` fixando SDK `10.0.401` e runner `Microsoft.Testing.Platform` na raiz `global.json`
- [X] T002 Criar manifesto de ferramentas locais com `dotnet-ef` `10.0.12` em `.config/dotnet-tools.json`
- [X] T003 [P] Configurar propriedades comuns de compilação (`net10.0`, `Nullable=enable`, `ImplicitUsings=enable`, `RestorePackagesWithLockFile=true`) em `Directory.Build.props`
- [X] T004 [P] Configurar gerenciamento central de versões de pacotes NuGet (`EF Core 10.0.12`, `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3`, `xunit.v3 4.0.2`, `Testcontainers.PostgreSql 4.16.0`) em `Directory.Packages.props`
- [X] T005 [P] Configurar regras de formatação e estilo verificáveis por `dotnet format` em `.editorconfig`
- [X] T006 Criar a solution `backend/Boilerplate.slnx` e os projetos `backend/src/Platform.Domain/Platform.Domain.csproj`, `backend/src/Platform.Application/Platform.Application.csproj`, `backend/src/Platform.Infrastructure/Platform.Infrastructure.csproj`, `backend/src/App.Api/App.Api.csproj`, `backend/tests/Platform.UnitTests/Platform.UnitTests.csproj`, `backend/tests/Platform.IntegrationTests/Platform.IntegrationTests.csproj` e `backend/tests/Platform.ContractTests/Platform.ContractTests.csproj`

T006 inclui o primeiro restore e a geração dos arquivos `packages.lock.json` junto de cada projeto, antes de validar restore com `--locked-mode`. Toda alteração posterior de dependências deve atualizar o lockfile correspondente na mesma tarefa; T039 apenas verifica sua consistência final.

**Checkpoint**: Solution `.slnx` compila sem erros, possui lockfiles versionados e respeita o grafo de referências entre camadas (`Domain <- Application <- Infrastructure <- App.Api`).

---

## Phase 2: Foundational (Pré-requisitos Bloqueantes da Instalação)

**Purpose**: Implementar os tipos centrais de configuração da instalação, erros padronizados (`ProblemDetails`) e abstrações base que bloqueiam as User Stories.

**⚠️ CRITICAL**: Nenhuma User Story deve ser iniciada antes da conclusão desta fase.

- [X] T007 [P] Criar enum `TenantState` (`enum (Inactive / Active) — Estado operacional configurado para a instalação.`) e exceções/resultados de domínio em `backend/src/Platform.Domain/Tenancy/TenantState.cs`
- [X] T008 [P] Implementar `InstallationTenantOptions` com validação explícita das restrições de `data-model.md` (`ApplicationId`: `string, 1 a 100 — Identidade confiável do produto derivado.`; `EnvironmentId`: `UUID — Identificador imutável do ambiente daquela instalação; não vazio.`; `TenantId`: `UUID — Identificador imutável do cliente/empresa daquela instalação; não vazio.`; `TenantDisplayName`: `string, 1 a 120 — Nome de apresentação da empresa naquela instalação.`; `TenantState`: `enum (Inactive / Active) — Estado operacional configurado para a instalação.`; `ConnectionStrings:AppDb`: `secret / arquivo privado — Conexão exclusiva ao banco PostgreSQL daquela instalação; separada entre runtime (sem DDL) e tooling (owner de migration).`) em `backend/src/Platform.Application/Configuration/InstallationTenantOptions.cs`
- [X] T009 [P] Criar contratos de porta para leitura da identidade do banco (`ITenantDatabaseIdentityProbe`) em `backend/src/Platform.Application/Tenancy/ITenantDatabaseIdentityProbe.cs`
- [X] T010 [P] Implementar fábrica e handler de respostas de erro `ProblemDetails` (`DEPENDENCY_UNAVAILABLE`, `NOT_FOUND`, `INTERNAL_ERROR`, `traceId` hexadecimal de 32 caracteres, sem expor secrets ou strings de conexão) em `backend/src/App.Api/Diagnostics/ProblemDetailsSetup.cs`
- [X] T011 Criar fixture base de PostgreSQL real (`postgres:18.6`) com Testcontainers para provisionar dois bancos isolados (Cliente A e Cliente B) com roles `owner` (migration) e `runtime` (sem DDL e sem `CONNECT` cruzado), usando alvos descartáveis próprios por suíte sem compartilhar estado entre execuções paralelas, em `backend/tests/Platform.IntegrationTests/Fixtures/PostgresInstallationFixture.cs`

**Checkpoint**: Fundação pronta — cada entrega de User Story abaixo pode ser executada e validada em lotes pequenos.

**Evidência da Parada 1 (2026-10-28)**: SDK 10.0.401, `dotnet-ef` 10.0.12 restaurado,
restore `--locked-mode`, build com zero avisos/erros e `dotnet format --verify-no-changes`
aprovados. MTP: 20 testes aprovados (12 unitários, 5 de diagnóstico/contrato e 3 da fixture
com PostgreSQL real), sem falhas ou ignorados. Grafo das quatro camadas e sete lockfiles
verificados. Fase 3 não iniciada; esta evidência não declara SC-009 concluído.

---

## Phase 3: User Story 2 - Modelo de Usuários e Permissões por Instalação (Priority: P1) 🎯 MVP Core (SC-009)

**Goal**: Definir o modelo persistível de usuários e permissões (`AppUser`, `UserPermission`) e seus mapeamentos locais. Contexto, migration e comprovação de persistência nos bancos A/B são entregues na Phase 4.

**Independent Test**: Compilar entidades/mapeamentos e executar T012 para validar UUIDs e limites de campos, sem exigir contexto, migration ou banco inicializado nesta fase.

### Tests for User Story 2 ⚠️

- [X] T012 [P] [US2] Criar testes unitários de validação das invariantes de `AppUser` e `UserPermission` (UUID não vazio, tamanho de `DisplayName` e `PermissionCode`; unicidade e FKs são verificadas em PostgreSQL por T013) em `backend/tests/Platform.UnitTests/Access/UserAccessModelTests.cs`

### Implementation for User Story 2

- [X] T014 [P] [US2] Implementar entidade `AppUser` herdando `IdentityUser<Guid>` com todas as restrições de `data-model.md` (`Id`: `UUID — PK local, não vazio.`; `TenantId`: `UUID — FK para TenantDatabaseIdentity.TenantId; impede conta de outro tenant nesse banco.`; `Email / NormalizedEmail`: `string, até 254 — Email válido; índice único normalizado neste banco.`; `UserName / NormalizedUserName`: `campos Identity — Derivados do email pela política inicial, normalização nativa.`; `DisplayName`: `string, 1 a 120 — Obrigatório.`; `IsEnabled`: `booleano — Conta desativada não pode acessar; ciclo local ao tenant.`; `PasswordHash`: `campo Identity — Hash nativo; nunca senha pura, resposta ou log.`; `SecurityStamp / ConcurrencyStamp`: `campos Identity — Invalidação e concorrência por conta local.`; `LockoutEnd / AccessFailedCount`: `campos Identity — Estrutura inicial; I2 aplica 5 falhas/15 minutos somente à conta local.`) em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Entities/AppUser.cs`
- [X] T015 [P] [US2] Implementar entidade `UserPermission` com todas as restrições de `data-model.md` (`UserId`: `UUID — Parte da PK; FK AppUser local.`; `PermissionCode`: `string, 1 a 128 — Parte da PK; código declarado pela aplicação.`; `GrantedAt`: `instante UTC — Obrigatório.`; `Version`: `inteiro 64 bits — Concorrência.`) em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Entities/UserPermission.cs`
- [X] T016 [US2] Configurar mapeamentos EF Core de `AppUser` e `UserPermission` no schema `access` (índice único em `NormalizedEmail`, PK composta `(UserId, PermissionCode)`, `timestamptz` UTC e token de concorrência `Version`) em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Configurations/AccessModelConfiguration.cs`

**Checkpoint**: Entidades, mapeamentos e invariantes unitárias definidos sem tabela global de usuários. Ainda não comprova persistência, isolamento ou SC-009.

---

## Phase 4: User Story 2 & User Story 3 - Persistência, Migrations Seguras e Isolamento de Banco (Priority: P1) 🎯 MVP Core (SC-009)

**Goal**: Garantir que cada banco possua o marcador imutável `TenantDatabaseIdentity` (`TenantId`, `EnvironmentId`, `AccessEpoch`), que migrations rodem somente via EF CLI com preflight de identidade/role antes de DDL e que reaplicações sejam idempotentes sem perda de dados.

**Independent Test**: Aplicar e reaplicar a migration de `TenantAccessDbContext` em dois bancos vazios A e B, persistir contas via `UserManager<AppUser>`, verificar independência sob concorrência, preservação na reaplicação e bloqueio antes de DDL quando a configuração de A aponta para B.

### Tests for User Story 2 & User Story 3 ⚠️

- [X] T013 [P] [US2] Criar testes de integração em dois bancos PostgreSQL reais, após T020–T023, com contas via `UserManager<AppUser>`, mesmo ID/email e senhas distintas em A/B; comprovar concessão/revogação local, unicidade normalizada dentro do banco, FKs, commit/rollback e operações runtime sobrepostas por barreira (pico >=2, ao menos uma por banco), com resultados válidos e zero efeito cruzado inclusive sob falha provocada em A, em `backend/tests/Platform.IntegrationTests/Access/TenantUserPersistenceTests.cs`
- [X] T017 [P] [US3] Criar testes unitários de preflight (`TenantId`, `EnvironmentId`, estado `Inactive` para DDL) e de `Migrations:AllowInitializeEmptyDatabase` (ausente/`false` nega inicialização, valor inválido falha e `true` não dispensa validação do alvo) em `backend/tests/Platform.UnitTests/Tenancy/MigrationPreflightValidatorTests.cs`
- [X] T018 [P] [US3] Criar testes de integração para `SC-009`, após T020–T023, validando aprovação explícita e aplicação inicial em bancos vazios A/B, negação antes de DDL sem aprovação ou com estrutura sem marcador, bloqueio de marcador divergente mesmo com aprovação, reaplicação sem duplicação/perda com aprovação desligada e bloqueio de credencial runtime tentando DDL ou conexão cruzada em `backend/tests/Platform.IntegrationTests/Tenancy/TenantMigrationAndIsolationTests.cs`

### Implementation for User Story 3

- [X] T019 [P] [US3] Implementar entidade `TenantDatabaseIdentity` no schema `foundation` com todas as restrições de `data-model.md` (`SingletonId`: `inteiro — 1; uma linha.`; `TenantId`: `UUID — Único, imutável; identidade do banco.`; `EnvironmentId`: `UUID — Ambiente esperado no catálogo/configuração.`; `AccessEpoch`: `UUID — Geração local de acesso; começa na inicialização técnica.`; `InitializedAt`: `instante UTC — Obrigatório.`) em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Entities/TenantDatabaseIdentity.cs`
- [X] T020 [US3] Implementar `TenantAccessDbContext` (`IdentityUserContext<AppUser, Guid>`) integrando `TenantDatabaseIdentity` (schema `foundation`), `AppUser`/`UserPermission` (schema `access`), histórico de migrations em `foundation.__EFMigrationsHistory` e FK de `AppUser.TenantId` para `TenantDatabaseIdentity.TenantId` em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/TenantAccessDbContext.cs`
- [X] T021 [US3] Implementar preflight com `Migrations:AllowInitializeEmptyDatabase` exclusivo do tooling (ausente/`false` nega inicialização; valor inválido falha; somente `true` aprova banco vazio após validar identidade/argumentos, banco, role e estado inativo), bloqueando marcador divergente ou estrutura sem marcador antes de DDL mesmo com aprovação, e callbacks idempotentes `UseSeeding`/`UseAsyncSeeding` sem usuários/senhas padrão, em `backend/src/Platform.Infrastructure/Persistence/DesignTime/TenantMigrationPreflight.cs`
- [X] T022 [US3] Implementar `TenantAccessDbContextFactory` (`IDesignTimeDbContextFactory<TenantAccessDbContext>`) recebendo `--tenant-id` e `--environment-id` via EF CLI em `backend/src/Platform.Infrastructure/Persistence/DesignTime/TenantAccessDbContextFactory.cs`
- [X] T023 [US3] Gerar a migration inicial versionada de `TenantAccessDbContext` (`InitialTenantAccessSchema`) em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Migrations/`
- [X] T024 [US3] Implementar `TenantDatabaseIdentityProbe` em `Platform.Infrastructure` para verificar em runtime se o banco configurado contém o marcador `TenantDatabaseIdentity` esperado e nenhuma migration pendente em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/TenantDatabaseIdentityProbe.cs`

**Checkpoint**: T013/T017/T018 aprovados após contexto, preflight, factory e migration; `SC-009` comprovado no nível de persistência, com dois bancos independentes, reaplicação sem perda, concorrência mínima e proteção contra configuração cruzada. O backend executável completa o aceite nas fases seguintes.

**Evidência da Parada 2 (2026-10-28)**: Entidades `AppUser`, `UserPermission`, `TenantDatabaseIdentity`, mapeamentos nos schemas `access` e `foundation`, `TenantAccessDbContext`, migration inicial versionada `InitialTenantAccessSchema`, preflight de migration com validação de `AllowInitializeEmptyDatabase` e `TenantDatabaseIdentityProbe` implementados. Suíte de testes: 67 testes aprovados (25 unitários, 5 de contrato e 37 asserções nos testes de integração com Testcontainers PostgreSQL 18.6), comprovando criação da estrutura em dois bancos de clientes, reaplicação idempotente sem perda de dados, contas independentes com mesmo ID/email e senhas distintas, permissões isoladas, integridade referencial com o marcador, commit/rollback transacional e concorrência com sobreposição (pico >= 2) com falha em A sem efeito em B. Fase 5 não iniciada.

---

## Phase 5: User Story 5 - Contrato HTTP Mínimo e Diagnóstico Seguro da API em I1 (Priority: P2)

**Goal**: Expor somente `/health/live`, `/health/ready` e `/openapi/v1.json` em `App.Api` conforme `contracts/openapi.json`, retornando `404 Problem Details` para rotas não mapeadas (`/api/v1/auth/*`, `/me`, `/context`) e `503 Problem Details` quando a configuração ou o marcador do banco falhar.

**Independent Test**: Subir `App.Api` em teste de contrato, validar respostas `200` de `/health/live` e `/health/ready`, validar o documento `/openapi/v1.json` contra `specs/001-fullstack-foundation/contracts/openapi.json` e verificar `404`/`503` em formato `application/problem+json` sem vazamento de secrets.

### Tests for User Story 5 ⚠️

- [ ] T025 [P] [US5] Criar testes de contrato HTTP validando `/health/live`, `/health/ready`, `/openapi/v1.json`, `503 DEPENDENCY_UNAVAILABLE` (quando o marcador do banco diverge ou o banco está indisponível) e `404 NOT_FOUND` em rotas ausentes (`/api/v1/auth/login`, `/api/v1/me`, `/api/v1/context`) em `backend/tests/Platform.ContractTests/Health/HealthAndOpenApiContractTests.cs`

### Implementation for User Story 5

- [ ] T026 [P] [US5] Implementar health checks de liveness (sem acesso a banco ou cookies) e readiness (validando `InstallationTenantOptions` e `ITenantDatabaseIdentityProbe` sem rodar migrations no startup) em `backend/src/App.Api/Health/InstallationReadinessHealthCheck.cs`
- [ ] T027 [US5] Configurar geração de OpenAPI 3.0.3 (`/openapi/v1.json`), mapeamento de `/health/live` e `/health/ready` e fallback `404 Problem Details` em `backend/src/App.Api/Program.cs`

**Checkpoint**: API executável, expondo exclusivamente a superfície contratada para o I1.

---

## Phase 6: User Story 1 - Execução Local Reprodutível com Docker Compose (Priority: P1)

**Goal**: Permitir que um desenvolvedor suba o PostgreSQL 18.6, aplique a migration via profile `migrations` e inicie a `App.Api` a partir de um clone limpo usando variáveis de ambiente por instalação, sem credenciais reais no repositório.

**Independent Test**: Configurar `infra/.env.local` (A) e `infra/.env.b.local` (B) a partir do exemplo, validar `docker compose config --quiet` em projetos distintos e aplicar a migration com conexões/owners próprios; ativar A e confirmar `200 OK` em `/health/live`, `/health/ready` e `/openapi/v1.json`.

### Tests for User Story 1 ⚠️

- [ ] T028 [P] [US1] Criar testes unitários garantindo que configuração obrigatória ausente ou inválida em `InstallationTenantOptions` bloqueia a prontidão com diagnóstico seguro (sem expor secrets ou connection strings) em `backend/tests/Platform.UnitTests/Configuration/InstallationTenantOptionsValidationTests.cs`

### Implementation for User Story 1

- [ ] T029 [P] [US1] Criar `Dockerfile` multi-stage para `App.Api` (runtime ASP.NET Core `10.0.12` não-root, sem SDK ou credenciais de migration) em `infra/docker/Dockerfile.api`
- [ ] T030 [P] [US1] Criar `Dockerfile` para o serviço pontual `migrations` (SDK `10.0.401` com `dotnet-ef` `10.0.12` restaurado) em `infra/docker/Dockerfile.migrations`
- [ ] T031 [P] [US1] Criar arquivo de exemplo com variáveis fictícias da instalação (`ApplicationId`, `EnvironmentId`, `TenantId`, `TenantDisplayName`, `TenantState`, conexões runtime/migration e `Migrations__AllowInitializeEmptyDatabase=false`), documentando configurações privadas próprias para A/B, em `infra/.env.example`
- [ ] T032 [US1] Criar `infra/compose.yaml` com `postgres` (`postgres:18.6`, volume em `/var/lib/postgresql`, sem porta no host), `api` e `migrations` (profile pontual); separar credenciais runtime/owner, montar aprovação de inicialização somente no tooling e permitir projetos A/B com volumes/redes independentes e sem nomes globais em `infra/compose.yaml`
- [ ] T033 [US1] Atualizar `.gitignore` para excluir `.env*.local` (incluindo A/B), arquivos privados de secrets, artefatos de build/test e key rings locais em `.gitignore`

**Checkpoint**: Ambiente local completo e reproduzível via Docker Compose para uma instalação de cliente.

---

## Phase 7: User Story 4 & User Story 6 - Guardrails Arquiteturais e CI do Backend (Priority: P2)

**Goal**: Garantir por testes automatizados que os limites das camadas (`Domain`, `Application`, `Infrastructure`, `App.Api`) não sejam violados e configurar o pipeline GitHub Actions para validar build, formatação, testes, vulnerabilidades e secrets antes da integração.

**Independent Test**: Executar a suíte de arquitetura e validar o workflow `.github/workflows/backend-ci.yml` com todos os gates de qualidade de I1.

### Tests for User Story 4 & User Story 6 ⚠️

- [ ] T034 [P] [US4] Criar testes automatizados de arquitetura verificando que `Platform.Domain` não referencia EF Core, Identity ou HTTP, que `Platform.Application` não referencia `Platform.Infrastructure` e que não há dependências circulares em `backend/tests/Platform.UnitTests/Architecture/LayerDependencyTests.cs`

### Implementation for User Story 4 & User Story 6

- [ ] T036 [US6] Criar workflow do backend com restore em `--locked-mode` usando lockfiles de T006, build, `dotnet format --verify-no-changes`, testes/Testcontainers com falha para zero testes, auditoria NuGet com bloqueio de achados altos/críticos não mitigados e `gitleaks`; scan ausente/falho não aprova, permissões são mínimas e actions fixadas por SHA; configurar/verificar checks obrigatórios de branch protection/ruleset com o mantenedor em `.github/workflows/backend-ci.yml`

**Checkpoint**: Limites arquiteturais e gates automatizados de qualidade e segurança ativos para o I1.

---

## Phase 8: User Story 7 & Polish - Documentação Operacional e Validação Final do I1 (Priority: P3)

**Goal**: Documentar a decisão single-tenant por deploy, registrar o guia de I1 no `README.md`, conferir lockfiles existentes e comprovar os gates com exercícios negativos controlados.

**Independent Test**: Seguir `README.md` e `specs/001-fullstack-foundation/quickstart.md` do zero, executar a bateria válida e demonstrar falha/bloqueio de cada exercício negativo da seção 5, retornando ao estado válido sem desativar checks.

- [ ] T037 [P] [US7] Registrar ADR documentando a decisão de implantação dedicada por cliente (1 backend + 1 banco PostgreSQL por instalação, sem `platform_catalog`, com segredos por aplicação no Dokploy/VPS) em `docs/adr/0001-single-tenant-per-deploy.md`
- [ ] T038 [P] [US7] Atualizar `README.md` com arquitetura, pré-requisitos de bancos/roles, configurações e projetos Compose próprios para A/B, aprovação de inicialização desligada por padrão e removida após uso, sequência inativar/migrar/ativar, checks obrigatórios e separação de `I1` a `I4` em `README.md`
- [ ] T039 Conferir os `packages.lock.json` existentes com restore em `--locked-mode` e executar `specs/001-fullstack-foundation/quickstart.md`, incluindo prova concorrente mínima e exercícios negativos de SC-006 (compilação/tipos, formatação, persistência/isolamento, secret fictício, relatório sintético de vulnerabilidade alta/crítica, scan indisponível e zero testes); registrar check/resultado/bloqueio no workflow/PR e confirmar execução válida após descartar somente as alterações do exercício, sem secrets reais ou dependências vulneráveis

---

## Dependencies & Execution Order

### Phase Dependencies (Fluxo em Lotes Pequenos)

- **Phase 1 (Setup)**: Sem dependências — início imediato (`T001`–`T006`).
- **Phase 2 (Foundational)**: Depende da Phase 1 — prepara opções da instalação, `ProblemDetails` e fixture PostgreSQL (`T007`–`T011`).
- **Phase 3 (US2 — Modelo de Usuários/Permissões)**: Depende da Phase 2 (`T012`, `T014`–`T016`); não depende de banco inicializado.
- **Phase 4 (US2 & US3 — Persistência e Migrations Seguras)**: Depende da Phase 3 (`T013`, `T017`–`T024`). T020 depende de T014–T016 e T019; T021 depende de T008/T020; T022 depende de T021; T023 depende de T022; T013/T018 só são executadas e validadas após T023 e seus pré-requisitos. **Ao concluir a Phase 4, o núcleo de persistência do `SC-009` está pronto e testado, inclusive sob concorrência mínima.**
- **Phase 5 (US5 — Superfície HTTP `/health` e `/openapi`)**: Depende da Phase 4 (`T025`–`T027`). **Ao concluir a Phase 5, o MVP do backend I1 já responde HTTP e passa nos testes.**
- **Phase 6 (US1 — Docker Compose Local)**: Depende da Phase 5 (`T028`–`T033`).
- **Phase 7 (US4 & US6 — Arquitetura e CI)**: Pode rodar após a Phase 5 em paralelo com a Phase 6 (`T034`, `T036`); T036 depende também dos lockfiles criados em T006 e atualizados nas mudanças de dependências.
- **Phase 8 (US7 & Polish — ADR, README e Validação Final)**: Depende das fases anteriores (`T037`–`T039`).

### Parallel Opportunities

- **Phase 1**: `T003`, `T004` e `T005` podem rodar em paralelo.
- **Phase 2**: `T007`, `T008`, `T009` e `T010` manipulam arquivos independentes e podem rodar em paralelo.
- **Phase 3 (US2)**: T012 pode ser escrita em paralelo a T014/T015, mas validada após as implementações; T014 (`AppUser.cs`) e T015 (`UserPermission.cs`) são independentes.
- **Phase 4 (US2 & US3)**: T017 pode ser escrita em paralelo a T019; T013/T018 podem rodar em paralelo somente após T023, usando fixtures/alvos próprios por suíte.
- **Phase 6 (US1)**: `T028`, `T029`, `T030` e `T031` podem ser executadas em paralelo.
- **Phase 7**: T034/T036 manipulam arquivos distintos e podem rodar em paralelo após seus pré-requisitos.
- **Phase 8**: T037/T038 podem rodar em paralelo após as fases anteriores; T039 depende de ambas e de T036.

---

## Parallel Example: User Story 2 & User Story 3 (Núcleo SC-009)

```text
# Phase 3, após Phase 2 — entidades em arquivos independentes:
Task T014: "Implementar entidade AppUser em backend/src/Platform.Infrastructure/Persistence/TenantAccess/Entities/AppUser.cs"
Task T015: "Implementar entidade UserPermission em backend/src/Platform.Infrastructure/Persistence/TenantAccess/Entities/UserPermission.cs"

# Phase 4, após T023 e seus pré-requisitos — integração com alvos próprios por suíte:
Task T013: "Validar persistência e concorrência em backend/tests/Platform.IntegrationTests/Access/TenantUserPersistenceTests.cs"
Task T018: "Validar migrations e isolamento em backend/tests/Platform.IntegrationTests/Tenancy/TenantMigrationAndIsolationTests.cs"
```

---

## Implementation Strategy (Execução Gradual sem Fluxo Gigante)

Para evitar um fluxo longo de uma só vez, você pode executar o `/speckit-implement` **por parada (checkpoint)**:

1. **Parada 1 — Esqueleto e Fundação (`Phase 1` + `Phase 2`, `T001` a `T011`)**:
   - Cria a solution `.NET 10`, pacotes fixados, lockfiles iniciais, configuração da instalação e fixture de teste.
2. **Parada 2 — Banco e Migrations (`Phase 3` + `Phase 4`, `T012` a `T024`)**:
   - Entrega `AppUser`, `UserPermission`, `TenantDatabaseIdentity`, `TenantAccessDbContext`, migration inicial e testes de isolamento entre dois bancos A e B (`SC-009`).
3. **Parada 3 — API Mínima e Docker Compose (`Phase 5` + `Phase 6`, `T025` a `T033`)**:
   - Entrega `/health/live`, `/health/ready`, `/openapi/v1.json` e o `infra/compose.yaml` funcionando localmente.
4. **Parada 4 — CI e Documentação (`Phase 7` + `Phase 8`, `T034`, `T036` a `T039`)**:
   - Fecha testes de arquitetura, workflow, prova negativa dos gates, ADR e `README.md`; T035 não é executada em I1.
5. **Próximos Incrementos (futuros, fora deste `tasks.md`)**:
   - **I2**: Login/logout, sessão por cookie (`AuthSession` + `ITicketStore`), `/api/v1/me` e `/api/v1/context`.
   - **I3**: Frontend React + TypeScript + Vite + Tailwind CSS e validação de extensão modular. **T035 adiada**: contrato de registro modular só será definido quando houver consumidor concreto; não criar `IModuleRegistration` em I1. O ID permanece reservado, sem renumerar as tarefas existentes.
   - **I4**: Publicação por cliente no Dokploy/VPS e rotinas de backup/restore.
