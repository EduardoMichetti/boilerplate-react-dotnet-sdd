# Implementation Plan: Fundação full-stack reutilizável

**Branch**: `main` | **Date**: 2026-10-13 | **Spec**: [spec.md](spec.md)

**Feature Context**: `001-fullstack-foundation`, resolvido por `.specify/feature.json`;
o identificador do Spec Kit não cria nem troca a branch Git.

**Input**: atualizar o desenho de `boilerplate-react-dotnet-sdd` conforme os
esclarecimentos de 2026-10-13 e o primeiro incremento FR-034/SC-009.

**Status**: planejamento documental. Não são implementados projetos, migrations,
containers, pipelines ou funcionalidades nesta etapa; `tasks.md` pertence à próxima etapa.

## Summary

O primeiro incremento, **I1**, entrega somente backend iniciado e executável,
persistência de usuários/acesso e migrations aplicáveis ao banco PostgreSQL exclusivo
da instalação (validado em testes com dois bancos independentes simulando dois clientes).
Sem frontend, painel, módulo de negócio, suíte administrativa ou banco de catálogo central.
Cadastro persistível não significa login HTTP já disponível.

O modelo adotado é **uma instalação dedicada por cliente (single-tenant por deploy:
1 backend + 1 banco da própria aplicação por cliente)**. Cada instalação recebe sua
identidade (`TenantId`, `EnvironmentId`), estado e conexão por variáveis de ambiente/secrets
(no Dokploy/VPS ou cofre externo) e valida o marcador de identidade gravado no seu próprio
banco antes de operar. Não existe banco apartado `platform_catalog` nem `CatalogDbContext`.
Cada cliente possui suas contas, credenciais, concessões e sessões futuras no próprio banco.

Docker Compose local, testes reais de persistência/isolamento e checks mínimos
de backend são suporte obrigatório de I1, não uma ampliação funcional.
Pipeline full-stack, autenticação HTTP, frontend e produção pertencem a incrementos
posteriores. Não se libera operação protegida sem seus controles completos.

## Technical Context

**Language/Version**: I1 usa C# 14, SDK .NET 10.0.401, target `net10.0` e runtime
.NET/ASP.NET Core 10.0.12 LTS. Base anterior preservada, sem upgrade nesta revisão.

**Primary Dependencies**: I1 usa Identity/EF Core 10.0.12, provider Npgsql/driver
10.0.3 e OpenAPI nativo. `dotnet-ef` 10.0.12 como ferramenta local fixada.
React 19.3.0, TypeScript 7.0.2, Vite 8.3.4, Tailwind 4.3.3 e Node 24.21.0 LTS
permanecem previstos somente para o frontend posterior.

**Storage**: PostgreSQL 18.6; **um único banco da própria aplicação por instalação
de cliente** (sem banco apartado de catálogo central). `TenantAccessDbContext` contém
o marcador de identidade da instalação e o modelo de acesso local daquele cliente.

**Testing**: I1 usa `xunit.v3` 4.0.2, Microsoft.Testing.Platform v2,
Testcontainers.PostgreSql 4.16.0 e testes HTTP de saúde/ausência de endpoints.
Vitest, Testing Library e Playwright são dependências posteriores, não de I1.

**Quality Tooling**: `dotnet format`, lockfiles, Gitleaks CLI 8.30.1 e auditoria
NuGet; GitHub Actions com permissões mínimas e ações fixadas por SHA.

**Target Platform**: containers Linux amd64, Docker Compose v2 em Windows/Linux;
VPS Linux e Dokploy (uma stack/serviço por cliente com variáveis de ambiente e secrets
próprios) na fase de publicação.

**Project Type**: monólito modular ASP.NET Core; quatro projetos de produção por
camada. Migrations por EF CLI, sem processo administrativo residente.

**Performance Goals**: SC-009 é o aceite de I1: dois bancos vazios (simulando duas
instalações de clientes), criação da estrutura, persistência independente e reaplicação
sem duplicação/perda. I1 também comprova isolamento com operações de A e B efetivamente
sobrepostas, sem antecipar a meta completa de SC-003: 200 operações, 100 por instalação
de cliente e pico observado de pelo menos 20, em I2. SC-006/SC-008 acompanham os checks
e a documentação de I1; os demais critérios permanecem nos incrementos posteriores.

**Constraints**: nenhum painel, banco de catálogo central, dado/conta global, seed de
usuário padrão, secret no Git/imagem/log ou endpoint de cadastro/login protegido exposto em I1.
Migrations não rodam no startup e não usam a credencial runtime.

**Scale/Scope**: configuração de instalação por ambiente, dois bancos sintéticos em
testes de isolamento e quatro camadas; somente health checks e OpenAPI da superfície
efetivamente disponível em I1.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

