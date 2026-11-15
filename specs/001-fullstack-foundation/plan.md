# Implementation Plan: L2 — autenticação e acesso local ao tenant

**Branch Git**: `main` | **Date**: 2026-11-05 | **Revisão**: 2026-11-08 (clarificações de prazo de sessão e limpeza; decisões G1–G15 de [tasks.md](tasks.md)) | **Spec**: [spec.md](spec.md)
**Feature Context**: `001-fullstack-foundation`, resolvido por `.specify/feature.json`.
O campo `BRANCH` retornado pelo setup é esse contexto, não uma troca de branch Git.
**Input**: “em specs\001-fullstack-foundation planejar l2 com a l1 entregue”.
**Status**: design da L2; nenhum código, migration, container ou teste de aplicação
é implementado/executado neste comando. L1/L2 correspondem a I1/I2 nos artefatos anteriores.

## Summary

**Construção do zero.** Nada foi publicado e não há dados a preservar. O que a L1
produziu (quatro camadas, usuários e permissões locais, marcador do banco, preflight
EF, health/OpenAPI, Compose e CI) é ponto de partida e pode ser alterado ou desfeito
livremente. O esquema do banco é criado por **uma migration inicial única**, gerada
do modelo completo descrito em [data-model.md](data-model.md), já com as convenções
da Constituição 2.2.0. Todo registro nasce com autoria e data reais.
[tasks.md](tasks.md) mantém seu histórico concluído e não é regenerado
por `/speckit-plan`. O próximo `/speckit-tasks` deve preservar esse histórico e
gerar somente o trabalho da L2 com identificadores novos.

A **L2** entrega login/logout HTTP por cookie, antiforgery, sessão persistida
revogável, `/me`, `/context`, autorização por concessão atual e procedimento restrito
de contas/permissões. Mantém **1 backend + 1 PostgreSQL por cliente**, sem catálogo,
conta global, escolha de banco por header, frontend, painel ou módulo de negócio.
A L3 continua responsável pela interface e extensão modular; L4, pela publicação
Dokploy/VPS, backups e restauração.

O design é orientado por [research.md](research.md) (revisão L2 D14–D21),
[data-model.md](data-model.md), [operações](contracts/operations.md) e
[ADR 0002](../../docs/adr/0002-sessoes-e-operacao-acesso-local.md).

## Technical Context

**Language/Version**: C# 14 / `net10.0`. SDK-base `10.0.401` com
`rollForward=latestFeature` e `allowPrerelease=false`, conforme `global.json`.
Não confundir SDK-base com a versão efetivamente selecionada no ambiente; registrar
`dotnet --version` na validação. Runtime/ASP.NET Core e Identity/EF Core `10.0.12`.

**Primary Dependencies**: pacotes existentes, provider/driver Npgsql `10.0.3`,
OpenAPI nativo, Cookies, Antiforgery, Data Protection e Rate Limiting do framework.
Identity continua com `IdentityUserContext<UsuarioAplicacao, Guid>` e concessões
diretas; sem RoleManager, Redis, JWT, mediator, biblioteca de CLI ou provedor externo.
Única dependência nova: `FluentValidation` e `FluentValidation.DependencyInjectionExtensions`
`12.2.0` (Apache-2.0, `net8.0`+), para validação de entrada HTTP conforme
[ADR 0003](../../docs/adr/0003-tratamento-de-erros-e-validacao.md); aprovação na
revisão exigida pela Constituição X. Não há atualização das versões existentes.
`Microsoft.Extensions.Hosting` `10.0.12` somente em `Platform.Operations` (G20, aprovada
2026-11-08; builder sem repassar argumentos de linha de comando) e referência direta a
`Microsoft.Extensions.Options` em Application (G19, já transitiva no grafo).

**Storage**: PostgreSQL `18.6`, banco exclusivo da instalação.
`AcessoTenantDbContext` tem **uma** migration inicial com marcador, contas,
permissões, `SessaoAutenticacao`, `RegistroOperacaoAcesso`, base auditável e
convenções de nomes (PK `Id<Entidade>`, FK `FkId<Entidade>`, constraints `pk_/fk_/
ix_/uq_/ck_`). A migration de rascunho existente no repositório é descartada e
regenerada a partir desse modelo. Bancos locais A/B são criados do zero.
Key ring em volume privado por instalação, não em novo banco.

**Testing**: `xunit.v3 4.0.2`, Microsoft.Testing.Platform e
Testcontainers.PostgreSql `4.16.0`; manter os três projetos de teste existentes.
Autenticação real com cookies/CSRF e PostgreSQL, relógio controlável por
`TimeProvider` (fonte única do instante atual em todo o código de produção, com
implementação de teste própria, sem pacote novo), barreiras para concorrência e
consumidor HTTP independente.

