# Boilerplate Full-Stack SDD (React + .NET 10 + PostgreSQL)

Base de código reutilizável e orientada a especificações para produtos empresariais com arquitetura **Single-Tenant por Deploy** (1 backend dedicado + 1 banco PostgreSQL exclusivo por cliente, sem banco de catálogo compartilhado).

Documentos de referência:

| Tema | Onde |
| :--- | :--- |
| Especificação, plano e tarefas | [spec.md](specs/001-fullstack-foundation/spec.md), [plan.md](specs/001-fullstack-foundation/plan.md), [tasks.md](specs/001-fullstack-foundation/tasks.md) |
| Modelo de dados, roles e configuração | [data-model.md](specs/001-fullstack-foundation/data-model.md) |
| Contrato HTTP publicado (OpenAPI) | [contracts/openapi.json](specs/001-fullstack-foundation/contracts/openapi.json) (servido em `/openapi/v1.json`) |
| EF CLI, identidades técnicas e host operacional | [contracts/operations.md](specs/001-fullstack-foundation/contracts/operations.md) |
| Roteiro de validação e matriz de aceite | [quickstart.md](specs/001-fullstack-foundation/quickstart.md) |
| Decisões | [ADR 0001](docs/adr/0001-single-tenant-per-deploy.md) (single-tenant por deploy), [ADR 0002](docs/adr/0002-sessoes-e-operacao-acesso-local.md) (sessões e operação de acesso), [ADR 0003](docs/adr/0003-tratamento-de-erros-e-validacao.md) (erros e validação) |
| Regras do projeto | [Constituição](.specify/memory/constitution.md) |

---

## 🏛️ Arquitetura

- **1 backend + 1 banco por instalação:** cada cliente roda na própria pilha de containers (Docker Compose local; Dokploy em produção, L4).
- **Sem catálogo central:** não há banco compartilhado entre clientes nem listagem de tenants.
- **Marcador físico do banco:** o schema `foundation` guarda uma linha singleton com `IdTenant`/`IdAmbiente`. API, tooling de migrations e host operacional conferem o marcador e a versão do esquema antes de tocar em dados; divergência responde 503 `DEPENDENCY_UNAVAILABLE` (nunca 200 vazio ou 401 silencioso).
- **Projetos backend:** `Platform.Domain`, `Platform.Application`, `Platform.Infrastructure`, `App.Api` (HTTP) e `Platform.Operations` (console de operação, sem servidor HTTP).

### Identidades do PostgreSQL por instalação

As roles derivam do nome do banco da instalação (`BANCO_APP`) e são criadas por [infra/docker/init-db.sh](infra/docker/init-db.sh) na criação do volume. Os GRANTs por tabela são emitidos pela própria migration inicial.

| Role | Quem usa | Pode | Não pode |
| :--- | :--- | :--- | :--- |
| `<banco>_owner` | Somente o serviço `migrations` | DDL e grants do banco em janela de manutenção | Ser montada na API ou usada no dia a dia |
| `<banco>_runtime` | Somente a API | Ler marcador/permissões, DML de contas para Identity/lockout, SELECT/INSERT/UPDATE de sessões | DDL, DELETE de sessões, alterar marcador ou auditoria operacional |
| `<banco>_operador` (grupo NOLOGIN) | Logins individuais de operador | DML de contas/concessões/sessões (único com DELETE de sessões) e INSERT de auditoria | DDL, UPDATE/DELETE de auditoria, outro banco |