Constituição vigente: [constitution.md](../../.specify/memory/constitution.md).
A fase 0 reutiliza a matriz de versões existente e verifica os mecanismos nativos
de migration e sessão; não introduz novas tecnologias.

| Princípio | Antes da pesquisa | Após o design | Evidência |
|-----------|-------------------|---------------|-----------|
| I. Arquitetura modular | PASS | PASS | Quatro camadas; referências para dentro; sem microsserviços. |
| II. Reutilização | PASS | PASS | Acesso é fundação; módulos futuros só por contratos explícitos. |
| III. Stack | PASS | PASS | Stack e pins preservados em research; sem dependências frontend em I1. |
| IV. Isolamento por cliente | PASS | PASS | 1 backend + 1 banco por instalação; sem catálogo central; validação do marcador do banco antes de uso. |
| V. API | PASS | PASS | OpenAPI de I1 separado do contrato futuro; Problem Details. |
| VI. Segurança | PASS | PASS | Runtime sem DDL; sem seed padrão nem endpoints protegidos incompletos. |
| VII. Qualidade | PASS | PASS | PostgreSQL real, testes e checks mínimos obrigatórios já em I1. |
| VIII. Infraestrutura | PASS | PASS | Compose local em I1; publicação/backup são gates posteriores. |
| IX. Manutenibilidade | PASS | PASS | EF CLI/factories nativas, sem suite Tools obrigatória. |
| X. Evolução | PASS | PASS | Incrementos explícitos, sem funcionalidades comerciais antecipadas. |

O adiamento genérico de CI foi delimitado na especificação: adia-se pipeline
full-stack/deploy, não os checks pertinentes ao backend antes da integração.
Nenhum princípio foi relaxado; não há exceção solicitada.

PASS indica conformidade do desenho, não testes de aplicação ou produção executados.

## Project Structure

### Documentation (this feature)

```text
specs/001-fullstack-foundation/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── openapi.json
│   ├── future-openapi.json
│   └── operations.md
└── checklists/
    └── requirements.md
```

Artefatos de design: [research](research.md), [modelo](data-model.md),
[API I1](contracts/openapi.json), [API futura](contracts/future-openapi.json),
[operação](contracts/operations.md) e [validação I1](quickstart.md).

### Source Code (repository root)

Estrutura a implementar em I1; nenhuma dessas pastas é criada por este comando.

```text
backend/
├── Boilerplate.slnx
├── src/
│   ├── Platform.Domain/
│   ├── Platform.Application/
│   ├── Platform.Infrastructure/
│   │   └── Persistence/
│   │       ├── TenantAccess/
│   │       └── DesignTime/
│   └── App.Api/
└── tests/
    ├── Platform.UnitTests/
    ├── Platform.IntegrationTests/
    └── Platform.ContractTests/
infra/
├── compose.yaml
├── .env.example
└── docker/
.github/
└── workflows/
docs/
└── adr/
```

`frontend`, `infra/deploy`, hosts de fixtures de UI e ferramentas de operação
ampliada só são criados quando seus incrementos forem solicitados.

**Structure Decision**: Domain não referencia HTTP, EF ou Identity. Application
referencia Domain e define portas específicas de acesso e resolução técnica.
Infrastructure implementa essas portas e herda tipos Identity/EF; API compõe serviços,
transporte, configuração e health checks. Os tipos `IdentityUser` não vazam para Domain.

Não adicionar repositório genérico, mediator, broker, plugin remoto, cache distribuído
ou `Platform.Tools` para apenas aplicar uma migration.

### Incrementos e rastreabilidade

| Incremento | Entrega | Requisitos e critérios |
|------------|---------|------------------------|
| I1 — agora | Backend, modelo de acesso, marcador da instalação, migrations, Compose, testes e checks. | FR-034/SC-009; partes aplicáveis de FR-001 a FR-003, FR-009, FR-011 a FR-016, FR-022 a FR-028. |
| I2 — posterior | Login/logout/contexto na instalação do cliente, sessão revogável e procedimento restrito de contas/permissões. | FR-005 a FR-008, FR-020 a FR-023, FR-033; SC-002/SC-005. |
| I3 — posterior | Interface web e validação de extensão por componentes opcionais de teste. | FR-004, FR-017 a FR-019; SC-001/SC-004. |
| I4 — antes de produção | Operação de deploys por cliente, publicação no Dokploy e recuperação. | FR-010, FR-014/FR-015 completos, FR-029 a FR-032; SC-007. |
| Transversal | Documentação, riscos e evidências dos incrementos entregues. | FR-024 a FR-028; SC-006/SC-008 conforme superfície existente. |

Nenhum requisito futuro foi removido; também não se afirma sua conclusão em I1.
As tarefas devem distinguir suporte mínimo de I1 de workstreams futuros, não tratar
as quatro entregas como um único bloqueio para o primeiro incremento.