**Target Platform**: ASP.NET Core em Linux, Compose v2 e PowerShell no Windows.
Mesmo origin para a API e o frontend posterior. Produção exige HTTPS;
Development HTTP é exceção explícita somente em localhost.

**Project Type**: monólito modular; quatro projetos de produção existentes mais
um executável operacional mínimo, `Platform.Operations`, fora da imagem da API.
Esse host só atende FR-033; não é framework administrativo nem requisito retroativo da L1.

**Performance Goals**: SC-002 e SC-005 completos para a superfície backend;
SC-003 com **200 operações, 100 por instalação e pico medido >=20**.
Não se inventa meta de latência/throughput. SC-006/SC-008 acompanham a L2,
mantendo SC-009 como regressão sobre a migration inicial.

**Constraints**: Português Técnico no código, `Id*`, `Excecao*`, `StringsConexao`,
XML summaries e `HasComment` nos campos persistidos. Propriedades C# herdadas do
Identity (`Id`, `UserId`) não são renomeáveis; suas colunas são mapeadas para
`IdUsuario`/`FkIdUsuario`. Propriedades públicas JSON já desenhadas permanecem.
Constituição 2.2.0: `Id*` é chave técnica UUID v7, nunca código de negócio; FK
persistida é `FkId*` (sem vazar para Application/DTO); instantes são
`DateTimeOffset` UTC/`timestamptz` em microssegundos vindos de `TimeProvider`.
Configuração do produto usa `CodigoAplicacao` (texto, não `Id*`).
Convenções e exceções em [data-model.md](data-model.md#2-convenções-de-nomes-no-banco).

**Scale/Scope**: cinco operações HTTP novas e dois health checks preservados;
duas instalações reais independentes nos testes; nenhuma conta ou senha padrão.
Procedimentos operacionais têm alvo explícito e autorização própria, não cookie de usuário.

## Constitution Check

*GATE: conferir antes da pesquisa e depois do design.*

Constituição: [constitution.md](../../.specify/memory/constitution.md), versão 2.3.0
(as convenções de nomes, Id e data/hora citadas como “2.2.0” permanecem vigentes).
PASS refere-se ao **desenho**, nunca a execução ou liberação já comprovadas.
Reavaliação 2026-11-08 (D21): prazo configurável e `sessao limpar` mantêm todos os
princípios em PASS e não exigem dependência nova nem exceção — ver notas por linha.

| Princípio | Pré-pesquisa | Pós-design | Evidência / gate de implementação |
|---|---|---|---|
| I. Arquitetura modular | PASS | PASS | Portas em Application; HTTP na API; Identity/EF em Infrastructure; host operacional só compõe casos de uso. |
| II. Contratos explícitos | PASS | PASS | APIs de identidade e contexto sem acesso a módulos/DbContexts externos. |
| III. Stack | PASS | PASS | Stack/pins existentes; mecanismos nativos; sem frontend nesta entrega. |
| IV. Isolamento | PASS | PASS | Instalação fixa, marcador antes de dados de acesso, chave/cookie/sessão tenant-local, testes A/B. |
| V. API | PASS | PASS | Contrato atual preservado; alvo L2 separado; Problem Details e nenhuma resposta HTML de autenticação. Categorias de falha, códigos estáveis e JSON estrito conforme ADR 0003 (bloco P1c). |
| VI. Segurança | PASS condicionado a P0 | PASS condicionado a P0 | Runtime sem owner/DDL, cookies+CSRF, autorização a cada operação, sessão revogável e tooling separado. Prazo limitado a 480 min e validado na inicialização; DELETE de sessões só para o operador individual; registro de limpeza sem ids de sessões/contas. |
| VII. Qualidade | PASS | PASS | Migration inicial única (banco novo, reaplicação, histórico desconhecido rejeitado); não há versão anterior publicada a atualizar. PostgreSQL real, revogação/concorrência e gates do backend. Limites 5/480 vs 4/481 e retenção 1/365 vs 0/366, fronteira do corte, falha injetada na limpeza e um registro por execução. |
| VIII. Infraestrutura | PASS condicionado a P0 | PASS condicionado a P0 | Compose local restrito, key ring privado persistente; HTTPS antes de acesso não local. Produção continua L4. |
| IX. Manutenibilidade | PASS | PASS | Mecanismos nativos e ADR 0002; sem abstrações genéricas. Regras 2.2.0 de Id, FK, nomes de constraints e data/hora com exceções registradas e testes de modelo/arquitetura. Novos nomes em Português Técnico (`PrazoAbsolutoMinutos`, `RetencaoSessoesDias`, `InstanteCorte`, `ck_sessoes_autenticacao_prazo`); corte calculado do `TimeProvider`; delete rastreado, sem `ExecuteDelete`. |
| X. Evolução | PASS | PASS | FluentValidation é a única dependência nova, justificada na ADR 0003 (alternativa nativa `AddValidation()` avaliada). Construção do zero explicitada (nada publicado, sem dados); login só liberado com todos os controles; L3/L4 preservadas. A partir da primeira publicação, mudanças de esquema passam a ser aditivas. |

**ADRs após 2026-11-08.** [ADR 0002](../../docs/adr/0002-sessoes-e-operacao-acesso-local.md)
continua atendida: expiração absoluta sem sliding/refresh, revogação na próxima
operação, host operacional sem HTTP e auditoria atômica; a ADR recebeu revisão
substituindo “30 minutos” por prazo configurável (padrão 30) e registrando a limpeza
por operador, sem job. [ADR 0003](../../docs/adr/0003-tratamento-de-erros-e-validacao.md)
não muda: nenhuma rota, status ou código HTTP novo; configuração inválida é falha
de inicialização (não Problem Details) e `sessao limpar` usa resultado/código
estável do host (`RETENCAO_INVALIDA`, `CONFLITO_CONCORRENTE`), sem exceção como
fluxo de regra esperada. Desde G29, `CONFLITO_CONCORRENTE` fica restrito a conflito
real; exceção inesperada nas operações de conta/permissão é `FALHA_INTERNA` e email
duplicado é `EMAIL_JA_CADASTRADO` (ver [operations.md](contracts/operations.md)).

### P0 — pré-requisitos bloqueantes, sem exceção constitucional

A conclusão informada da L1 não dispensa verificar a configuração usada na L2.
A leitura do Compose/exemplo atual mostra a conexão runtime exemplificada com
`owner_cliente_a`, PostgreSQL publicado para depuração e ausência de key ring.
Não se assume separação de privilégios ou proteção de sessão por existir um comentário.

Antes de habilitar login, as tarefas L2 devem:

1. Separar efetivamente runtime, operador de acesso e owner de migrations por
   credencial/grants; remover credenciais utilizáveis dos exemplos e logs.
   API jamais recebe conexão de operador/owner; provar que runtime não executa DDL.
2. Deixar PostgreSQL sem publicação pública; acesso de depuração, quando necessário,
   limitado explicitamente ao loopback. A/B usam portas, volumes e redes próprios.
3. Persistir key ring separado por instalação; produção exige certificado externo
   para proteção em repouso e HTTPS. O Compose atual é Production sobre HTTP:
   definir modo local Development/loopback ou fornecer HTTPS, nunca relaxar cookies
   seguros silenciosamente para fazê-lo funcionar.
   Em Docker, Development HTTP local usa publicação da API no loopback do host;
   o listener dentro do container pode aceitar sua interface de rede. Não exigir
   `RemoteIpAddress` loopback dentro do container (o gateway Docker não é loopback)
   nem confiar só em Host/X-Forwarded-For para provar acesso local.
4. Validar os scripts de inicialização sempre em banco local criado do zero:
   o `infra/docker/init-db.sh` (substitui o antigo `init-db.sql`; lê variáveis e cria
   `<banco>_owner`, `<banco>_runtime`, `<banco>_operador` e o login individual do
   operador, com CONNECT/USAGE apenas — GRANTs por tabela vêm da migration, G6) só roda
   na primeira criação do volume, então o ambiente local de desenvolvimento é montado
   com volumes novos (`down --volumes` faz parte do setup).
5. Não considerar `.env.example` uma configuração pronta. Secrets locais ficam no
   `infra/.env.local` privado (mesmo modelo de variáveis do Dokploy, G4); cada serviço
   recebe somente a própria credencial, o exemplo versionado não tem senha utilizável e
   `${VAR:?}` falha quando falta valor, sem fabricar valores ausentes.

Não há violação aceita no design. Falha de P0 **bloqueia implementação/liberação da
superfície autenticada**, não autoriza um login “provisório”. O comando atual é documental.

## Project Structure

### Documentation (this feature)

```text
specs/001-fullstack-foundation/
├── spec.md                       # requisitos mantidos
├── plan.md                       # plano vigente L2
├── research.md                   # decisões históricas + revisão L2
├── data-model.md                 # modelo completo L2 e convenções
├── quickstart.md                 # validação L1 e roteiro futuro L2
├── tasks.md                      # histórico L1 preservado
└── contracts/
    ├── openapi.json              # contrato L2 entregue (promovido na Parada L2-2)
    └── operations.md             # contratos EF e procedimento restrito L2
```

### Source Code (repository root)

```text
backend/src/
├── Platform.Domain/              # regras puras, sem Identity/EF/HTTP
├── Platform.Application/
│   ├── Configuration/            # OpcoesInstalacaoTenant existente
│   ├── Tenancy/                  # verificação de identidade existente
│   └── Access/                   # casos de uso e portas L2
├── Platform.Infrastructure/
│   ├── Access/                   # adaptadores Identity e operação restrita L2
│   └── Persistence/TenantAccess/
│       ├── AcessoTenantDbContext.cs
│       ├── Entities/             # UsuarioAplicacao, PermissaoUsuario, IdentidadeBancoTenant + L2
│       └── Migrations/           # migration inicial única, gerada do modelo completo
├── App.Api/
│   ├── Access/                   # endpoints, cookies/store e antiforgery L2
│   ├── Diagnostics/              # Problem Details existente, ampliado
│   └── Health/                   # sem lookup de sessão
└── Platform.Operations/           # novo host pontual L2, sem servidor HTTP
backend/tests/
├── Platform.UnitTests/
├── Platform.IntegrationTests/
└── Platform.ContractTests/
infra/                            # Compose/perfis/secrets/key ring, sem deploy real
docs/adr/                         # 0001 preservado; 0002 registra L2
```

**Structure Decision**: Application define portas específicas de autenticação,
sessão, consulta de contexto e manutenção de acesso; não recebe tipos Identity,
`HttpContext` ou `AuthenticationTicket`. API adapta cookies/`ITicketStore` e DTOs;
Infrastructure persiste tickets já protegidos como bytes e implementa Identity.
O adapter de store não captura DbContext scoped nas options: resolve escopo por chamada.
`Platform.Operations` referencia Application e Infrastructure só na composição.
Testes arquiteturais devem abranger o novo host.

`EntidadeAuditavel` e `IEntidadeAuditavel` em Domain definem a base comum de
metadados, sem HTTP/Identity/EF nem PK genérica (permissão tem PK composta).
`PermissaoUsuario` e `SessaoAutenticacao` herdam essa base.
`UsuarioAplicacao` mantém `IdentityUser<Guid>` e implementa o mesmo contrato;
C# não permite uma segunda classe-base. Mapeamento e preenchimento compartilhados
em Infrastructure garantem o mesmo comportamento sem alterar herança Identity.
A base não tem PK nem exclusão lógica.

Preenchimento ([D19](research.md#d19--preenchimento-de-auditoria-datahora-e-identificadores-2026-11-06)):
porta `IContextoOperacao` em Application (`IdOperacao`, autor, `InstanteOperacao`
lido uma vez do `TimeProvider`), implementada por API, `Platform.Operations` e
tooling; `InterceptadorAuditoria` em Infrastructure preenche metadados e incrementa
`Versao` somente em sessões. Token de concorrência por entidade
([D20](research.md#d20--tabelas-auxiliares-identity-concorrência-por-entidade-relógio-dos-uuids-e-troca-de-sessão-2026-11-07)):
conta usa `ConcurrencyStamp` do Identity, concessão é imutável (INSERT/DELETE) e
sessão usa `Versao`. Entidades auditáveis só são alteradas rastreadas; `ExecuteUpdate`/
`ExecuteDelete` são proibidos nelas. Helper puro `InstanteUtc` em Domain normaliza
para UTC/microssegundos e valida; não lê relógio. Helper puro `IdentificadorV7` gera
UUID v7 a partir do `InstanteOperacao`; PKs `Guid` sem geração pelo provider.

Nomes no banco ([data-model §2](data-model.md#2-convenções-de-nomes-no-banco)):
PK `Id<Entidade>`, FK `FkId<Entidade>[Papel]` só na persistência, colunas PascalCase,
tabelas snake_case, constraints/índices `pk_/fk_/ix_/uq_/ck_` em minúsculas por
convenção central no `OnModelCreating`, validadas por teste de modelo.

## Phase 0 — pesquisa e decisões

Revisão [D14–D21](research.md#revisão-l2--2026-11-04) resolve os pontos de integração
(D21: prazo de sessão configurável e limpeza auditada, clarificações de 2026-11-08):
pipeline cookie/CSRF, revogação sob corrida, persistência de chaves, autorização do
operador, (D19) mecanismo de auditoria, padrão de data/hora, identificadores,
nomes no banco e migration inicial única e (D20) tabelas auxiliares Identity,
concorrência por entidade, relógio dos UUIDs e troca atômica de sessão no login.
As premissas de preservação/upgrade da L1 em D14, D16 e D17 estão marcadas como
substituídas pela construção do zero. Não há esclarecimentos técnicos pendentes.
Exemplos do source browser/branch móvel não provam comportamento do binário fixado:
as transições de store serão verificadas na implementação com o runtime utilizado.

## Phase 1 — design e contratos

### Pipeline HTTP e autorização

1. Tratar erros/correlação com o mecanismo existente, ampliado conforme
   [Tratamento de erros e validação](#tratamento-de-erros-e-validação); aceitar forwarded headers
   somente de proxies explicitamente confiáveis. Limiter usa IP efetivo, não header livre.
2. Separar health/OpenAPI/rotas não mapeadas do fluxo de sessão. `AllowAnonymous`
   sozinho não impede autenticação automática. Health ignora cookies e header tenant;
   liveness não toca banco/key ring; readiness verifica banco/versão e proteção de chaves.
3. Em `/api/v1` mapeado, exigir exatamente um `X-Tenant-Id` UUID não vazio.
   Validar instalação e marcador antes de consultar conta/sessão; nunca resolver outro banco.
   Runtime/readiness exigem a cadeia de migrations conhecida completa, sem
   pendentes nem IDs desconhecidos; apenas “nenhuma pendente” não detecta banco
   de versão futura. Tooling aceita um prefixo válido dessa cadeia para upgrade
   (relevante a partir da segunda migration); qualquer ID fora da cadeia conhecida
   é rejeitado.
4. Autenticar cookies apenas onde necessário: login/antiforgery permitem ausência de
   sessão; `/me`, `/context` e logout exigem sessão local válida.
   Cookie não desprotegível não provoca lookup nem autentica.
5. Conferir conta ativa, lockout, stamp, sessão, epoch e expiração a cada operação.
   `/me` autoriza a leitura da própria identidade pela correspondência conta/sessão;
   `/context` exige também `foundation.context.read` vigente. Logout só pode revogar
   a própria sessão, sem exigir permissão de contexto ou instalação ativa.
   GET antiforgery deve permitir obter o token dessa sessão mesmo com instalação
   inativa, sem liberar `/me`, `/context` ou dados.
6. Validar antiforgery explicitamente em login/logout JSON. Respostas de acesso e
   erros usam `Cache-Control: no-store`; dados de senha/cookie/ticket nunca são logados.

Header divergente: 401 genérico no login; 403 nas demais operações com header válido.
Header ausente/duplicado/inválido: 400. Sessão ausente/inválida: 401.
Instalação inativa: login 401; `/me` e `/context` com sessão válida retornam 409.
Login que perde a disputa pela mesma sessão anterior: 409 SESSION_CONFLICT.
Logout de sessão própria ainda válida permanece permitido, com CSRF, e só confirma
204 após revogação persistida; GET antiforgery não exige instalação ativa.
Inativação da instalação suspende consultas, mas não revoga sessões ou muda epoch.
Após reativar, só sessões ainda válidas/não revogadas podem retomar;
o prazo fixado na emissão continua contando, sem reiniciar ou pausar.
Marcador divergente ou dependência indisponível: 503 seguro; nunca tentar outro alvo.
A ordem detalhada e os códigos constam em [operations.md](contracts/operations.md).

### Tratamento de erros e validação

Decisões em [ADR 0003](../../docs/adr/0003-tratamento-de-erros-e-validacao.md);
aplicam-se às rotas L2 e a todo módulo futuro.

1. Três categorias de falha: entrada malformada (400 `INPUT_INVALID` + `errors`),
   regra de negócio esperada (`Resultado`/`Resultado<T>` → 403/404/409/422) e falha
   inesperada (exceção `Excecao*` → `ManipuladorExcecaoSeguro` → 500/503).
2. `Erro`, `TipoErro`, `Resultado` e `Resultado<T>` em `Platform.Domain/Resultados`,
   sem dependências. Catálogo por módulo `Erros<Modulo>`; o `TipoErro` define o status.
3. JSON estrito: `UnmappedMemberHandling.Disallow`, `RespectNullableAnnotations`,
   `RespectRequiredConstructorParameters` e `RouteHandlerOptions.ThrowOnBadRequest = true`
   em todos os ambientes; `BadHttpRequestException` vira 400 sem ecoar valor nem
   mensagem original. `JsonUnmappedMemberHandling.Skip` só em `*.Integracoes.*`.
4. FluentValidation para DTOs `Requisicao*` em `Platform.Application`, aplicado por
   filtro no grupo `/api/v1`; sem banco, relógio ou regras assíncronas.
   Chaves de `errors` em camelCase sem `$.` (ex.: `tarefas[0].emailResponsavel`).
5. `ConfiguracaoProblemDetails` ganha fábricas para 400 (validação/desserialização,
   `TENANT_REQUIRED`, `CSRF_INVALID`), 401 (`TipoErro.NaoAutenticado`, restrito a
   `ErrosAcesso`, G5), 403, 404, 409, 422 e 429 (`RATE_LIMITED` + `Retry-After`). As rotas L2 continuam emitindo apenas os códigos de
   plataforma de [operations.md](contracts/operations.md); 422 e códigos
   `<MODULO>_<REGRA>` ficam disponíveis para módulos de negócio.
6. O login valida seu DTO pela mesma via, sempre antes de qualquer lookup de conta
   (o logout é `DELETE` sem corpo, G15). Ordem nas cinco rotas (G2 revisada, G9): rate
   limiter (só login) → `X-Tenant-Id` (400) → tenant coincide (401 login / 403 demais) →
   marcador/versão (503) → autenticação do cookie (logout sem sessão → 401) → corpo +
   FluentValidation (login) → CSRF → estado da instalação → credenciais/autorização.
   Detalhe em [operations.md §4](contracts/operations.md#ordem-de-validação).

**Rotas REST (G15)**: `GET /api/v1/antiforgery-token`, `POST /api/v1/sessions` (login,
201 Created), `DELETE /api/v1/sessions/current` (logout, 204), `GET /api/v1/me` e
`GET /api/v1/context`.

### Sessão e proteção

Cookie de produção `__Host-bps-session`, HttpOnly/Secure/SameSite=Lax,
Path `/`, sem Domain. Development HTTP localhost usa `bps-session`;
modo inseguro fora desse escopo é erro. Cookie antiforgery é separado.
O ticket contém só referências e stamp, não concessões permanentes.

Store usa referência protegida `IdTenant/EpocaAcesso/IdSessao`.
Login com sessão corrente válida faz a **troca atômica** (FR-008, clarificação
2026-11-07): numa única transação revoga a anterior com `Versao` esperada e insere
a nova; commit antes de emitir `Set-Cookie`. `StoreAsync` usa a unidade de trabalho
da requisição; o login nunca chama `RenewAsync` com a chave anterior (evita
reutilização de chave pelo handler). Conflito de `Versao` (outro login, logout,
Renew ou revogação concorrente) faz rollback e retorna 409 SESSION_CONFLICT;
falha de persistência faz rollback e retorna 503. Nos dois casos a anterior mantém
estado e prazo, nenhuma sessão é criada e repetir exige novo GET antiforgery.
Logout só responde 204 após commit da revogação.
Sessões em outros clientes/dispositivos da mesma conta permanecem independentes:
novo login rotaciona apenas a referência do cookie corrente; logout revoga somente
essa sessão. Reset, desativação, lockout e revogação operacional revogam todas
as sessões da conta, sem afetar outro tenant.
Expiração absoluta, sem sliding expiration, remember-me ou refresh. Prazo em
`Sessao:PrazoAbsolutoMinutos` (clarificação 2026-11-08,
[D21](research.md#d21--prazo-de-sessão-configurável-e-limpeza-auditada-de-sessões-2026-11-08)):
inteiro 5–480 inclusive, padrão 30 quando ausente; fora da faixa ou não inteiro
impede a inicialização (`ValidateOnStart`, diagnóstico seguro). Fixado na criação:
`ExpiraEm = CriadoEm + prazo configurado`, ambos do mesmo `InstanteOperacao`;
ticket e cookie usam esse `ExpiraEm`. Retrieve/Renew comparam com o `ExpiraEm`
gravado e nunca recalculam pela configuração atual. CHECK no banco limita
`ExpiraEm - CriadoEm` a 5–480 minutos.
Renew é atualização rastreada de sessão ativa com `Versao` esperada, nunca upsert,
nem extensão de prazo; só altera metadados se o ticket mudar.
Store não usa cache de autorização; erro de banco não é tratado como “sessão ausente”.
A API não remove sessões nem executa job em background; a remoção de registros
encerrados é o comando restrito `sessao limpar` (abaixo).

Identity valida senha/hash e lockout: cinco falhas por conta, 15 minutos de bloqueio,
com atualização concorrente segura. `GerenciadorUsuariosAplicacao : UserManager<UsuarioAplicacao>`
em Infrastructure sobrescreve `AccessFailedAsync`/`IsLockedOutAsync` com `TimeProvider` +
`InstanteUtc`, porque o `UserManager` 10.0.12 lê `DateTimeOffset.UtcNow` no lockout (G13). Ao confirmar o bloqueio, revogar todas as
sessões da conta e atualizar stamp na mesma transação; fim do bloqueio só permite
novo login, nunca retomada de sessão antiga. Limiter nativo por IP efetivo (uma instalação por deploy): cinco pedidos
de login/minuto, janela fixa, fila zero, 429/Retry-After. Login inválido não enumera
email/tenant; não criar contas ou permissões por startup/login.

### Operação restrita e evolução do banco

Host pontual L2 cria/desativa/reativa contas, redefine senha, concede/retira a
permissão declarada, revoga sessões de uma conta e remove sessões encerradas além
da retenção (`sessao limpar`). Usa UserManager, alvo explícito,
credencial individual de operador distinta, preflight e auditoria persistida.
Login PostgreSQL por operador, não compartilhado, é atribuído ao responsável
autenticado e gravado em `LoginOperador` com a correlação. Não aceita
`--actor` como autorização, senha em argumentos, conta global ou chamadas por HTTP.
Contrato e política de senha em [operations.md](contracts/operations.md).

`sessao limpar` (clarificação 2026-11-08, [operations §5](contracts/operations.md#sessao-limpar)):
retenção `OperacaoAcesso:RetencaoSessoesDias`, inteiro 1–365, padrão 30, sem argumento
que a altere; inválida encerra antes de conectar. Remove somente sessões revogadas
com `RevogadaEm < corte` ou não revogadas com `ExpiraEm < corte`
(`corte = InstanteOperacao - retenção`); sessão válida nunca é elegível. Uma
transação com delete rastreado (`Versao` esperada, sem `ExecuteDelete`) e registro;
falha faz rollback total e grava registro `Falha` separado. Exatamente um
`RegistroOperacaoAcesso` imutável por execução com alvo validado, atribuído ao
`LoginOperador` individual, com retenção, instante de corte, quantidade removida,
resultado e correlação, sem ids de sessões ou contas e sem autoria Sistema.
Somente o operador tem DELETE em sessões; runtime não. Sem job na API.

Contas/permissões/sessões possuem autor/data da criação e última alteração e
correlação, preenchidos centralmente por contexto confiável de execução.
Identidade do operador é o login individual verificado; usuário vem da
autenticação/credencial validada; ação automática ou anônima é identificada como
tal, sem tomar email/Id externo como autoria. Dados e metadados persistem na mesma
transação. Histórico operacional é append-only, não herda base mutável.

O esquema nasce de uma migration inicial única com todo o modelo
([data-model §8](data-model.md#8-migration-inicial-e-validação)). Lockout fica
habilitado em toda conta desde a criação. Bancos locais A/B são criados do zero.
Validar banco novo, reaplicação, rejeição de histórico desconhecido, alvo errado e
runtime sem DDL.
Executar com owner via factory/preflight existentes; sem seed de usuários.
GRANTs por tabela para `<banco>_runtime` e `<banco>_operador` são emitidos pela própria
migration inicial, com nomes derivados de `current_database()` (G6).
Registro de preparação/migration na L2 (FR-014, G14): exit code e diagnóstico seguro do
preflight mais `foundation.__EFMigrationsHistory` do próprio banco; auditoria persistente
de preparação/migration fica para a L4, com backup/restauração.
Downgrade de imagem não reverte automaticamente os dados.

### Aceite e ordem de execução para a próxima etapa

| Rastreabilidade L2 | Evidência exigida |
|---|---|
| US2; FR-005 a FR-008, FR-033; SC-002 | Login/logout, sessão vinculada, revogação/expiração, conta/permissão e operação restrita independentes em A/B. Prazo configurável: 5 e 480 aceitos, ausente = 30, 4/481/não inteiro bloqueiam a inicialização; expiração no `ExpiraEm` fixado na emissão, inclusive após mudar a configuração. Limpeza: com retenção padrão remove 100% das encerradas há mais de 30 dias e 0% das válidas ou encerradas há 30 dias ou menos (fronteira exata do corte); um registro por execução, inclusive sem remoções e com falha injetada, atribuído ao operador; retenção 0/366 rejeitada. |
| US3; FR-009, FR-011 a FR-016; SC-003 | Marcador/estado antes do acesso, migration inicial única, alvo errado bloqueado e carga 200/100/100/pico >=20. |
| US5; FR-020 a FR-023; SC-005 | Consumidor independente usa somente contrato, cookie jar/CSRF e distingue erros; sem frontend. |
| US1/US6; FR-002/003, FR-024 a FR-028; SC-006/008 | Execução reproduzível, configuração explícita, CI bloqueante e documentação/evidências da superfície L2. |
| FR-034; SC-009 | Regressão da base sobre a migration inicial: estrutura/persistência A/B, reaplicação e backend executável, sem credenciais padrão. |
| NFR-001 a NFR-008 e NFR-010 | Secrets/menor privilégio, isolamento concorrente, reprodutibilidade, diagnósticos seguros e compatibilidade contratual. |

FR-004/017 a FR-019 e SC-001/004 continuam L3; provisionamento/ativação no
Dokploy de FR-010, FR-029 a FR-032, NFR-009 e SC-007 continuam L4.
L2 verifica o estado configurado e prepara proteção de sessões, sem declarar
esses critérios futuros concluídos.

| Bloco | Depende de | Resultado verificável |
|---|---|---|
| P0 — configuração/segredos/privilégios | L1 | Runtime/operador/owner distintos, bancos isolados e modo local/HTTPS explícito. |
| P1a — fundações de modelo | P0 | `InstanteUtc`, `IdentificadorV7`, `TimeProvider` registrado, `IContextoOperacao`, `EntidadeAuditavel`/`IEntidadeAuditavel`/`IEntidadeVersionada`, `InterceptadorAuditoria` com token por entidade; código existente alinhado ao modelo (sem `DateTimeOffset.UtcNow` em `PermissaoUsuario`/preflight, validação de offset, sem `ConcedidaEm`, concessão sem `Versao`, FKs `FkIdUsuario`/`FkIdTenant`, colunas Identity e auxiliares mapeadas, PKs `Guid` com `ValueGeneratedNever`, `CodigoAplicacao` em código/env/Compose/testes); convenção central de nomes de constraints; testes de arquitetura (relógio, `Guid.NewGuid`/`CreateVersion7()` sem argumento, `ExecuteUpdate`) e de modelo (PK `Id*`, FK `FkId*`, UUID v7 com timestamp do relógio de teste, exceções em lista fechada, nomes de constraints ≤63, catálogo `CodigosPermissao`). |
| P1b — migration | P1a | Migration de rascunho descartada; migration inicial única gerada do modelo completo; bancos A/B criados do zero; SC-009 e rejeição de histórico desconhecido. |
| P1c — erros e validação (ADR 0003) | P1b (execução sequencial) | `Resultado`/`Resultado<T>`/`Erro` no Domain; `ConfiguracaoProblemDetails` com 400/401/403/404/409/422/429 e ordem de validação G2; JSON estrito com `ThrowOnBadRequest`; FluentValidation com filtro no grupo `/api/v1`; testes de contrato (formato, chaves camelCase, nenhum valor ecoado) e de arquitetura (validador por `Requisicao*`, validador sem I/O, padrão/unicidade de códigos, `Skip` só em `*.Integracoes.*`). Executado após P1b (decisão de execução sequencial, 2026-11-08). |
| P2 — sessão e controles HTTP | P1, P1c | Cookies/store, CSRF, limiter, autorização e saúde isolada. Opção `Sessao:PrazoAbsolutoMinutos` validada na inicialização (5/480 aceitos, 4/481/não inteiro rejeitados, ausente = 30), `ExpiraEm` fixado na emissão e CHECK de prazo; runtime sem DELETE em sessões e sem job de limpeza. |
| P3 — operação restrita | P2 (execução sequencial; reutiliza adaptadores de sessão/DI) | Casos de uso autorizados/auditáveis sem endpoint administrativo, incluindo `sessao limpar`: retenção 1–365 (padrão 30), critério de corte com fronteira exata, delete rastreado atômico e um registro imutável por execução (sucesso, zero removidas e falha) com campos de limpeza e CHECK. Executado após P2. |
| P4 — contratos e aceite | P2 e P3 | SC-002/003/005, regressões da base (SC-009), CI/documentação e consumidor independente. |

SC-003 usa operações sintéticas exclusivamente no host de testes, passando pelos
mesmos controles de acesso e persistência. A distribuição não ganha CRUD de teste.
Medir operações realmente em voo por barreiras/contadores: 200 tarefas agendadas
não provam pico >=20. Provar resultados válidos e efeitos de rollback, não só negativas.

Ao implementar, atualizar `openapi.json` e o documento servido para L2 no mesmo
conjunto de mudanças dos endpoints e testes; não anunciar rotas antes disso.
O contrato futuro preserva nomes JSON camelCase existentes. Apenas a ausência
intencional das cinco rotas L2 muda de 404 para o comportamento novo;
health, erros e rotas desconhecidas mantêm seu contrato.
Readiness L2 acrescenta intencionalmente key ring à condição de prontidão:
chaves indisponíveis causam 503 mesmo com banco pronto. Preservar formato/status
da superfície L1 não significa manter exatamente seu conjunto de dependências.

## Complexity Tracking

Sem exceções constitucionais. O novo host operacional e a auditoria têm consumidor
concreto (FR-033) e evitam expor uma API administrativa ou usar SQL manual para senhas.
Reutilizam as quatro camadas, Identity e o único banco local; ver ADR 0002.

Exceções registradas a regras não críticas (Constituição IX), sem prazo por serem
permanentes e justificadas, detalhadas em [data-model.md](data-model.md#3-política-de-identificadores):
`IdUnico` inteiro do marcador; `CodigoPermissao` na PK da concessão (catálogo fechado
no código-fonte, nunca reutilizado); tabelas auxiliares Identity com PK `int` ou
composta textual imposta pelo framework, mantidas vazias, sem grants de escrita e
sem uso na L2 (D20). Propriedades Identity `Id`/`UserId` seguem o
padrão pelo nome da coluna, não da propriedade.
Gerar o esquema numa migration inicial única não é exceção à Constituição X: o
projeto está em construção, nada foi publicado e não há dados a migrar.
O próximo passo é gerar tarefas L2; este comando termina no design da Phase 1.
