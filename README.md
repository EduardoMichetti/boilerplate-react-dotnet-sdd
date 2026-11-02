# Boilerplate Full-Stack SDD (React + .NET 10 + PostgreSQL)

Base de código reutilizável e orientada a especificações para produtos empresariais com arquitetura **Single-Tenant por Deploy** (1 backend dedicado + 1 banco PostgreSQL exclusivo por cliente, sem banco de catálogo compartilhado).

---

## 🏛️ Arquitetura e Decisão de Design

Adotamos a estratégia de isolamento físico dedicado por cliente:
- **1 Backend + 1 Banco de Dados por Instalação:** Cada cliente é executado em sua própria pilha de containers (orquestrada pelo Dokploy em produção ou via Docker Compose localmente).
- **Sem Catálogo Central:** Elimina pontos únicos de falha e o risco de vazamento cruzado de dados entre diferentes empresas.
- **Marcador Físico de Banco (`TenantDatabaseIdentity`):** Cada banco de dados possui uma linha singleton no schema `foundation` que valida a correspondência exata do `TenantId` e `EnvironmentId` configurados nas variáveis de ambiente antes de qualquer operação de negócio.
- **Separação de Privilégios (Runtime vs. Tooling):**
  - **Runtime da API:** Opera exclusivamente com credencial sem permissões de DDL (`CREATE`, `ALTER`, `DROP`). Não executa migrações no startup.
  - **Tooling de Migrations:** Executado de forma pontual (via container dedicado com profile `migrations` ou EF CLI manual) com a credencial `owner`.

Consulte o ADR detalhado em [docs/adr/0001-single-tenant-per-deploy.md](docs/adr/0001-single-tenant-per-deploy.md).

---

## 🗺️ Roteiro de Incrementos

O boilerplate é construído em 4 ciclos de entrega orientados a especificações:

| Incremento | Escopo | Status |
| :---: | :--- | :---: |
| **I1** | **Fundação do Backend e Persistência:** Modelo de usuários/permissões, migrations versionadas, liveness/readiness seguro, Docker Compose local e guardrails de CI. | **Concluído** |
| **I2** | **Autenticação e Sessão HTTP:** Login seguro por cookie (`__Host-bps-session`), antiforgery CSRF, proteção contra força bruta e contexto do tenant. | Planejado |
| **I3** | **Frontend Web Moderno:** Aplicação React 19 + TypeScript + Vite, telas de autenticação e consumo de contratos OpenAPI. | Planejado |
| **I4** | **Deploy em Produção / Dokploy:** Orquestração com Dokploy na VPS, SSL automático, backups com retenção e rotinas operacionais. | Planejado |

---

## ⚙️ Pré-requisitos para Desenvolvimento Local

- **.NET SDK:** `10.0.403` (ou compatível com a feature band 10.0.4xx declarada em `global.json`).
- **Docker & Docker Compose:** Docker Desktop ou Docker Engine Linux.
- **Git** e terminal PowerShell ou Bash.

---

## 🚀 Como Executar Localmente

### 1. Restaurar ferramentas e dependências
```powershell
dotnet tool restore
dotnet restore backend/Boilerplate.slnx --locked-mode
dotnet build backend/Boilerplate.slnx --no-restore
```

### 2. Executar a suíte de testes automatizados
```powershell
# Executa testes unitários (modelo de acesso, preflight, configurações e arquitetura)
dotnet test backend/tests/Platform.UnitTests/Platform.UnitTests.csproj --no-restore

# Executa testes de contrato HTTP (/health/live, /health/ready, /openapi/v1.json, 404 e 503)
dotnet test backend/tests/Platform.ContractTests/Platform.ContractTests.csproj --no-restore

# Executa testes de integração com PostgreSQL real (requer Docker ativo)
dotnet test backend/tests/Platform.IntegrationTests/Platform.IntegrationTests.csproj --no-restore
```

---

## 🐳 Execução Local com Docker Compose (Instalações A e B)

O ambiente Compose permite subir instâncias isoladas para validar múltiplos clientes simultâneos sem colisão de portas ou volumes.

### Passo 1: Preparar as variáveis de ambiente
Copie o arquivo de exemplo para as instalações locais desejadas:
```powershell
Copy-Item infra/.env.example infra/.env.local     # Cliente A
Copy-Item infra/.env.example infra/.env.b.local   # Cliente B (para validação cruzada)
```

### Passo 2: Subir o PostgreSQL
```powershell
docker compose --project-name boilerplate-a -f infra/compose.yaml --env-file infra/.env.local up -d postgres
```

### Passo 3: Executar a primeira migration (Inicialização do Banco)
1. Edite `infra/.env.local` e defina temporariamente:
   - `TenantState=Inactive`
   - `Migrations__AllowInitializeEmptyDatabase=true`
2. Execute o serviço pontual de migração:
   ```powershell
   docker compose --project-name boilerplate-a -f infra/compose.yaml --env-file infra/.env.local --profile migrations run --rm migrations dotnet ef database update --project Platform.Infrastructure --startup-project App.Api --context ContextoBancoAcessoInquilino -- --tenant-id aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa --environment-id 11111111-1111-4111-8111-111111111111
   ```
3. Retorne `Migrations__AllowInitializeEmptyDatabase=false` e altere `TenantState=Active`.

### Passo 4: Subir a API do Cliente A
```powershell
docker compose --project-name boilerplate-a -f infra/compose.yaml --env-file infra/.env.local up -d api
```

### Passo 5: Testar os endpoints
```powershell
# Liveness (processo vivo sem tocar em banco)
curl http://localhost:8080/health/live

# Readiness (valida opções da instalação e marcador no banco)
curl http://localhost:8080/health/ready

# Contrato OpenAPI exposto em I1
curl http://localhost:8080/openapi/v1.json
```

---

## 🛡️ Qualidade de Código e CI/CD

O repositório possui regras estritas de verificação automatizada via GitHub Actions (`.github/workflows/backend-ci.yml`):
- **Formatação de Código:** Verificada por `dotnet format backend/Boilerplate.slnx --verify-no-changes --no-restore`.
- **Modo Travado de Dependências:** Restore obrigatório com `--locked-mode`.
- **Auditoria de Vulnerabilidades NuGet:** Bloqueia o pipeline se houver pacotes com vulnerabilidades de severidade Alta ou Crítica.
- **Detecção de Segredos:** Varredura com `Gitleaks` impedindo commits com credenciais reais.