### I1 — modelo e migration de acesso

- Modelo em `TenantAccessDbContext`, baseado em `IdentityUserContext<AppUser, Guid>`,
  mais concessões explícitas por usuário e marcador de identidade da instalação no banco.
- Email normalizado é único dentro de cada banco, não globalmente. TenantId faz
  parte da conta e deve corresponder ao único marcador gravado nesse banco.
- O vínculo da conta com a empresa é representado por sua propriedade TenantId;
  não existe banco de catálogo central (`platform_catalog`) nem entidade central com
  vínculos da mesma conta a vários tenants.
- Hash e validação de senha usam Identity. Testes criam contas sintéticas por
  UserManager; a migration cria estrutura, não usuários ou senhas.
- A prova de persistência de I1 usa credenciais runtime próprias em dois bancos reais,
  com o mesmo email e identificador local em A/B. Uma barreira de sincronização deve
  comprovar ao menos uma operação de cada banco sobreposta à outra (pico observado >=2).
  Verificar leituras, gravações, concessão/revogação, unicidade local, FKs, commit e
  rollback; falha provocada em A não altera B. Negar todas as operações não satisfaz
  a prova. Não exige endpoints de usuário nem a carga completa de SC-003.
- Sessão persistida é uma extensão de modelo de I2, não pré-condição de SC-009.

Configuração da instalação (via variáveis de ambiente/secrets no Compose local ou no
Dokploy na VPS): `ApplicationId`, `EnvironmentId`, `TenantId`, `TenantDisplayName`,
estado da instância e string/secret de conexão do próprio banco. Sem painel, listagem
anônima ou catálogo de usuários.

### I1 — aplicação segura das migrations

`IDesignTimeDbContextFactory` de `TenantAccessDbContext` recebe a configuração da
instalação e argumentos explícitos de EF CLI (`--tenant-id`, `--environment-id`).
Não existe `CatalogDbContext`. Não aceitar string de conexão/secret por HTTP nem
inferir valores padrão silenciosos.

Antes de aplicar DDL:

1. Validar configuração da instalação (`ApplicationId`, `EnvironmentId`, `TenantId`) e conexão do banco-alvo.
2. Exigir janela de manutenção/instância inativa, permissão operacional e credencial owner do banco daquela instalação.
3. Verificar banco esperado, role efetivo e marcador existente (`TenantDatabaseIdentity`).
4. Se houver marcador divergente (outro `TenantId` ou `EnvironmentId`), interromper **antes** das migrations.
5. Se não houver marcador, permitir somente inicialização explicitamente autorizada
   de banco vazio, já criado pelo operador e sem objetos de aplicação preexistentes.

Essa autorização é `Migrations:AllowInitializeEmptyDatabase`, booleana e exclusiva do
tooling (variável `Migrations__AllowInitializeEmptyDatabase`). Ausente ou `false`, nega
inicialização de banco sem marcador antes de DDL; valor inválido é erro explícito.
Somente `true` autoriza o alvo da conexão privada aprovada, após validar a identidade
configurada, sua correspondência com os argumentos EF CLI, banco, role e estado inativo.
O sinal não seleciona outro banco nem dispensa qualquer validação. Marcador divergente
ou estrutura existente sem marcador sempre bloqueia, mesmo com `true`. Com marcador
correto, aplicar/reaplicar migrations não exige essa autorização de inicialização.
Remover o sinal ou voltar a `false` após a primeira inicialização; não montá-lo na API.

Após a migration, `UseSeeding` e `UseAsyncSeeding` inicializam somente o marcador
imutável da instalação aprovada; repetição com marcador correto é no-op. Não usar HasData
com valores que alterem o modelo por cliente. Callback não corrige marcador ausente
em banco existente nem sobrescreve identidade divergente.

Lock nativo de migrations protege execução concorrente; preflight também exige
janela sem writers/DDL concorrentes. A credencial owner é montada somente no processo
de tooling (`migrations`). A API usa credencial runtime sem permissão de DDL e não cria
bancos, modifica esquema ou inicializa marcador no startup.

### I1 — superfície HTTP, execução e qualidade

Somente `/health/live`, `/health/ready` e documentação OpenAPI de I1.
Nenhum endpoint de contas, concessões, contexto de usuário ou login é mapeado.
Rotas não existentes retornam 404 Problem Details, não HTML ou sucesso fictício.

Liveness não consulta o banco de dados. Readiness verifica a configuração runtime da
instalação e confirma que o banco configurado possui o marcador (`TenantId`/`EnvironmentId`)
e a versão de migration esperados para aquela instância.
Health nunca inicia lookup de sessão por cookie recebido, inclusive após I2.