Cada operador tem **login PostgreSQL individual** (`LOGIN_OPERADOR_ACESSO`, ex.: `appdb_cliente_a_op_maria`), membro de `<banco>_operador`, com senha própria. O host operacional confere `session_user`, a igualdade com `OperacaoAcesso__LoginAutorizado` e a pertença ao grupo; nome informado em argumento nunca substitui essa identidade. Detalhes em [operations.md §3](specs/001-fullstack-foundation/contracts/operations.md#3-identidades-técnicas-e-pré-requisitos-l2).

---

## 🗺️ Roteiro de incrementos

| Incremento | Escopo | Status |
| :---: | :--- | :---: |
| **L1** | Fundação do backend e persistência: modelo de usuários/permissões, migrations versionadas, liveness/readiness, Compose local e CI. | **Concluído** |
| **L2** | Autenticação e acesso local ao tenant: sessão por cookie revogável, antiforgery, `/me` e `/context`, host operacional de contas/permissões e `sessao limpar`. | **Concluído** |
| **L3** | Frontend React 19 + TypeScript + Vite consumindo o contrato OpenAPI; extensão modular. | Planejado |
| **L4** | Publicação Dokploy/VPS, TLS, backups e restauração por instalação, agendamento externo de `sessao limpar`. | Planejado |

O que a L2 **não** entrega: frontend, cadastro público, reset de senha por email/HTTP, JWT/refresh, listagem de tenants, deploy e backup/restauração.

---

## ⚙️ Pré-requisitos

- **.NET SDK** da feature band declarada em [global.json](global.json) (base `10.0.401`, roll-forward `latestFeature`, sem preview).
- **Docker** com Compose v2 (Docker Desktop ou Engine Linux). Os testes de integração sobem PostgreSQL real descartável via Testcontainers.
- Git e PowerShell ou Bash.

---

## 🧪 Build e testes

```powershell
dotnet tool restore
dotnet restore backend/Boilerplate.slnx --locked-mode
dotnet build backend/Boilerplate.slnx --no-restore
dotnet format backend/Boilerplate.slnx --verify-no-changes --no-restore
dotnet test --solution backend/Boilerplate.slnx --no-restore --minimum-expected-tests 1
```

O runner é o Microsoft.Testing.Platform (`global.json`), por isso use `--solution`. Suítes:

- `Platform.UnitTests`: regras de domínio/aplicação, opções, arquitetura e host operacional.
- `Platform.ContractTests`: contrato HTTP, Problem Details e documento OpenAPI servido.
- `Platform.IntegrationTests`: PostgreSQL real com instalações A e B, sessões, grants, migration, carga isolada SC-003 e consumidor independente SC-005 (exige Docker).

O [nuget.config](nuget.config) da raiz limpa as origens herdadas da máquina (`<clear/>`) e usa somente `nuget.org`; o Central Package Management exige origem única (`NU1507`).

---

## 🐳 Execução local com Docker Compose (instalações A e B)

Cada instalação é um **projeto Compose** (`--project-name`) com um **env privado** próprio. Volumes e redes são nomeados por projeto; não há `container_name` global.

### 1. Variáveis privadas (`.env.local`)

```powershell
Copy-Item infra/.env.example infra/.env.local     # instalação A (projeto boilerplate-a)
Copy-Item infra/.env.example infra/.env.b.local   # instalação B (projeto boilerplate-b)
```

Preencha os valores marcados como “definir valor privado”. Arquivos `infra/.env*.local` são ignorados pelo Git e nunca devem ser versionados. O exemplo sozinho **não sobe nada**: variáveis obrigatórias vazias fazem o Compose falhar (`${VAR:?}`).

| Variável | Uso |
| :--- | :--- |
| `CodigoAplicacao`, `IdAmbiente`, `IdTenant`, `NomeExibicaoTenant`, `EstadoTenant` | Identidade da instalação (não secretos). `EstadoTenant=Inativo` em manutenção, `Ativo` para atender. |
| `PORTA_API` | Porta da API no loopback do host (A e B distintas). |
| `BANCO_APP` | Banco exclusivo da instalação; nomes das roles derivam dele. |
| `SENHA_SUPERUSUARIO_POSTGRES` | Somente o serviço `postgres`. |
| `SENHA_OWNER` | Somente o serviço `migrations`. |
| `SENHA_RUNTIME` | Somente a `api`. |
| `LOGIN_OPERADOR_ACESSO`, `SENHA_OPERADOR_ACESSO` | Somente o serviço `operations` (login individual). |
| `Sessao__PrazoAbsolutoMinutos` | Opcional. Prazo absoluto da sessão, inteiro **5–480**, padrão **30**. Vazio = padrão; fora da faixa ou não inteiro impede a API de iniciar. |
| `OperacaoAcesso__RetencaoSessoesDias` | Opcional. Retenção do `sessao limpar`, inteiro **1–365**, padrão **30**. |
| `Migracoes__PermitirInicializarBancoVazio` | Somente tooling: `true` apenas na primeira inicialização de banco vazio aprovado. |
| `PORTA_POSTGRES_DEPURACAO` | Somente com `infra/compose.depuracao.yaml` (PostgreSQL publicado no loopback). |

Cada serviço recebe **somente a própria credencial**; a API nunca recebe senha de owner nem de operador.

### 2. Subir PostgreSQL e aplicar a migration inicial

```powershell
$compose = 'infra/compose.yaml'
docker compose --project-name boilerplate-a -f $compose --env-file infra/.env.local config --quiet
docker compose --project-name boilerplate-a -f $compose --env-file infra/.env.local up -d postgres
```

Com `EstadoTenant=Inativo` e `Migracoes__PermitirInicializarBancoVazio=true` (somente na primeira vez):

```powershell
docker compose --project-name boilerplate-a -f $compose --env-file infra/.env.local --profile migrations run --rm migrations dotnet ef database update --project Platform.Infrastructure --startup-project App.Api --context AcessoTenantDbContext -- --tenant-id <IdTenant> --environment-id <IdAmbiente>
```

Depois volte `Migracoes__PermitirInicializarBancoVazio=false`. A API não executa migrations no startup. Na primeira execução é normal ver no log uma falha de `SELECT ... "__EFMigrationsHistory"`: o EF cria a tabela e segue até `Done.`.

**Recriar o banco local do zero:** o `init-db.sh` só roda na criação do volume. Em ambiente **local e descartável**:

```powershell
docker compose --project-name boilerplate-a -f $compose --env-file infra/.env.local down --volumes
```

Isso apaga dados e key ring locais da instalação A. Nunca faça isso com dados de cliente.

### 3. Contas e permissões pelo host operacional (perfil `operations`)

Não há painel nem cadastro por HTTP. Contas e permissões são preparadas pelo `Platform.Operations`, publicado apenas no perfil pontual `operations`:

```powershell
$op = "docker compose --project-name boilerplate-a -f $compose --env-file infra/.env.local --profile operations run --rm operations"
```

| Comando | Efeito |
| :--- | :--- |
| `conta criar --id-tenant UUID --id-ambiente UUID --email EMAIL --nome-exibicao NOME` | Cria conta local (sem permissão automática). Senha lida por prompt sem eco. |
| `conta desativar --id-tenant UUID --id-ambiente UUID --id-usuario UUID` | Desativa e revoga todas as sessões da conta. |
| `conta reativar --id-tenant UUID --id-ambiente UUID --id-usuario UUID` | Reativa sem ressuscitar sessões. |
| `conta redefinir-senha --id-tenant UUID --id-ambiente UUID --id-usuario UUID` | Redefine senha (prompt sem eco), remove lockout e revoga sessões. |
| `conta revogar-sessoes --id-tenant UUID --id-ambiente UUID --id-usuario UUID` | Revoga todas as sessões da conta. |
| `permissao conceder --id-tenant UUID --id-ambiente UUID --id-usuario UUID --codigo foundation.context.read` | Concede permissão. |
| `permissao revogar --id-tenant UUID --id-ambiente UUID --id-usuario UUID --codigo foundation.context.read` | Revoga permissão. |
| `sessao limpar --id-tenant UUID --id-ambiente UUID` | Remove sessões encerradas há mais que `OperacaoAcesso__RetencaoSessoesDias` (padrão 30). |

Exemplo: `Invoke-Expression "$op conta criar --id-tenant <IdTenant> --id-ambiente <IdAmbiente> --email acesso@example.invalid --nome-exibicao 'Teste A'"`.

- Senhas **nunca** vão em argumento, variável de ambiente, stdout ou arquivo; argumentos como `--senha`/`--actor` são rejeitados.
- Saída: um objeto JSON em stdout (`idOperacao`, `resultado` `sucesso`/`negado`/`falha`, `codigo`…); exit code 0 somente para sucesso persistido e auditado. Toda execução com alvo validado grava um `RegistroOperacaoAcesso` com o login individual do operador.
- **`sessao limpar` não roda sozinho:** a API não tem job, timer nem `IHostedService` de limpeza. O agendamento externo (cron/Dokploy) é da L4; até lá, execute manualmente.

Contrato completo em [operations.md §5](specs/001-fullstack-foundation/contracts/operations.md#5-host-operacional-l2--fr-033).

### 4. Subir a API

Com `EstadoTenant=Ativo`:

```powershell
docker compose --project-name boilerplate-a -f $compose --env-file infra/.env.local up -d api
curl http://localhost:8080/health/live     # processo vivo, sem banco
curl http://localhost:8080/health/ready    # opções, marcador e versão do banco
curl http://localhost:8080/openapi/v1.json # contrato L2
```

#### Alternativa: API no host (F5 / `dotnet run`)

Para depurar a API fora do container, publique o PostgreSQL **somente no loopback** com o override de depuração (defina `PORTA_POSTGRES_DEPURACAO` no env privado):

```powershell
docker compose --project-name boilerplate-a -f $compose -f infra/compose.depuracao.yaml --env-file infra/.env.local up -d postgres
```

O perfil [launchSettings.json](backend/src/App.Api/Properties/launchSettings.json) traz só valores não secretos (identidade da instalação A, Development e key ring em `keyrings/cliente-a`, ignorado pelo Git). A string de conexão **não** é versionada: informe a da role `<BANCO_APP>_runtime` (nunca owner) no seu ambiente.

```powershell
$env:StringsConexao__BancoApp = 'Host=localhost;Port=<PORTA_POSTGRES_DEPURACAO>;Database=<BANCO_APP>;Username=<BANCO_APP>_runtime;Password=<SENHA_RUNTIME>'
dotnet run --project backend/src/App.Api
```

No VS Code, o perfil `C#: App.Api (Host Local)` lê `StringsConexao__BancoApp` do arquivo privado `infra/.env.host.local` (`envFile`, ignorado pelo Git).

A jornada de login com cookie jar (antiforgery → `POST /api/v1/sessions` → `/api/v1/me` → `/api/v1/context` → `DELETE /api/v1/sessions/current`) está em [quickstart.md §5](specs/001-fullstack-foundation/quickstart.md#5-consumidor-independente--sc-005). Toda rota de acesso exige o header `X-Tenant-Id`.

Para validar A e B juntas, use `--project-name boilerplate-b`, `--env-file infra/.env.b.local` e valores distintos de `IdTenant`, `IdAmbiente`, `BANCO_APP`, `PORTA_API` e segredos. Portas diferentes no mesmo hostname **não** isolam cookies: use cookie jars separados.

---

## 🔐 Sessão, HTTPS e chaves

- **Modo local = Development + loopback.** O Compose roda a API com `ASPNETCORE_ENVIRONMENT=Development` e publica a porta só em `127.0.0.1`. Nesse modo, os cookies são `bps-session`/`bps-csrf` e aceitam HTTP. Não publique esse modo para acesso remoto.
- **Fora de Development, HTTPS é obrigatório:** cookies `__Host-bps-session`/`__Host-bps-csrf` com `Secure`, `HttpOnly`, `SameSite`, `Path=/` e sem `Domain`; use um host por instalação.
- **Sessão:** prazo absoluto fixado na emissão (`Sessao__PrazoAbsolutoMinutos`, 5–480, padrão 30), sem sliding expiration nem refresh; mudar a configuração não altera sessões já emitidas. Logout revoga no banco antes do 204.
- **Key ring por instalação:** `ProtecaoDados__DiretorioChaves` aponta para um volume privado da instalação (`chaves_protecao_dados` no Compose). O discriminador do Data Protection combina `CodigoAplicacao/IdAmbiente/IdTenant`. Perder o volume invalida as sessões em curso.
- **Certificado em produção:** fora de Development, `ProtecaoDados__ArquivoCertificado` (PFX com chave privada) é obrigatório e `ProtecaoDados__ArquivoSenhaCertificado` aponta para um arquivo com a senha. Monte ambos como arquivos privados; nunca em Git, imagem ou build argument.
- **Proxies:** `X-Forwarded-*` não é processado na L2 (`CabecalhosEncaminhados:ProxiesConfiaveis` vazio).

Detalhes em [ADR 0002](docs/adr/0002-sessoes-e-operacao-acesso-local.md) e [operations.md §4](specs/001-fullstack-foundation/contracts/operations.md#4-contrato-http-l2).

---

## 🧾 Dados pessoais

A fundação armazena somente dois campos pessoais por conta (Constituição VI):

| Campo | Onde fica |
| :--- | :--- |
| `Email` | Tabela de usuários no banco **da própria instalação** (schema `access`). |
| `NomeExibicao` | Mesma tabela, no mesmo banco. |

- Não há cópia em banco central nem em outra instalação.
- Email, nome, senha, hash, cookie, ticket de sessão e string de conexão **nunca** aparecem em logs, respostas de erro (Problem Details) ou saída do host operacional. A saída do host traz só identificadores (`idUsuario`, `idOperacao`) e o login técnico do operador.
- `/api/v1/me` devolve email e nome apenas da própria conta autenticada.
- Senhas são armazenadas só como hash do ASP.NET Core Identity.
- **Retenção e base legal** dos dados pessoais são definidas pelo **produto derivado**; a fundação oferece apenas a retenção técnica de sessões encerradas (`sessao limpar`).

---

## 🛡️ Qualidade e CI

[.github/workflows/backend-ci.yml](.github/workflows/backend-ci.yml) roda em alterações de `backend/**`, `infra/**` e arquivos de build:

- restore `--locked-mode`, build, `dotnet format --verify-no-changes` e testes (`--minimum-expected-tests 1`, nenhuma execução sem testes aprova);
- validação de `infra/compose.yaml` com valores fictícios gerados na execução, e confirmação de que `infra/.env.example` sozinho falha;
- auditoria NuGet bloqueando vulnerabilidades altas/críticas;
- Gitleaks contra secrets. Falso-positivo só é aceito após triagem, por fingerprint exato em [.gitleaksignore](.gitleaksignore) (nunca por regra ou caminho inteiro).

Pull requests seguem [.github/pull_request_template.md](.github/pull_request_template.md) (objetivo, riscos, evidências, documentação, contratos, tenants, dependências e critérios de qualidade).