Compose local inclui API, PostgreSQL e perfil pontual `migrations` com SDK;
esse perfil usa EF CLI, não suíte administrativa. Runtime e tooling têm secrets
e permissões separados. Volume PostgreSQL 18 em `/var/lib/postgresql`, imagens
versionadas/digests registrados e exemplos sem credenciais reais.
O roteiro de A/B usa configurações privadas e projetos Compose distintos, com volumes
e redes próprios, sem nomes globais nem porta de PostgreSQL publicada. Alterar somente
`--tenant-id` nunca seleciona outra instalação ou conexão.

Checks de I1: restore locked, build, format/analyzers, testes de domínio,
migration/isolamento PostgreSQL real (com dois bancos simulando duas instalações),
contrato de saúde, auditoria NuGet e secret scan.
Os lockfiles são gerados no primeiro restore da fase de setup e atualizados junto de
alterações de dependências, antes de qualquer build/CI que use `--locked-mode`.
GitHub Actions sem secrets de produção em PR, permissões mínimas, ações por SHA
e branch protection. Resultado incompleto/falho não aprova.
SC-006 exige uma execução válida e exercícios negativos controlados dos checks de I1:
compilação/tipos C#, convenções, persistência/isolamento, secret fictício, relatório
sintético de vulnerabilidade alta/crítica, scan indisponível e zero testes.
Registrar falha por check e comprovar que os checks obrigatórios bloqueiam integração;
não introduzir secrets reais ou dependências vulneráveis para executar o exercício.

### I2 — desenho de autenticação na instalação do cliente

Como cada cliente possui seu próprio backend e seu próprio banco, o login autentica
diretamente contra o banco daquela instalação, após confirmar configuração ativa e
marcador `TenantDatabaseIdentity` correspondente. O header `X-Tenant-Id` (quando enviado
pelo consumidor ou confirmado pela UI) deve coincidir com o `TenantId` configurado na
instância; qualquer divergência é rejeitada imediatamente sem fallback.

Depois da emissão, a referência de sessão protegida pelo handler nativo inclui
`TenantId`, epoch de acesso e `SessionId`. Um cookie emitido na instalação do Cliente A
nunca é válido na instalação do Cliente B (além de chaves de proteção e `TenantId`
distintos, a sessão existe apenas no banco de A).

Adapter `ITicketStore` sem estado mutável cria escopo por chamada a partir da
referência já desprotegida pelo framework; não captura DbContext scoped em singleton.

Validar sessão, usuário habilitado, stamp, `TenantId` da instalação e concessão em cada operação.
Sessão absoluta inicial de 30 minutos, logout revogado antes da resposta; renew
condicionado à sessão ativa, sem upsert, ressurreição ou extensão da expiração.
Cookies HttpOnly/Secure/SameSite=Lax e CSRF nativo em login/logout/JSON mutável.
Lockout inicial de 5 falhas/15 minutos por conta local; throttle de login por IP confiável
na instância, sem afetar a instalação ou banco de outro cliente.
Sem JWT próprio, localStorage de tokens, refresh, OIDC ou identidade global.

`/me` e `/context` retornam somente a conta e a empresa da instalação atual; não existe
`/me/tenants`. Acesso ao Cliente B ocorre no endereço/instalação de B, autenticando a conta de B.

### I3/I4 — evolução sem ampliar I1

Frontend preserva React/TypeScript/Vite/Tailwind e mesma origem da API em produção.
Módulos futuros declaram contratos e dependências acíclicas, sem acesso direto
a tabelas/DbContexts internos; variantes opcionais são comprovadas em testes.

Operação de contas é restrita à instalação do cliente, sem painel. No Dokploy (VPS),
cada cliente é publicado como uma aplicação/stack isolada, onde o operador configura
as variáveis de ambiente e segredos daquele cliente (armazenados na VPS pelo Dokploy
ou sincronizados de cofre externo). Backup do banco do cliente inclui contas, permissões,
sessões e dados de negócio. Restauração valida o marcador do cliente, invalida sessões,
renova epoch e revisa concessões antes de liberar uso, sem afetar qualquer outro cliente.

### Validação do desenho

Verificados nesta revisão: fronteiras de incremento, referências relativas,
hierarquia do template, rastreabilidade FR-001 a FR-034 e descoberta Spec Kit.
I1 contém somente duas operações de saúde; I2 tem cinco operações previstas de
autenticação/contexto mais dois health checks, sem catálogo central e sem listagem de
tenants por conta. Não há usuários globais no modelo.

Provas SC-001 a SC-009 pertencem à implementação, não são executadas neste plano.

## Complexity Tracking

Sem violação constitucional ou exceção. O isolamento single-tenant por deploy elimina
o banco de catálogo central (`platform_catalog`) e a resolução dinâmica de conexões,
reduzindo a superfície de ataque e a complexidade do código.
