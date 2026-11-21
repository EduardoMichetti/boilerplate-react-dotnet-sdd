---

description: "Tasks L2 (autenticação e acesso local ao tenant) + histórico L1 preservado"
---

# Tasks: Fundação full-stack reutilizável — L2 (autenticação e acesso local ao tenant)

**Input**: `/specs/001-fullstack-foundation/` — [plan.md](plan.md) (L2, revisão 2026-11-08), [spec.md](spec.md), [research.md](research.md) D14–D21, [data-model.md](data-model.md), [contracts/operations.md](contracts/operations.md), [contracts/future-openapi.json](contracts/future-openapi.json), [quickstart.md](quickstart.md), [ADR 0002](../../docs/adr/0002-sessoes-e-operacao-acesso-local.md), [ADR 0003](../../docs/adr/0003-tratamento-de-erros-e-validacao.md).

**Tests**: incluídos — exigidos por FR-025, FR-026, FR-027, SC-002, SC-003, SC-005 e pelo plano (PostgreSQL real, `TimeProvider` controlável, barreiras de concorrência).

**Organization**: o histórico L1 (T001–T039, T035 reservado) está preservado integralmente na seção [Histórico L1](#histórico-l1--recorte-i1-concluído). A L2 usa IDs novos a partir de **T040**, com as fases numeradas a partir de **9** para não colidir com as fases L1. Blocos do plano: P0 → Phase 10, P1a → Phase 11, P1b → Phase 12, P1c → Phase 13, P2 → Phase 14, P3 → Phase 15, P4 → Phases 16–18.

**Construção do zero** (clarificação 2026-11-06): nada foi publicado e não há dados. O código L1 é ponto de partida alterável; a migration de rascunho é descartada e uma migration inicial única é gerada do modelo completo.

## Decisões registradas na geração destas tasks (2026-11-08)

Gaps encontrados durante a geração e resolvidos com o mantenedor. Valem como requisito das tasks abaixo. T042 os propaga para os artefatos de design.

| # | Gap | Decisão |
|---|---|---|
| G1 | Aprovação de FluentValidation (Constituição X) pendente | **Aprovada** pelo mantenedor em 2026-11-08; ADR 0003 passa a “aceita”. |
| G2 | Ordem entre corpo, CSRF e tenant não definida em operations §4 | Cinco rotas L2: rate limiter (só login) → `X-Tenant-Id` presente/formato (400) → tenant coincide (401 `AUTH_INVALID` no login / 403 `TENANT_ACCESS_DENIED` nas demais) → marcador/versão (503 `DEPENDENCY_UNAVAILABLE`) → autenticação do cookie de sessão, quando presente → corpo JSON + FluentValidation (400 `INPUT_INVALID`, login/logout) → CSRF (400 `CSRF_INVALID`, login/logout) → estado da instalação → credenciais/autorização. Limiter, tenant e marcador são middleware (rodam antes da autenticação e do binding). **Revisão 2026-11-08 (remediação do `/speckit-analyze`)**: a versão anterior colocava o marcador depois do CSRF, mas o token antiforgery é vinculado à identidade autenticada, cuja leitura consulta a sessão no banco; como marcador/versão DEVEM ser conferidos antes de consultar conta/sessão (plan Phase 1 item 3; operations §4), o marcador passa a preceder autenticação, corpo e CSRF. A precedência corpo → CSRF é mantida. Logout sem sessão válida: 401 `AUTH_REQUIRED` logo após a autenticação (G9). |
| G3 | Gravações sintéticas de SC-003 | Rota e tabela sintéticas **somente no projeto de testes**, protegidas por sessão real + `foundation.context.read`; nada entra na API distribuída. |
| G4 | Mecanismo local de segredos | **Variáveis no `.env.local` privado** (mesmo modelo do Dokploy). Cada serviço recebe somente a própria credencial; o exemplo versionado não tem senha utilizável; `${VAR:?}` falha quando falta valor. |
| G5 | 401 sem `TipoErro` na ADR 0003 | Novo `TipoErro.NaoAutenticado` → 401, de uso restrito ao catálogo de plataforma `ErrosAcesso` (teste de arquitetura). |
| G6 | GRANTs por tabela após a migration | Emitidos **pela própria migration inicial**, com nomes de roles derivados do banco atual: `<banco>_owner`, `<banco>_runtime`, `<banco>_operador` (grupo NOLOGIN). Logins individuais de operador são membros de `<banco>_operador`. Roles criadas antes pelo `init-db.sh` (Compose) ou pela fixture (testes). |
| G7 | Defaults menores (aceitos como recomendados) | Cookie antiforgery `__Host-bps-csrf` (produção) / `bps-csrf` (Development localhost). Forwarded headers desligados na L2: chave `CabecalhosEncaminhados:ProxiesConfiaveis` vazia = não processar `X-Forwarded-*`. `applicationVersion` = `AssemblyInformationalVersion` do `App.Api`. Enums persistidos como texto com CHECK. Formato de `CodigoPermissao`: `^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$`. `TipoOperacao` = `CriarConta`, `DesativarConta`, `ReativarConta`, `RedefinirSenha`, `RevogarSessoes`, `ConcederPermissao`, `RevogarPermissao`, `LimparSessoes`. |
| G8 | Emenda 2.3.0 da Constituição (princípio V) sem aprovação registrada (Governance) | **Aprovada** pelo mantenedor em 2026-11-08 (segunda rodada do `/speckit-analyze`). T041 registra a aprovação na ADR 0003; T042 remove o Sync Impact Report, que deixa de ter pendências. |
| G9 | Posição do 401 do logout sem sessão na ordem G2 | Logout sem sessão válida responde **401 `AUTH_REQUIRED` logo após a autenticação**, antes de corpo e CSRF (`RequireAuthorization` com autorização de recurso próprio; coerente com T107). Login e GET antiforgery permitem ausência de sessão e mantêm corpo → CSRF → credenciais. |
| G10 | Opções numéricas opcionais repassadas vazias pelo Compose | Valor vazio (`""`) em `Sessao:PrazoAbsolutoMinutos` e `OperacaoAcesso:RetencaoSessoesDias` equivale a **ausente** (usa o padrão 30); qualquer outro valor segue a validação estrita (inclusive `" 30"` rejeitado). |
| G11 | Limiter e relógio nos testes HTTP | O limiter usa janela de tempo real, não controlada por `TimeProvider`: a `FabricaApiInstalacao` permite sobrescrever limite/partição para as suítes que não testam o limiter; T093 usa a política real. `RemoteIpAddress` nulo cai numa partição única fixa (nunca sem limite). Identity (lockout) e cookie handler DEVEM usar o `TimeProvider` do DI. |
| G12 | “Cada execução” de `sessao limpar` gera um registro | Execução auditável começa após operador autenticado e alvo validado. Configuração inválida (encerra antes de conectar) e login não autorizado (negado antes de mutação, sem INSERT) não geram registro; o diagnóstico vai para stderr com exit não zero. Spec FR-033 ajustada. |
| G13 | `UserManager` do Identity 10.0.12 lê `DateTimeOffset.UtcNow` (sem `TimeProvider`) no lockout | `GerenciadorUsuariosAplicacao : UserManager<UsuarioAplicacao>` em Infrastructure sobrescreve `AccessFailedAsync` e `IsLockedOutAsync` usando `TimeProvider` + `InstanteUtc` (`LockoutEnd` em UTC, microssegundos). Registrado por `AddUserManager<>` em T100 e usado por T098/T114; o cookie handler usa `AuthenticationSchemeOptions.TimeProvider`. Nenhum caminho de produção grava `LockoutEnd` pelo relógio do sistema. |
| G15 | Rotas de sessão em estilo RPC (`/auth/login`, `/auth/logout`, `/auth/antiforgery`) contrariam o padrão REST pedido pelo mantenedor (início do `/speckit-implement`, 2026-11-08) | Rotas como recurso: `GET /api/v1/antiforgery-token` (200, operationId `getAntiforgeryToken`), `POST /api/v1/sessions` (login → **201 Created**, corpo `tenantId`/`expiresAt`, operationId `createSession`), `DELETE /api/v1/sessions/current` (logout → 204, operationId `deleteCurrentSession`); `/api/v1/me` e `/api/v1/context` mantidas (recursos singleton). Logout sem corpo JSON (DELETE): a etapa “corpo JSON + FluentValidation” de G2 aplica-se só ao login; o logout valida CSRF pelo header. T042 propaga para `future-openapi.json`, `operations.md`, `plan.md` e `quickstart.md`; nas tasks abaixo, “login”/“logout”/“GET antiforgery” referem-se a essas rotas. |
| G16 | A imagem `postgres:18.6` exige senha do superusuário; T045/T046 só listavam as variáveis do `init-db.sh` | Variável privada `SENHA_SUPERUSUARIO_POSTGRES` no `.env.local`, repassada como `POSTGRES_PASSWORD` (`${VAR:?}`) **somente** ao serviço `postgres`; `POSTGRES_HOST_AUTH_METHOD=trust` rejeitado (daria superusuário à API na mesma rede). |
| G17 | Chave de `errors` quando a falha de desserialização não tem campo (JSON malformado, corpo vazio, raiz de tipo errado: `JsonException.Path = "$"`) | Chave `"$"` (convenção do `ValidationProblemDetails` do ASP.NET Core; não é o prefixo `$.` proibido), mensagem genérica pt-BR sem ecoar conteúdo. Erros de campo seguem sem prefixo (`email`, `itens[0].nome`). Registrar na ADR 0003 D6 e na descrição de `errors` do contrato. |
| G18 | No caminho de exceção (JSON malformado, 500, 503) o `ExceptionHandlerMiddleware` do ASP.NET Core força `Cache-Control: no-cache,no-store` (+ `Pragma: no-cache`, `Expires: -1`), divergindo do valor literal `no-store` do contrato | Contrato passa a exigir que `Cache-Control` **contenha** a diretiva `no-store` (resposta não cacheável), aceitando diretivas adicionais mais restritivas; testes verificam `NoStore`. Sem middleware para reescrever o cabeçalho (frágil à ordem interna de callbacks do framework). |
| G19 | T060 exige `IValidateOptions` em `Platform.Application`, que não referenciava `Microsoft.Extensions.Options` | Referência direta a `Microsoft.Extensions.Options` 10.0.12 em Application. Pacote do próprio .NET, já presente no grafo como transitivo de Infrastructure/API: nenhuma biblioteca nova entra no build. Valida configuração na inicialização; não se confunde com FluentValidation (entrada HTTP). |
| G20 | `Platform.Operations` (T115, `Host.CreateApplicationBuilder`) precisa de hosting e configuração por variáveis de ambiente, ausentes do grafo | **Aprovada** pelo mantenedor (2026-11-08, Constituição X) a dependência `Microsoft.Extensions.Hosting` 10.0.12 somente em `Platform.Operations`. O builder é criado **sem repassar `args`** (argumentos de linha de comando nunca viram configuração, evitando `--OperacaoAcesso:LoginAutorizado=...` ou senha por argv); teste em T109/T115 comprova. Sem `Microsoft.AspNetCore.App` (T054). |
| G21 | No ASP.NET Core 10, `SignInAsync` do cookie handler com `SessionStore` chama `RenewAsync(chaveAnterior)` e reaproveita a chave quando a requisição já tem sessão válida, o que contraria a troca atômica e a regra “o login nunca chama `RenewAsync` com a chave anterior” (T103/T106) | **Emissão manual** (mantenedor, 2026-11-08, início da Parada L2-2): `CasoUsoLogin` faz a troca atômica e o commit; depois o endpoint escreve o cookie com o `TicketDataFormat` e o `CookieManager` do próprio esquema, contendo só a claim de sessão do handler (`Microsoft.AspNetCore.Authentication.Cookies-SessionId`, conferida na inicialização e coberta por teste) com a chave nova `IdTenant/EpocaAcesso/IdSessao`. O `ITicketStore` mantém `RetrieveAsync`/`RenewAsync`/`RemoveAsync`; `StoreAsync` lança exceção (nenhum caminho cria sessão fora do login). O logout revoga pelo caso de uso e então remove o cookie. |
| G14 | Registro do resultado de preparação/migration por empresa (FR-014) | Na L2 o resultado é o exit code e o diagnóstico seguro do preflight mais `foundation.__EFMigrationsHistory` do próprio banco; auditoria persistente de preparação/migration fica para a L4 (com backup/restauração). Sem código novo; T042 registra no plano. |
| G22 | SC-003 (T118) intermitente: 200 requisições simultâneas com `Pooling=false` em A e B no mesmo PostgreSQL de teste esgotavam `max_connections=100` (SQLSTATE 53300) | **Pool real só nas duas APIs do SC-003** (mantenedor, 2026-11-12, Parada L2-5): `Pooling=true`, `MaxPoolSize=40` por instalação (40+40 < 100), via `OpcoesFabricaApi.Extras`; demais fixtures mantêm `Pooling=false`. Atende “reutilização de conexões do pool” de T118. Evidência do teste emitida por `TestOutputHelper` (relatório TRX). |
| G23 | Falha transitória do banco chegava como 500 `INTERNAL_ERROR`: a estratégia de execução do EF/Npgsql embrulha `DbException` em `InvalidOperationException` | **503 `DEPENDENCY_UNAVAILABLE` quando qualquer causa interna** é `DbException`/configuração/isolamento/dependência (mantenedor, 2026-11-12). `ConfiguracaoProblemDetails.EhFalhaDependencia`, teste de contrato com `DbException` direto e embrulhado; ADR 0003 D1 revisada. |
| G24 | `launchSettings.json` versionado (e `.vscode/launch.json` local) com string de conexão do **owner**, senha de exemplo, `IdAplicacao` obsoleto e sem `ProtecaoDados:DiretorioChaves` (violava P0 e impedia o F5) | **Perfil host-local corrigido** (mantenedor, 2026-11-12): só valores não secretos, `CodigoAplicacao`, key ring em `keyrings/cliente-a` (ignorado); `StringsConexao__BancoApp` da role runtime vem do ambiente do desenvolvedor (`$env:` ou `envFile` privado `infra/.env.host.local`). README documenta com `compose.depuracao.yaml`. |
| G25 | Restore falha com `NU1507` quando a máquina tem outra origem NuGet (ex.: `github-mrvengenharia`) | **`nuget.config` na raiz** com `<clear/>` e somente `nuget.org` (mantenedor, 2026-11-12; nenhuma origem privada é usada). Sem pacote novo. |
| G26 | Gitleaks acusa 2 achados no histórico (commits `1765569`, `a6e0ce2`): constante de teste `ValorSecreto` com valor fictício | **Falso-positivo triado** (mantenedor, 2026-11-12): `.gitleaksignore` com os 2 fingerprints exatos e `// gitleaks:allow` na linha; nenhuma regra ou caminho liberado. |
| G27 | ADR 0002 sem aceite registrado | **Aceita** pelo mantenedor em 2026-11-12. |
| G28 | `conta criar` real (quickstart §4) falha com `CONFLITO_CONCORRENTE`: o host registrava `ContextoOperacaoOperador` sem login (parâmetro opcional `null`), gravando `TipoAutor=Operador` com `Autor` nulo, rejeitado por `ck_*_auditoria_*`. A fixture injetava o login, por isso a suíte não detectava. Afeta toda gravação auditável do host (criar/desativar/reativar/redefinir-senha/revogar-sessoes/conceder); `permissao revogar` e `sessao limpar` não | **Autor definido pelo verificador** (aprovada em 2026-11-15; T132): `IRegistroOperadorVerificado` no padrão de `IRegistroUsuarioVerificado`; `VerificadorAutorizacaoOperador` define o `current_user` confirmado antes de qualquer gravação; composição única `AdicionarServicosHostOperacoes` usada pelo host e pela fixture; verificador registrado só em `AdicionarCasosUsoOperacoes` (a API não o resolve); interceptador rejeita autor incoerente antes do banco. O `catch` genérico que mascara a causa como `CONFLITO_CONCORRENTE` fica para decisão separada (muda códigos de `operations.md`); decidido em G29/T133. |
| G29 | O `catch` genérico dos 7 casos de uso de conta/permissão transformava qualquer exceção em `CONFLITO_CONCORRENTE` (mascarou G28); `ManutencaoContasIdentity` também classificava email duplicado e erros não previstos do Identity como conflito | **Classificação explícita** (aprovada pelo mantenedor em 2026-11-15; T133): `CONFLITO_CONCORRENTE` só para conflito real (concorrência otimista, `ConcurrencyFailure`, SQLSTATE `23505`/`40001`/`40P01`, zero linhas na retirada); exceção inesperada → `FALHA_INTERNA`; email duplicado → `EMAIL_JA_CADASTRADO`; email recusado pelo Identity → `EMAIL_INVALIDO`. Resultado/registro levam só o código; tipo e mensagem da exceção vão para stderr. `operations.md` emendado. `sessao limpar` mantém `FALHA_LIMPEZA`. |

## Format: `[ID] [P?] [Story] Description`

- **[P]**: pode rodar em paralelo (arquivos distintos, sem dependência de tarefa incompleta)
- **[Story]**: User Story atendida (`[US2]`, `[US3]`, `[US5]`); Setup/Foundational/Polish não têm rótulo
- Caminhos relativos à raiz do repositório

---

## Phase 9: Setup L2 (dependências, projeto novo e documentação das decisões)

**Purpose**: Preparar dependência aprovada, o novo host operacional e o suporte de relógio de teste.

- [X] T040 Adicionar `FluentValidation` e `FluentValidation.DependencyInjectionExtensions` versão `12.2.0` em `Directory.Packages.props`; referenciar `FluentValidation` em `backend/src/Platform.Application/Platform.Application.csproj` e `FluentValidation.DependencyInjectionExtensions` em `backend/src/App.Api/App.Api.csproj`; regenerar os `packages.lock.json` afetados e confirmar `dotnet restore backend/Boilerplate.slnx --locked-mode`
- [X] T041 [P] Atualizar `docs/adr/0003-tratamento-de-erros-e-validacao.md`: status “aceito (2026-11-08, mantenedor)”; registrar a aprovação da emenda 2.3.0 da Constituição pelo mantenedor em 2026-11-08 (G8); revisão em D3 acrescentando `TipoErro.NaoAutenticado` → 401, restrito ao catálogo de plataforma `ErrosAcesso` e proibido em módulos de negócio (G5); D5 com a linha 401
- [X] T042 [P] Propagar as decisões G1–G12 para os artefatos de design (em `operations.md` §4 também a posição do 401 do logout, G9, e a regra de vazio = ausente, G10): `specs/001-fullstack-foundation/plan.md` (P1c com 401/429, ordem G2, grants G6), `specs/001-fullstack-foundation/data-model.md` (§2 nomes de roles, §6 chave `CabecalhosEncaminhados:ProxiesConfiaveis`, §7 formato de `CodigoPermissao`, valores de `TipoOperacao`, enums como texto), `specs/001-fullstack-foundation/contracts/operations.md` (§3 roles derivadas do banco e grants na migration, §4 ordem G2 revisada e cookie CSRF); em `plan.md` também P0 itens 4–5 (`init-db.sh` no lugar de `init-db.sql`, segredos em `.env.local` privado conforme G4), a lista de fábricas Problem Details (400/401/403/404/409/422/429), a execução sequencial decidida (P1c após P1b; P3 após P2), o `GerenciadorUsuariosAplicacao` (G13) e o registro de preparação/migration da L2 (G14). Em `.specify/memory/constitution.md`, **remover** o comentário “Sync Impact Report” do cabeçalho (ele próprio exige remoção antes do commit), sem alterar o texto normativo; as aprovações de FluentValidation (G1) e da emenda 2.3.0 (G8) ficam registradas na ADR 0003 (T041)
- [X] T043 Criar o projeto console `backend/src/Platform.Operations/Platform.Operations.csproj` (`net10.0`, sem `Microsoft.NET.Sdk.Web`, referências somente a `Platform.Application` e `Platform.Infrastructure`), incluí-lo em `backend/Boilerplate.slnx` e gerar seu `packages.lock.json`; incluir um `Program.cs` mínimo sem lógica (encerra com exit não zero e mensagem “não implementado”), para que a solution compile até T115 substituí-lo
- [X] T044 [P] Criar `RelogioControlavel : TimeProvider` (instante inicial explícito, `Avancar(TimeSpan)`, `DefinirAgora(DateTimeOffset)`, sempre offset zero, sem pacote novo) em `backend/tests/Comum/RelogioControlavel.cs` e incluí-lo por `<Compile Include="..\Comum\RelogioControlavel.cs" />` nos três projetos de teste (`backend/tests/Platform.UnitTests/Platform.UnitTests.csproj`, `backend/tests/Platform.IntegrationTests/Platform.IntegrationTests.csproj`, `backend/tests/Platform.ContractTests/Platform.ContractTests.csproj`)

**Checkpoint**: solution restaura em `--locked-mode` com o novo projeto; decisões G1–G7 registradas nos artefatos.

---

## Phase 10: Foundational P0 — configuração, segredos e privilégios (bloqueante)

**Purpose**: Separar runtime/operador/owner, remover credenciais utilizáveis, restringir PostgreSQL ao loopback e definir o modo local Development (plan P0 1–5). Falha aqui bloqueia login.

**⚠️ CRITICAL**: nenhuma superfície autenticada é habilitada antes desta fase.

- [X] T045 Substituir `infra/docker/init-db.sql` por `infra/docker/init-db.sh` (remover o `.sql`): ler de variáveis de ambiente `BANCO_APP`, `SENHA_OWNER`, `SENHA_RUNTIME`, `LOGIN_OPERADOR_ACESSO` e `SENHA_OPERADOR_ACESSO`, abortando com mensagem sem valores se alguma faltar (nunca fabricar valor); criar `<BANCO_APP>_owner` e `<BANCO_APP>_runtime` (LOGIN, `NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS`), `<BANCO_APP>_operador` (NOLOGIN) e o login individual `LOGIN_OPERADOR_ACESSO` membro de `<BANCO_APP>_operador`; criar o banco com owner `<BANCO_APP>_owner`; `REVOKE ALL ON DATABASE ... FROM PUBLIC`; `GRANT CONNECT` só às três roles; schemas `foundation` e `access` pertencentes ao owner com `USAGE` para runtime e operador; `REVOKE CREATE ON SCHEMA public FROM PUBLIC`. Nenhum GRANT de tabela aqui (G6)
- [X] T046 Reescrever `infra/compose.yaml`: `postgres` sem `ports` (depuração opcional somente por `infra/compose.depuracao.yaml` com `127.0.0.1:${PORTA_POSTGRES_DEPURACAO}:5432`), recebendo apenas as variáveis do `init-db.sh` e montando `./docker/init-db.sh` em `/docker-entrypoint-initdb.d/`; `api` publicada em `127.0.0.1:${PORTA_API:?}:8080`, `ASPNETCORE_ENVIRONMENT=Development`, `CodigoAplicacao`, `IdAmbiente`, `IdTenant`, `NomeExibicaoTenant`, `EstadoTenant`, `StringsConexao__BancoApp` montada com `${BANCO_APP}_runtime`/`${SENHA_RUNTIME:?}`, `Sessao__PrazoAbsolutoMinutos` opcional (vazio = ausente, G10), `ProtecaoDados__DiretorioChaves=/var/lib/bps/chaves` no volume nomeado `chaves_protecao_dados` (atualizar `infra/docker/Dockerfile.api` para criar `/var/lib/bps/chaves` com dono e permissão restrita do usuário não-root da imagem, para que o volume novo herde esse dono); `migrations` com conexão do owner e `Migracoes__PermitirInicializarBancoVazio`; novo serviço `operations` (profile `operations`, `stdin_open: true`, `tty: true`, build `infra/docker/Dockerfile.operations`) com conexão do login individual, `OperacaoAcesso__LoginAutorizado=${LOGIN_OPERADOR_ACESSO:?}` e `OperacaoAcesso__RetencaoSessoesDias` opcional (vazio = ausente, G10). A API nunca recebe senha de owner/operador. Sem `container_name` nem nomes globais; volumes/redes por projeto Compose
- [X] T047 [P] Reescrever `infra/.env.example`: `CodigoAplicacao` no lugar de `IdAplicacao`; variáveis de T045/T046 com valor vazio e comentário “definir valor privado”; nenhuma senha ou string de conexão utilizável; bloco de referência B com `IdTenant`, `IdAmbiente`, `BANCO_APP` e `PORTA_API` distintos; instruções de `down --volumes` para recriar bancos locais do zero (o init só roda na criação do volume)
- [X] T048 [P] Criar `infra/docker/Dockerfile.operations`: build multi-stage com SDK `10.0.401` publicando `backend/src/Platform.Operations`, imagem final runtime .NET `10.0.12` não-root, sem SDK, sem `dotnet-ef` e sem credenciais
- [X] T049 Validar sem imprimir segredos: `docker compose --project-name boilerplate-a -f infra/compose.yaml --env-file infra/.env.local config --quiet` e o mesmo para B com `--project-name boilerplate-b --env-file infra/.env.b.local` (valores privados locais); confirmar que `infra/.env.example` sozinho falha por variáveis obrigatórias ausentes e que `.gitignore` cobre `infra/.env*.local` e diretórios locais de chaves; registrar o resultado no checkpoint abaixo

**Checkpoint (P0)**: três identidades distintas por instalação, PostgreSQL sem publicação pública, API só no loopback em Development, key ring em volume próprio e exemplo sem segredo.

> **Resultado T049 (2026-11-08)**: `docker compose ... config --quiet` OK para A (`boilerplate-a`, `.env.local`, inclusive perfis `migrations`/`operations`) e B (`boilerplate-b`, `.env.b.local`); `.env.example` sozinho falha por variáveis obrigatórias ausentes (`LOGIN_OPERADOR_ACESSO`, `SENHA_SUPERUSUARIO_POSTGRES`…); override de depuração sem `PORTA_POSTGRES_DEPURACAO` falha; portas publicadas: somente `api` em `127.0.0.1`; a API não recebe credencial de owner/operador; volumes por projeto (`boilerplate-a_chaves_protecao_dados`); `.gitignore` cobre `infra/.env*.local` e `keyrings/`. Valores privados gerados localmente com senhas aleatórias (não impressos). `init-db.sh` com `bash -n` OK; execução real no container fica para a subida A/B (Docker daemon).

---

## Phase 11: Foundational P1a — fundações de modelo, relógio, identificadores e auditoria

**Purpose**: `InstanteUtc`, `IdentificadorV7`, `TimeProvider`, `IContextoOperacao`, base auditável, interceptor, convenções de nomes e alinhamento do código L1 (plan P1a; data-model §2–§7; D19/D20).

### Tests (escrever primeiro; devem falhar antes da implementação)

- [X] T050 [P] Testes de `InstanteUtc` em `backend/tests/Platform.UnitTests/Tempo/TestesInstanteUtc.cs`: `Normalizar` converte para offset zero e trunca para microssegundos; `Validar(valor, nomeCampo)` rejeita `default` e offset diferente de zero com mensagem citando o campo
- [X] T051 [P] Testes de `IdentificadorV7` em `backend/tests/Platform.UnitTests/Identificadores/TestesIdentificadorV7.cs`: versão 7, timestamp em milissegundos igual ao instante usado (via `RelogioControlavel`), rejeição de offset diferente de zero e de `default`
- [X] T052 [P] Testes de opções em `backend/tests/Platform.UnitTests/Configuration/TestesOpcoesSessaoEOperacao.cs`: `Sessao:PrazoAbsolutoMinutos` aceita `5`, `30` e `480`, ausente = 30, vazio (`""`) = ausente = 30 (G10), rejeita `4`, `481`, `0`, `-5`, `30.5`, `" 30"`, `"1e2"` e texto com mensagem que cita a chave e a faixa “5–480” sem valores de outras chaves; `OperacaoAcesso:RetencaoSessoesDias` aceita `1` e `365`, ausente ou vazio = 30, rejeita `0`, `366` e não inteiro; parse com `NumberStyles.None` e cultura invariante
- [X] T053 [P] Testes do catálogo em `backend/tests/Platform.UnitTests/Access/TestesCodigosPermissao.cs`: códigos únicos, todos casam com `^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$` e têm 1–128 caracteres, catálogo contém `foundation.context.read`, código desconhecido é rejeitado por `EhConhecido`
- [X] T054 [P] Testes de arquitetura em `backend/tests/Platform.UnitTests/Architecture/TestesArquiteturaRelogioEIdentificadores.cs`, por varredura textual dos `.cs` de `backend/src/**` excluindo `**/Migrations/**`: proibidos `DateTime.Now`, `DateTime.UtcNow`, `DateTime.Today`, `DateTimeOffset.Now`, `DateTimeOffset.UtcNow`, `Guid.NewGuid(`, `Guid.CreateVersion7()` sem argumento, `ExecuteUpdate`/`ExecuteUpdateAsync`, `ExecuteDelete`/`ExecuteDeleteAsync` e `HasDefaultValueSql`; e atualizar `backend/tests/Platform.UnitTests/Architecture/LayerDependencyTests.cs` para cobrir `Platform.Operations` (referencia só Application/Infrastructure; nenhum pacote `Microsoft.AspNetCore.*` de servidor HTTP; Domain continua sem Identity/EF/HTTP)

### Implementation

- [X] T055 [P] Implementar `InstanteUtc` estático e puro (`Normalizar`, `Validar`; não lê relógio) em `backend/src/Platform.Domain/Tempo/InstanteUtc.cs`
- [X] T056 [P] Implementar `IdentificadorV7.Gerar(DateTimeOffset instante)` sobre `Guid.CreateVersion7(DateTimeOffset)`, exigindo offset zero e instante não `default`, em `backend/src/Platform.Domain/Identificadores/IdentificadorV7.cs`
- [X] T057 [P] Criar a base auditável em `backend/src/Platform.Domain/Auditoria/`: `TipoAutor.cs` (enum `Operador`, `Usuario`, `Sistema`, `Anonimo`), `IEntidadeAuditavel.cs`, `IEntidadeVersionada.cs` (`long Versao`) e `EntidadeAuditavel.cs` (abstrata, só metadados, sem PK nem exclusão lógica) com os campos de data-model §4: `CriadoEm` (“UTC `timestamptz`, obrigatório”), `TipoAutorCriacao`, `AutorCriacao` (“string até 256 … nulo somente para Anonimo”), `IdOperacaoCriacao` (“UUID de correlação gerado pelo servidor, obrigatório”), `AlteradoEm` (“opcional, preenchido somente quando há alteração efetiva”), `TipoAutorAlteracao`, `AutorAlteracao`, `IdOperacaoAlteracao` (“obrigatório quando `AlteradoEm` estiver preenchido”); XML summary em todos os membros
- [X] T058 [P] Criar em `backend/src/Platform.Domain/Access/`: `CodigosPermissao.cs` (constante `FundacaoContextoLer = "foundation.context.read"`, `Todos`, `EhConhecido`, regex de formato G7), `TipoOperacaoAcesso.cs` (valores G7) e `ResultadoOperacaoAcesso.cs` (`Sucesso`, `Negado`, `Falha`)
- [X] T059 [P] Criar a porta `IContextoOperacao` (`Guid IdOperacao`, `TipoAutor TipoAutor`, `string? Autor`, `DateTimeOffset InstanteOperacao`; instante lido uma vez do `TimeProvider` e normalizado por `InstanteUtc`) em `backend/src/Platform.Application/Auditoria/IContextoOperacao.cs`
- [X] T060 [P] Implementar `LeitorInteiroConfiguracao` (parse G7/D21; valor vazio tratado como ausente, G10), `OpcoesSessao` (`PrazoAbsolutoMinutos`, inteiro 5–480 inclusive, padrão 30) e `OpcoesOperacaoAcesso` (`LoginAutorizado` obrigatório para o host — login PostgreSQL individual, não papel, `RetencaoSessoesDias` inteiro 1–365 inclusive, padrão 30), com validadores `IValidateOptions` de diagnóstico seguro, em `backend/src/Platform.Application/Configuration/LeitorInteiroConfiguracao.cs`, `OpcoesSessao.cs` e `OpcoesOperacaoAcesso.cs`
- [X] T061 Renomear `IdAplicacao` para `CodigoAplicacao` (“string de 1 a 100; identificação textual do produto (não secreta, não é `Id*`)”) em `backend/src/Platform.Application/Configuration/OpcoesInstalacaoTenant.cs`, no binding de `backend/src/App.Api/Program.cs`, em `backend/src/Platform.Infrastructure/Persistence/DesignTime/AcessoTenantDbContextFactory.cs`/`PreflightMigracaoTenant.cs` e nos testes `backend/tests/Platform.UnitTests/Configuration/TestesValidacaoOpcoesInstalacaoTenant.cs` e `backend/tests/Platform.UnitTests/Foundation/TestesConfiguracaoInstalacao.cs`; registrar `TimeProvider.System` como singleton em `Program.cs`
- [X] T062 Alinhar entidades existentes em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Entities/`: `UsuarioAplicacao.cs` implementa `IEntidadeAuditavel` e troca `IdTenant` por `FkIdTenant` (“FK para `identidade_banco.IdTenant`”); `PermissaoUsuario.cs` herda `EntidadeAuditavel`, troca `IdUsuario` por `FkIdUsuario`, remove `ConcedidaEm`, `Versao` e o inicializador `DateTimeOffset.UtcNow` (`CriadoEm` é o instante da concessão; concessão imutável); `IdentidadeBancoTenant.cs` valida `InicializadoEm` com `InstanteUtc.Validar`; `Validar()` de todas usa `InstanteUtc.Validar` nos instantes e rejeita Id vazio
- [X] T063 [P] Criar `SessaoAutenticacao` (herda `EntidadeAuditavel`, implementa `IEntidadeVersionada`) em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Entities/SessaoAutenticacao.cs` com: `IdSessao` (“UUID v7 não vazio, PK opaca gerada pelo servidor”), `FkIdUsuario` (“FK local, exclusão restrita”), `EpocaAcesso` (“UUID não vazio, cópia da geração vigente na emissão”), `TicketProtegido` (“`bytea` anulável … removido na revogação”), `ExpiraEm` (“`CriadoEm` + `Sessao:PrazoAbsolutoMinutos` vigente na emissão (padrão 30); imutável”), `RevogadaEm` (“UTC anulável … mesmo instante de `AlteradoEm`”), `Versao` (“int64 positivo, token de concorrência incrementado pelo `InterceptadorAuditoria`”); `Validar()` e XML summaries
- [X] T064 [P] Criar `RegistroOperacaoAcesso` (append-only, sem herdar `EntidadeAuditavel`) em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Entities/RegistroOperacaoAcesso.cs` com: `IdOperacao` (“UUID v7 não vazio, PK”), `IdTenant`/`IdAmbiente` (referências sem constraint), `IdUsuarioAlvo` (“UUID opcional; referência sem FK”), `LoginOperador` (“string de 1 a 128”), `TipoOperacao` (`TipoOperacaoAcesso`), `CodigoPermissao` (“string opcional até 128”), `RegistradaEm`, `Resultado` (`ResultadoOperacaoAcesso`), `CodigoResultado` (“string até 100, código estável seguro”), `RetencaoDias` (“int 1–365, somente `LimparSessoes`”), `InstanteCorte` (“somente `LimparSessoes`: `RegistradaEm - RetencaoDias`”), `QuantidadeRemovida` (“int ≥ 0, somente `LimparSessoes`”); `Validar()` e XML summary em todos os membros
- [X] T065 Implementar a convenção central de nomes `ConvencaoNomesBanco.Aplicar(ModelBuilder)` (laço sobre o modelo: `pk_<tabela>`, `fk_<tabela>_<tabela_ref>[_<papel>]`, `ix_<tabela>_<colunas>`, `uq_<tabela>_<colunas>`, `ck_<tabela>_<regra>`, minúsculas, colunas em snake_case no nome, exceção se passar de 63 caracteres) em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Configurations/ConvencaoNomesBanco.cs`
- [X] T066 Atualizar `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Configurations/ConfiguracaoModeloAcesso.cs` (marcador, usuários, permissões e tabelas auxiliares Identity): tabelas `foundation.identidade_banco`, `access.usuarios`, `access.permissoes_usuario`, `access.reivindicacoes_usuario`, `access.logins_usuario`, `access.tokens_usuario`; `Id`→coluna `IdUsuario`, `UserId`→`FkIdUsuario`, `Id` da reivindicação→`IdReivindicacaoUsuario`; `FkIdTenant` com FK para `IdTenant` (chave alternativa `uq_identidade_banco_id_tenant`); PKs `Guid` com `ValueGeneratedNever()`; PK `(FkIdUsuario, CodigoPermissao)`; CHECK de formato do código; `NomeExibicao` 1–120; `Email` até 254; colunas de auditoria e CHECKs `ck_<tabela>_auditoria_*` (criação sempre completa, autor nulo só para Anonimo; alteração toda presente ou toda nula); enums como texto com CHECK; `timestamptz` sem default; `HasComment` em todas as colunas
- [X] T067 [P] Criar `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Configurations/ConfiguracaoSessaoAutenticacao.cs`: tabela `access.sessoes_autenticacao`, FK restrita `fk_sessoes_autenticacao_usuarios`, `Versao` como token de concorrência, índices `ix_sessoes_autenticacao_fk_id_usuario` (não único), `ix_sessoes_autenticacao_expira_em` e `ix_sessoes_autenticacao_revogada_em`, CHECK `ck_sessoes_autenticacao_prazo` (`ExpiraEm - CriadoEm` entre 5 e 480 minutos), CHECK de sessão revogada sem ticket, CHECK `Versao > 0`, auditoria e `HasComment`
- [X] T068 [P] Criar `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Configurations/ConfiguracaoRegistroOperacaoAcesso.cs`: tabela `access.operacoes_acesso`, índices `ix_operacoes_acesso_id_usuario_alvo_registrada_em` e por `RegistradaEm`, CHECK `ck_operacoes_acesso_limpeza` (três campos de limpeza presentes só com `TipoOperacao=LimparSessoes` e, nesse tipo, `IdUsuarioAlvo`/`CodigoPermissao` nulos), enums como texto com CHECK, `HasComment`
- [X] T069 Implementar `InterceptadorAuditoria : SaveChangesInterceptor` em `backend/src/Platform.Infrastructure/Persistence/Auditoria/InterceptadorAuditoria.cs` a partir de `IContextoOperacao`: `Added` preenche criação; `Modified` com alteração efetiva preenche última alteração e incrementa `Versao` em `IEntidadeVersionada`; rejeita mudança de campos de criação; `PermissaoUsuario` `Modified` rejeitado; `RegistroOperacaoAcesso` `Modified`/`Deleted` rejeitados; em `UsuarioAplicacao`, mudança apenas de `ConcurrencyStamp` não conta como alteração efetiva; `SessaoAutenticacao` `Deleted` não altera metadados
- [X] T070 Atualizar `backend/src/Platform.Infrastructure/Persistence/TenantAccess/AcessoTenantDbContext.cs`: `DbSet` de sessões e operações, aplicar as configurações de T066–T068 e `ConvencaoNomesBanco` por último; interceptor registrado pela composição (API, host, factory)
- [X] T071 Atualizar `backend/src/Platform.Infrastructure/Persistence/DesignTime/PreflightMigracaoTenant.cs` e `AcessoTenantDbContextFactory.cs`: `InicializadoEm` e `EpocaAcesso` (`IdentificadorV7`) vindos de `TimeProvider.System`; histórico aplicado precisa ser prefixo válido da cadeia conhecida e qualquer ID desconhecido bloqueia antes de DDL, mesmo sem pendentes
- [X] T072 Atualizar `backend/src/Platform.Infrastructure/Persistence/TenantAccess/VerificadorIdentidadeBancoTenant.cs`: runtime/readiness só aceitam a cadeia conhecida completa e na ordem (`GetAppliedMigrations` igual a `GetMigrations`); ID desconhecido ou pendente → versão incompatível

**Checkpoint (P1a)**: testes T050–T054 passam; nenhum relógio estático, `Guid.NewGuid()` ou `ExecuteUpdate` no código de produção.

> **Resultado (2026-11-08)**: `Platform.UnitTests` 148/148 aprovados (T050–T054 inclusos; varredura textual sem violações). Parte unitária de T079 (`TestesModeloAcessoUsuario`) adiantada para compilar. `Microsoft.Extensions.Options` 10.0.12 referenciado diretamente em `Platform.Application` para os `IValidateOptions` de T060 (já presente no grafo como transitivo de Infrastructure; nenhum pacote novo). Testes de integração ainda não compilam até T075/T077/T079 (Phase 12).

---

## Phase 12: Foundational P1b — migration inicial única, grants e regressão SC-009

**Purpose**: Descartar o rascunho, gerar a migration única do modelo completo com GRANTs (G6) e provar bancos A/B do zero (plan P1b; data-model §8).

- [X] T073 Remover `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Migrations/20261102141948_EsquemaInicialAcessoTenant.cs`, `...Designer.cs` e `AcessoTenantDbContextModelSnapshot.cs`; gerar a migration única `EsquemaInicialAcessoTenant` com `dotnet ef migrations add EsquemaInicialAcessoTenant --project backend/src/Platform.Infrastructure --startup-project backend/src/App.Api --context AcessoTenantDbContext` em `backend/src/Platform.Infrastructure/Persistence/TenantAccess/Migrations/`
- [X] T074 Acrescentar ao `Up` da migration (arquivo gerado em T073) um bloco `migrationBuilder.Sql` com `DO $$` que deriva as roles de `current_database()` via `format('%I', ...)`, falha com mensagem clara se `<banco>_runtime` ou `<banco>_operador` não existir e concede: **runtime** — SELECT em `foundation.identidade_banco` e `foundation."__EFMigrationsHistory"`; SELECT, UPDATE em `access.usuarios`; SELECT em `access.permissoes_usuario`; SELECT, INSERT, UPDATE em `access.sessoes_autenticacao` (sem DELETE). **operador** — SELECT no marcador e no histórico; SELECT, INSERT, UPDATE em `access.usuarios`; SELECT, INSERT, DELETE em `access.permissoes_usuario`; SELECT, UPDATE, DELETE em `access.sessoes_autenticacao`; somente INSERT em `access.operacoes_acesso`. Nenhuma das duas recebe privilégio em `reivindicacoes_usuario`, `logins_usuario`, `tokens_usuario` ou nas sequências delas, nem escrita no marcador. `Down` revoga o que concedeu
- [X] T075 Atualizar `backend/tests/Platform.IntegrationTests/Fixtures/FixtureInstalacaoPostgres.cs` (A e B no mesmo container): criar por banco `<banco>_owner`, `<banco>_runtime`, `<banco>_operador` (NOLOGIN) e dois logins individuais membros do grupo, `<banco>_op_um` e `<banco>_op_dois`, com senhas aleatórias por execução; replicar os grants de banco/schema de T045; remover `ALTER DEFAULT PRIVILEGES` e os GRANTs pós-migration, que agora vêm da migration; expor uma string de conexão por identidade; adaptar `backend/tests/Platform.IntegrationTests/Fixtures/TestesFixtureInstalacaoPostgres.cs`
- [X] T076 [P] Testes de convenções do modelo (só o modelo EF, sem banco) em `backend/tests/Platform.IntegrationTests/Modelo/TestesConvencoesModelo.cs`: PK `Id*`/`IdUnico`; toda FK `FkId*` apontando para `Id*`; constraints/índices no padrão G6/§2, minúsculos e com até 63 caracteres; PKs `Guid` com `ValueGeneratedNever`; `HasComment` em todas as colunas; nenhum default SQL em data; exceções numa lista fechada (marcador, PK da concessão, três tabelas auxiliares Identity) — tabela nova fora da lista que viole reprova
- [X] T077 [P] Testes de migration e privilégios: atualizar `backend/tests/Platform.IntegrationTests/Tenancy/TestesMigracaoEIsolamentoTenant.cs` (banco novo A/B, reaplicação sem duplicação nem perda, histórico com ID desconhecido rejeitado pelo tooling e pelo runtime/readiness mesmo sem pendentes, alvo A apontando para B bloqueado antes de DDL) e criar `backend/tests/Platform.IntegrationTests/Tenancy/TestesPrivilegiosERestricoes.cs` (runtime sem DDL, sem DELETE em sessões, sem INSERT em operações, sem escrita em tabelas auxiliares, sem CONNECT em B; operador sem UPDATE/DELETE em operações e sem escrita no marcador; migration falha se as roles não existem; CHECKs: prazo de 4 e 481 minutos rejeitado, sessão revogada com ticket rejeitada, campos de limpeza fora de `LimparSessoes` rejeitados, código de permissão fora do formato rejeitado; instantes gravados e lidos com offset zero e microssegundos iguais; tabelas auxiliares vazias ao fim da suíte)
- [X] T078 [P] Testes do interceptor e identificadores em `backend/tests/Platform.IntegrationTests/Auditoria/TestesInterceptadorAuditoria.cs` com `RelogioControlavel` e `IContextoOperacao` de teste: criação completa nos três tipos auditáveis; alteração efetiva preenche última alteração e incrementa `Versao` só na sessão; no-op não altera metadados; criação imutável; `PermissaoUsuario` `Modified` e `RegistroOperacaoAcesso` `Modified`/`Deleted` falham; só `ConcurrencyStamp` não conta; UUID v7 das PKs com timestamp igual ao `InstanteOperacao`; falha de auditoria não confirma o dado (mesma transação)
- [X] T079 Atualizar `backend/tests/Platform.IntegrationTests/Access/TestesPersistenciaUsuarioTenant.cs` e `backend/tests/Platform.UnitTests/Access/TestesModeloAcessoUsuario.cs` para os nomes e campos novos (`FkIdTenant`, `FkIdUsuario`, base auditável, sem `ConcedidaEm`/`Versao` na concessão), mantendo as provas SC-009 (mesmo email em A/B, concorrência mínima, falha em A sem efeito em B)

**Checkpoint (P1b)**: `dotnet test` verde; SC-009 reprovado sobre a migration inicial única; grants comprovados por tentativas negadas.

> **Resultado (2026-11-08)**: migration única `20261108234429_EsquemaInicialAcessoTenant` com GRANTs (G6). `dotnet test --solution` 226/226 aprovados (Unit, Integration com PostgreSQL 18.6 via Testcontainers, Contract). Na L2 o runtime não tem INSERT em `access.usuarios`: os testes SC-009 criam contas com o login individual de operador e leem com o runtime.

---

## Phase 13: Foundational P1c — erros e validação (ADR 0003)

**Purpose**: `Resultado`/`Erro`, Problem Details 400/401/403/404/409/422/429, JSON estrito e FluentValidation no grupo `/api/v1` (plan P1c; G1, G2, G5). Executada após a Phase 12, em sequência (T061 e T088 editam `Program.cs`).

### Tests

- [X] T080 [P] Testes de `Resultado`, `Resultado<T>` e `Erro` em `backend/tests/Platform.UnitTests/Resultados/TestesResultado.cs`: `Corresponder`, conversões implícitas de `T` e `Erro`, `Resultado.Ok()`, `Valor` em falha lança `InvalidOperationException`
- [X] T081 [P] Testes de `ValidadorRequisicaoLogin` (`TestValidate`) em `backend/tests/Platform.UnitTests/Access/TestesValidadorRequisicaoLogin.cs`: `email` obrigatório, formato de email, até 254; `password` obrigatório, 1–128; mensagens em pt-BR que não ecoam o valor; chaves `email`/`password`
- [X] T082 [P] Testes de arquitetura em `backend/tests/Platform.ContractTests/Architecture/TestesArquiteturaValidacaoErros.cs` (ContractTests já referencia `App.Api`, onde ficam `ErrosPlataforma` e o registro DI dos validadores; UnitTests não referencia `App.Api`): todo tipo `Requisicao*` tem `IValidator<T>` registrado e um `Validador<Nome>`; validadores sem `MustAsync`/`CustomAsync` e sem dependência de `DbContext`, repositório ou `TimeProvider`; `JsonUnmappedMemberHandling.Skip` só em namespaces `*.Integracoes.*`; códigos dos catálogos `Erros*` (`ErrosAcesso`, `ErrosPlataforma` e futuros) únicos **entre todos os catálogos** e no padrão `^[A-Z][A-Z0-9_]+$`; nenhum literal de código de erro (string no padrão acima usada como `code`) declarado fora de um catálogo em `backend/src/**`; `TipoErro.NaoAutenticado` usado só em `ErrosAcesso`
- [X] T083 [P] Testes de contrato de erros em `backend/tests/Platform.ContractTests/Diagnostics/TestesConfiguracaoProblemDetails.cs` (ampliar), usando um endpoint de teste registrado só no host do teste: 400 `INPUT_INVALID` para JSON malformado, campo desconhecido, tipo incompatível e `null` em campo obrigatório, com `errors` em camelCase sem `$.` e sem ecoar valor nem mensagem original, em Production e Development; formato de 401/403/409/429 (`type`, `title`, `status`, `code`, `traceId` de 32 hex); `Retry-After` no 429; `Cache-Control: no-store` nas respostas de erro de `/api/v1`

### Implementation

- [X] T084 [P] Implementar `backend/src/Platform.Domain/Resultados/Erro.cs` (`Codigo`, `Mensagem`, `TipoErro`), `TipoErro.cs` (`NaoEncontrado`, `Conflito`, `RegraNegocio`, `AcessoNegado`, `NaoAutenticado`) e `Resultado.cs` (`Resultado` e `Resultado<T>`, conforme ADR 0003 D3)
- [X] T085 [P] Criar o catálogo de plataforma `ErrosAcesso` em `backend/src/Platform.Application/Access/ErrosAcesso.cs`: `AUTH_INVALID` e `AUTH_REQUIRED` (`NaoAutenticado`), `TENANT_ACCESS_DENIED` (`AcessoNegado`), `TENANT_UNAVAILABLE` e `SESSION_CONFLICT` (`Conflito`), com títulos em pt-BR
- [X] T086 [P] Criar `RequisicaoLogin` (`Email` e `Senha`, esta com `[JsonPropertyName("password")]`; nomes JSON `email`/`password` do contrato) e `ValidadorRequisicaoLogin` em `backend/src/Platform.Application/Access/Contratos/RequisicaoLogin.cs` e `ValidadorRequisicaoLogin.cs`
- [X] T087 Criar o catálogo `backend/src/App.Api/Diagnostics/ErrosPlataforma.cs` com as constantes únicas dos códigos de transporte/plataforma sem `TipoErro` (`INPUT_INVALID`, `TENANT_REQUIRED`, `CSRF_INVALID`, `RATE_LIMITED`, `DEPENDENCY_UNAVAILABLE`, `NOT_FOUND`, `INTERNAL_ERROR`), usado por fábricas, middlewares e limiter (Constituição V: código declarado uma única vez). Ampliar `backend/src/App.Api/Diagnostics/ConfiguracaoProblemDetails.cs`: fábricas para 400 (`INPUT_INVALID` com `errors`, `TENANT_REQUIRED`, `CSRF_INVALID`), 401, 403, 409, 422 e 429 (`RATE_LIMITED` + `Retry-After`); `DeErro(Erro)` como ponto único de tradução `TipoErro`→status; `Cache-Control: no-store` nos erros; `ManipuladorExcecaoSeguro` converte `BadHttpRequestException` com `JsonException` interna em 400 `INPUT_INVALID` com chave derivada de `JsonException.Path` sem `$.`, sem ecoar valor nem mensagem e sem `LogError`
- [X] T088 Criar `backend/src/App.Api/Diagnostics/ConfiguracaoJsonEstrito.cs` (`UnmappedMemberHandling.Disallow`, `RespectNullableAnnotations`, `RespectRequiredConstructorParameters`, `RouteHandlerOptions.ThrowOnBadRequest = true` em todos os ambientes) e `backend/src/App.Api/Diagnostics/FiltroValidacaoRequisicao.cs` (endpoint filter do grupo `/api/v1` que resolve `IValidator<T>` de cada argumento `Requisicao*` e responde 400 `INPUT_INVALID`; resolvedor de nomes FluentValidation usa `JsonPropertyName` ou camelCase); registrar validadores com `AddValidatorsFromAssemblyContaining<ValidadorRequisicaoLogin>()` em `backend/src/App.Api/Program.cs`

**Checkpoint (P1c)**: categorias de falha, códigos e JSON estrito ativos; nenhuma rota L2 publicada ainda.

> **Resultado — Parada L2-1 concluída (2026-11-08)**: `restore --locked-mode`, build (0 avisos), `dotnet format --verify-no-changes` e `dotnet test --solution` 280/280 (Unit 165, Integration com PostgreSQL real, Contract 53). No caminho de exceção, o `ExceptionHandlerMiddleware` do ASP.NET Core força `Cache-Control: no-cache,no-store` (+ `Pragma`/`Expires`); os testes exigem a diretiva `no-store`. `ExcecaoDependenciaIndisponivel` (Application) e `DbException` mapeiam para 503.

---

## Phase 14: User Story 2 — Sessão e controles HTTP (Priority: P1) 🎯 MVP

**Goal**: Login/logout por cookie com CSRF, sessão persistida revogável com prazo absoluto configurável, `/me` e `/context` com autorização pela concessão atual, rate limit e lockout (plan P2; FR-005–FR-008; SC-002 parte HTTP).

**Independent Test**: com contas sintéticas criadas via `UserManager` na fixture (sem host operacional), em A e B com mesmo email e senhas distintas, executar a matriz HTTP do quickstart §6 (jornada, ordem G2, expiração 5/30/480, revogação, troca atômica, disputa, lockout, rate limit, instalação inativa/reativada) em cookie jars separados.

### Tests for User Story 2 — HTTP ⚠️

- [X] T089 [US2] Preparar suporte HTTP: referenciar `backend/src/App.Api/App.Api.csproj` e `Microsoft.AspNetCore.Mvc.Testing` em `backend/tests/Platform.IntegrationTests/Platform.IntegrationTests.csproj` (se ausentes; atualizar lockfile) e criar em `backend/tests/Platform.IntegrationTests/Http/Suporte/` `FabricaApiInstalacao.cs` (`WebApplicationFactory<Program>` por instalação: Development, `RelogioControlavel`, diretório temporário de chaves por instância, conexão runtime da fixture, overrides de `Sessao:PrazoAbsolutoMinutos`/`EstadoTenant`, override de limite/partição do limiter para as suítes que não testam o limiter — T093 usa a política real (G11) —, e asserção de que o cookie handler (`AuthenticationSchemeOptions.TimeProvider`) e o `UserManager<UsuarioAplicacao>` resolvido como `GerenciadorUsuariosAplicacao` (G13) leem o `RelogioControlavel`), `ClienteAcessoHttp.cs` (cookie jar próprio, GET antiforgery, login, logout, `X-Tenant-Id`) e `ContasSinteticas.cs` (cria conta/concessão via `UserManager` com `IContextoOperacao` de teste)
- [X] T090 [P] [US2] Testes de jornada e ordem de validação em `backend/tests/Platform.IntegrationTests/Http/TestesJornadaEOrdemValidacao.cs`: jornada antiforgery→login 200 (`tenantId`, `expiresAt`)→novo GET antiforgery→`/me`→`/context`→logout 204; header ausente 400 `TENANT_REQUIRED`, inválido/duplicado/lista 400 `INPUT_INVALID`; divergente: login 401 `AUTH_INVALID` sem lookup de conta, demais 403; ordem G2 revisada (marcador divergente com corpo inválido, CSRF inválido ou cookie de sessão válido/inválido → 503 `DEPENDENCY_UNAVAILABLE`, sem consulta a `access.sessoes_autenticacao` nem `access.usuarios`; corpo inválido + CSRF inválido → `INPUT_INVALID`; corpo válido + CSRF ausente/inválido/de identidade anterior → `CSRF_INVALID`; logout sem cookie de sessão ou com sessão inválida → 401 `AUTH_REQUIRED` mesmo com corpo e CSRF inválidos, G9); email desconhecido, senha errada, senha fora da política, conta desativada/bloqueada → 401 genérico sem sessão; sem `foundation.context.read` → `/context` 403 e `/me` 200; revogar a concessão vale na próxima operação; instalação inativa: login 401, `/me`/`/context` 409 `TENANT_UNAVAILABLE` com sessão válida, antiforgery e logout permitidos; `Cache-Control: no-store`; health com cookies válidos/inválidos não consulta sessão; `/api/v1/me/tenants` 404
- [X] T091 [P] [US2] Testes de sessão em `backend/tests/Platform.IntegrationTests/Http/TestesSessaoExpiracaoRevogacao.cs`: prazo padrão (29:59 autoriza, 30:00 → 401); 5 e 480 expiram exatamente; `Sessao__PrazoAbsolutoMinutos` `4`, `481`, `0`, `30.5` e texto impedem a inicialização com diagnóstico que cita chave e faixa, sem segredos; sessão emitida com 30 e API reiniciada com 5 mantém o `ExpiraEm` original; uso não estende prazo; logout só responde 204 após commit e replay dá 401; falha injetada no commit do logout → 503 sem apagar cookie; duas sessões da mesma conta em jars distintos independentes no login/logout; cookie de A reenviado a B → 401 e zero dados; epoch alterado na fixture → 401; reativação retoma só sessão dentro do prazo original; restart com o mesmo key ring mantém a sessão
- [X] T092 [P] [US2] Testes de concorrência e lockout em `backend/tests/Platform.IntegrationTests/Http/TestesConcorrenciaSessaoLockout.cs` (barreiras determinísticas): falha injetada na troca de sessão do login → 503, nenhuma sessão nova, anterior válida com o mesmo `ExpiraEm`; dois logins com a mesma sessão anterior → exatamente um 200 e um 409 `SESSION_CONFLICT` sem `Set-Cookie`, uma sessão nova no banco, outros jars intactos, repetição com novo GET antiforgery funciona; disputa Renew/logout sem ressurreição nem extensão; quinta falha bloqueia por 15 minutos e revoga todas as sessões + troca stamp atomicamente, inclusive com falhas concorrentes (sem contagem perdida); três conflitos de `ConcurrencyStamp` seguidos → 503; `LockoutEnd` gravado = instante do `RelogioControlavel` + 15 minutos, offset zero e microssegundos (G13); bloqueio ainda vigente em +14:59 e encerrado em +15:00 pelo relógio de teste; após 15 minutos o cookie antigo continua 401 e novo login funciona; conta de B inalterada
- [X] T093 [P] [US2] Testes de rate limit em `backend/tests/Platform.IntegrationTests/Http/TestesLimiteLogin.cs`: sexto login na janela de um minuto → 429 `RATE_LIMITED` com `Retry-After`; `X-Forwarded-For` forjado não contorna (forwarded desligado, G7); `RemoteIpAddress` nulo (padrão do `TestServer`) cai na partição fixa e também é limitado (G11); teste separado do lockout; requisição limitada não revela validade de header/corpo/conta
- [X] T094 [P] [US2] Testes de auditoria das sessões em `backend/tests/Platform.IntegrationTests/Http/TestesAuditoriaSessaoHttp.cs`: sessão criada com `TipoAutorCriacao=Usuario` e `AutorCriacao` = Id da conta só após credencial verificada; falha de senha atualiza a conta com autoria `Anonimo` (email enviado não vira autor); revogação preenche alteração e `RevogadaEm` com o mesmo instante; nenhum registro com autoria `Sistema`
- [X] T129 [US2] Testes de logs seguros em `backend/tests/Platform.IntegrationTests/Http/TestesLogsSemSegredos.cs`, com um `ILoggerProvider` de captura registrado em `FabricaApiInstalacao` (nível `Trace`, todas as categorias): executar login válido, senha errada, lockout, CSRF inválido, JSON malformado contendo a senha, falha injetada de commit (503) e logout; nenhuma entrada contém a senha enviada, o email enviado ou o `NomeExibicao` da conta (dados pessoais, Constituição VI), o valor dos cookies `bps-session`/`bps-csrf`, o token antiforgery, bytes/base64 do ticket protegido, a string de conexão ou a senha do runtime; as entradas de erro carregam `traceId`/`IdOperacao` (NFR-001, NFR-006; plan Pipeline item 6)
- [X] T130 [US2] Testes de cookies e proteção de chaves em `backend/tests/Platform.IntegrationTests/Http/TestesCookiesEProtecaoChaves.cs`: em Development, `Set-Cookie` de sessão `bps-session` com HttpOnly, SameSite=Lax, Path `/`, sem Domain e `expires` igual ao `ExpiraEm` gravado; antiforgery `bps-csrf`; em Production (HTTPS simulado pelo `TestServer` e certificado de teste gerado na execução), nomes `__Host-bps-session`/`__Host-bps-csrf` com Secure; a API em Production sem `ProtecaoDados:ArquivoCertificado` ou com certificado ilegível não inicializa, com diagnóstico que cita a chave sem segredos; nenhum caminho fora de Development emite `bps-session` sem Secure

### Implementation for User Story 2 — HTTP

- [X] T095 [P] [US2] Criar portas e modelos em `backend/src/Platform.Application/Access/Portas/`: `IRepositorioSessoes.cs` (emitir, trocar atomicamente, obter ativa, renovar com `Versao` esperada, revogar, revogar todas da conta), `IServicoCredenciais.cs` (verificar senha e lockout com resultado tipado), `IConsultaContextoAcesso.cs` e `ModelosAcesso.cs` (`SessaoEmitida`, `IdentidadeAtual`, `ContextoAcesso`) — sem tipos Identity, `HttpContext` ou `AuthenticationTicket`
- [X] T096 [US2] Implementar casos de uso em `backend/src/Platform.Application/Access/CasosUso/`: `CasoUsoLogin.cs` (instalação ativa, credenciais via porta, `ExpiraEm = CriadoEm + OpcoesSessao.PrazoAbsolutoMinutos` do mesmo `InstanteOperacao`, troca atômica com sessão anterior, `Resultado<SessaoEmitida>` com `ErrosAcesso`), `CasoUsoLogout.cs` (somente a própria sessão; não exige instalação ativa nem permissão), `ValidacaoSessaoAtual.cs` (conta ativa, lockout, stamp, epoch, `agora >= ExpiraEm` gravado), `CasoUsoConsultarIdentidade.cs` e `CasoUsoConsultarContexto.cs` (exige `foundation.context.read` vigente; 409 com instalação inativa)
- [X] T097 [P] [US2] Implementar `RepositorioSessoes` em `backend/src/Platform.Infrastructure/Access/RepositorioSessoes.cs`: alterações rastreadas com `Versao` esperada, sem upsert nem `ExecuteUpdate`; troca anterior→nova numa transação com commit antes do retorno; `DbUpdateConcurrencyException` → `ErrosAcesso.SESSION_CONFLICT`; erro de banco propaga como exceção de dependência (503), nunca “sessão ausente”; revogação remove `TicketProtegido`; Renew com bytes iguais é no-op
- [X] T098 [P] [US2] Implementar `ServicoCredenciaisIdentity` em `backend/src/Platform.Infrastructure/Access/ServicoCredenciaisIdentity.cs`: `GerenciadorUsuariosAplicacao` (G13) com `CheckPasswordAsync`/`AccessFailedAsync`/`IsLockedOutAsync`; até três recargas e reavaliações em `ConcurrencyFailure`, depois falha de dependência; ao confirmar lockout, revogar todas as sessões e `UpdateSecurityStampAsync` na mesma transação; login válido só zera contagem fora do bloqueio; sem fábrica de claims nem tabelas auxiliares
- [X] T099 [P] [US2] Implementar `ConsultaContextoAcesso` (conta, sessão e concessões lidas no banco a cada chamada, sem claims nem cache) em `backend/src/Platform.Infrastructure/Access/ConsultaContextoAcesso.cs`
- [X] T100 [US2] Criar `backend/src/Platform.Infrastructure/InjecaoDependencias.cs` com `AdicionarInfraestruturaAcesso(IServiceCollection, ...)`, reutilizado pela API e pelo host: `AcessoTenantDbContext` com Npgsql, histórico em `foundation` e `InterceptadorAuditoria`; `AddIdentityCore<UsuarioAplicacao>` com stores EF; opções Identity explícitas (lockout habilitado, 5 falhas, 15 minutos; senha 12–128 com maiúscula, minúscula, dígito e não alfanumérico; email único; sem exigir email confirmado); repositórios/serviços de T097–T099; criar `backend/src/Platform.Infrastructure/Access/GerenciadorUsuariosAplicacao.cs` (G13: sobrescreve `AccessFailedAsync` e `IsLockedOutAsync` com `TimeProvider` + `InstanteUtc`, XML summary) e registrá-lo com `AddUserManager<GerenciadorUsuariosAplicacao>()`; mover para cá os registros de `Program.cs`
- [X] T101 [P] [US2] Implementar `ContextoOperacaoHttp : IContextoOperacao` (scoped; `InstanteOperacao` lido uma vez; `IdOperacao` via `IdentificadorV7`; `Anonimo` até `DefinirUsuarioVerificado(idUsuario)`; associa `IdOperacao` ao `traceId` no log) em `backend/src/App.Api/Access/ContextoOperacaoHttp.cs`
- [X] T102 [P] [US2] Implementar `MiddlewareCabecalhoTenant` em `backend/src/App.Api/Access/MiddlewareCabecalhoTenant.cs`, só nas cinco rotas L2 de `/api/v1`: exatamente um `X-Tenant-Id` UUID não vazio (400 `ErrosPlataforma.TENANT_REQUIRED` / 400 `ErrosPlataforma.INPUT_INVALID`); divergente da configuração → 401 `AUTH_INVALID` no login e 403 `TENANT_ACCESS_DENIED` nas demais; sem acesso a banco; header nunca altera conexão
- [X] T128 [US2] Implementar `MiddlewareVerificacaoInstalacao` em `backend/src/App.Api/Access/MiddlewareVerificacaoInstalacao.cs`, só nas cinco rotas L2 de `/api/v1`, executado depois de `MiddlewareCabecalhoTenant` e **antes** da autenticação (G2 revisada): chama `VerificadorIdentidadeBancoTenant` (marcador igual à configuração e cadeia de migrations completa, T072) a cada requisição, sem cache; divergência, versão incompatível ou banco indisponível → 503 `ErrosPlataforma.DEPENDENCY_UNAVAILABLE` sem tocar contas/sessões e sem tentar outro alvo; não verifica `EstadoTenant` (estado é decidido nos casos de uso para permitir antiforgery/logout com instalação inativa); health e OpenAPI fora do escopo
- [X] T103 [US2] Implementar `ArmazenamentoTicketsSessao : ITicketStore` em `backend/src/App.Api/Access/ArmazenamentoTicketsSessao.cs`: chave = referência `IdTenant/EpocaAcesso/IdSessao`; escopo resolvido por chamada via `IHttpContextAccessor` (sem capturar DbContext nas options); `StoreAsync` na unidade de trabalho da requisição; `RetrieveAsync` valida referência, sessão, conta, lockout, stamp, epoch e `ExpiraEm`; `RenewAsync` nunca faz upsert, reativação ou extensão; `RemoveAsync` revoga; erros não viram `null`; o login nunca chama `RenewAsync` com a chave anterior; ticket persistido protegido por Data Protection
- [X] T104 [US2] Criar `backend/src/App.Api/Access/ConfiguracaoAutenticacaoSessao.cs`: cookie `__Host-bps-session` (HttpOnly, Secure, SameSite=Lax, Path `/`, sem Domain) e `bps-session` sem Secure só com `Environment=Development`; `SlidingExpiration=false`; `ExpiresUtc` do ticket e do cookie = `ExpiraEm`; `TimeProvider` das opções do cookie vindo do DI (G11); eventos de redirect → Problem Details 401/403 sem HTML; `OnValidatePrincipal` sem renovar principal rejeitado; antiforgery com cookie `__Host-bps-csrf`/`bps-csrf` e header `X-CSRF-TOKEN`; Data Protection com `SetApplicationName($"{CodigoAplicacao}/{IdAmbiente}/{IdTenant}")`, `PersistKeysToFileSystem(ProtecaoDados:DiretorioChaves)` e certificado de `ProtecaoDados:ArquivoCertificado`/`ArquivoSenhaCertificado` obrigatório fora de Development (falha na inicialização sem ele); `OpcoesSessao` com `ValidateOnStart`
- [X] T105 [US2] Criar `backend/src/App.Api/Access/ConfiguracaoLimiteLogin.cs`: política nativa de janela fixa (5 por minuto, fila 0) particionada pelo IP efetivo (`RemoteIpAddress`; nulo usa uma partição única fixa, nunca isenção, G11), só no login; rejeição 429 `ErrosPlataforma.RATE_LIMITED` com `Retry-After`; `ForwardedHeaders` registrado somente quando `CabecalhosEncaminhados:ProxiesConfiaveis` não está vazio (G7)
- [X] T106 [US2] Implementar `backend/src/App.Api/Access/EndpointsAcesso.cs` e `backend/src/App.Api/Access/ModelosRespostaAcesso.cs`: GET `/api/v1/auth/antiforgery` (`GetAndStoreTokens`; cookie inválido = anônimo, sem mascarar falha de dependência), POST `/api/v1/auth/login` e `/api/v1/auth/logout` com `ValidateRequestAsync` explícito na posição G2 (logout com `RequireAuthorization` de recurso próprio, de modo que sessão ausente/inválida dê 401 antes de corpo e CSRF, G9), `SignIn` só após commit, logout 204 só após commit e então remover cookie, GET `/api/v1/me` com autorização de recurso próprio e GET `/api/v1/context` com `foundation.context.read`; DTOs camelCase do contrato (`requestToken`/`headerName`, `tenantId`/`expiresAt`, `CurrentUser`, `TenantContext` com `applicationVersion` = `AssemblyInformationalVersion`); `Cache-Control: no-store`; erros via `ConfiguracaoProblemDetails.DeErro`
- [X] T107 [US2] Recompor o pipeline em `backend/src/App.Api/Program.cs`: Problem Details → `/health/*` e `/openapi/v1.json` fora de autenticação/sessão → forwarded condicional → rate limiter → `MiddlewareCabecalhoTenant` → `MiddlewareVerificacaoInstalacao` (T128) → autenticação → autorização (logout sem sessão para aqui com 401, G9) → grupo `/api/v1` com `FiltroValidacaoRequisicao`; `AdicionarInfraestruturaAcesso` e `ContextoOperacaoHttp` (o `TimeProvider.System` já foi registrado em T061; apenas conferir que não há segundo registro); nenhum job/`IHostedService` de limpeza
- [X] T108 [US2] Atualizar a saúde: liveness sem banco nem chaves; readiness em `backend/src/App.Api/Health/VerificacaoSaudeProntidaoInstalacao.cs` exige cadeia de migrations completa (T072) e round-trip de proteção por novo `backend/src/App.Api/Health/VerificacaoProtecaoChaves.cs` (chaves indisponíveis → 503 mesmo com banco pronto); nenhuma das duas consulta contas/sessões

### Contrato publicado junto dos endpoints (Constituição V) — mesmo commit/PR de T106

> Movidas da Phase 17 na remediação do `/speckit-analyze` (2026-11-08): o OpenAPI DEVE mudar no mesmo conjunto de alterações dos endpoints, e o teste L1 que espera 404 nas rotas L2 quebraria a Phase 14 se ficasse para depois.

- [X] T121 [US5] Criar `backend/src/App.Api/OpenApi/TransformadorDocumentoOpenApi.cs` e declarar `Produces`/`ProducesProblem` em `EndpointsAcesso.cs`: título/versão/descrição L2 (sem texto “I1”), esquema de segurança cookie, `ProblemDetails` com o enum de códigos, headers `X-Tenant-Id`/`X-CSRF-TOKEN`; registrar em `backend/src/App.Api/Program.cs`
- [X] T122 [US5] Promover o contrato: substituir `specs/001-fullstack-foundation/contracts/openapi.json` pelo conteúdo L2 de `future-openapi.json` (sem `x-planned-only`, `x-increment: "I2"`, versão `2.0.0`), excluir `specs/001-fullstack-foundation/contracts/future-openapi.json` e atualizar os links em `specs/001-fullstack-foundation/contracts/operations.md`, `specs/001-fullstack-foundation/quickstart.md`, `specs/001-fullstack-foundation/plan.md` e `docs/adr/0003-tratamento-de-erros-e-validacao.md`
- [X] T119 [US5] Atualizar `backend/tests/Platform.ContractTests/Health/TestesContratoHealthEOpenApi.cs`: o documento servido tem exatamente as sete operações (`getAntiforgeryToken`, `login`, `logout`, `getCurrentUser`, `getTenantContext`, `getLiveness`, `getReadiness`), os status de cada uma conforme o contrato, o esquema `SessionCookie`, o enum de `code` da plataforma (igual a `ErrosAcesso` ∪ `ErrosPlataforma`) e nenhum `x-planned-only`; remover as asserções L1 de que as rotas L2 retornam 404; `/api/v1/me/tenants` continua 404. Adaptar o host dos ContractTests às exigências de T104/T108 numa factory compartilhada em `backend/tests/Platform.ContractTests/Suporte/FabricaApiContrato.cs`: certificado de teste gerado na execução para Production (`ProtecaoDados:ArquivoCertificado`/`ArquivoSenhaCertificado`), diretório temporário de chaves por instância, opções obrigatórias de sessão e conexão; migrar para ela `TestesContratoHealthEOpenApi.cs` (`CriarAplicacaoCom`) e `backend/tests/Platform.ContractTests/Diagnostics/TestesConfiguracaoProblemDetails.cs` (que já usa `EnvironmentName = "Production"`), mantendo readiness 200 com banco e key ring prontos

**Checkpoint**: US2-HTTP funcional e testada em A/B sem host operacional; `/openapi/v1.json` e `contracts/openapi.json` iguais à implementação; `dotnet test` verde.

> **Resultado — Parada L2-2 concluída (2026-11-08)**: `restore --locked-mode`, build (0 avisos), `dotnet format --verify-no-changes` e `dotnet test --solution` 334/334 (Unit 165, Integration 114 com PostgreSQL 18.6 via Testcontainers, Contract 55). Emissão do cookie de sessão pelo endpoint após o commit da troca atômica (G21); `StoreAsync` não é usado. Header `X-Tenant-Id` vazio equivale a ausente (`TENANT_REQUIRED`), como em G10. `XmlKeyDecryptionOptions` é interno no .NET 10: o certificado do key ring é lido na composição e registrado por `ProtectKeysWithCertificate`; a validação na inicialização (`ValidateOnStart`) recusa ausência ou certificado ilegível fora de Development. Para evitar autenticação automática fora das rotas L2, o esquema padrão de autenticação não autentica (`bps-nenhum`) e o cookie é autenticado só nas cinco rotas, depois de tenant e marcador.

---

## Phase 15: User Story 2 — Operação restrita e `sessao limpar` (Priority: P1)

**Goal**: Host `Platform.Operations` com os oito comandos de operations §5, autorização por login PostgreSQL individual, auditoria atômica e limpeza de sessões (plan P3; FR-033; SC-002 partes de auditoria e limpeza).

**Independent Test**: executar os casos de uso com entrada sintética (senha por provedor de teste, sem console) contra A/B com os logins `<banco>_op_um`/`<banco>_op_dois`; verificar efeitos, auditoria, exit codes e ausência de efeito em B. Não depende das rotas HTTP da Phase 14, mas reutiliza seus adaptadores T097 (`RepositorioSessoes`), T098 e T100 (DI); executar depois da Phase 14.

### Tests for User Story 2 — operação ⚠️

- [X] T109 [P] [US2] Testes do interpretador em `backend/tests/Platform.UnitTests/Operations/TestesInterpretadorComandos.cs` (referenciar `backend/src/Platform.Operations/Platform.Operations.csproj` em `backend/tests/Platform.UnitTests/Platform.UnitTests.csproj`): os oito comandos com argumentos válidos; parâmetro desconhecido, duplicado ou ausente falha; UUID vazio falha; `--actor`, `--senha`/`--password` e `--retencao-dias` rejeitados; `--codigo` desconhecido falha
- [X] T110 [P] [US2] Testes dos comandos de conta/permissão em `backend/tests/Platform.IntegrationTests/Operacoes/TestesOperacoesContaPermissao.cs`: `criar` (sem concessão automática, política de senha 12–128 e regras nativas, Id v7); `desativar`, `redefinir-senha` e `revogar-sessoes` revogam todas as sessões e trocam stamp na mesma transação da auditoria; `reativar` não ressuscita sessões; reset remove lockout/contagem; concessão duplicada e revogação ausente → no-op auditado; concessão concorrente (violação de PK), retirada concorrente (zero linhas) ou `ConcurrencyFailure` → `falha`/`CONFLITO_CONCORRENTE` com exit não zero, sem repetição; login runtime ou owner → `negado` antes de mutação; alvo divergente negado; falha no INSERT da auditoria → rollback; `op_um` e `op_dois` aparecem com seus `LoginOperador`; conta e concessões de B idênticas
- [X] T111 [P] [US2] Testes de `sessao limpar` em `backend/tests/Platform.IntegrationTests/Operacoes/TestesLimpezaSessoes.cs` com `RelogioControlavel`: fixture com sessões ativa, expirada e revogada encerradas em `corte − 1µs`, exatamente no corte e depois → só as anteriores ao corte removidas; um registro `Sucesso` com `RetencaoDias`, `InstanteCorte`, `QuantidadeRemovida`, `LoginOperador` e `IdOperacao`, sem `IdUsuarioAlvo`/`CodigoPermissao`; nenhuma elegível → `sucesso` com 0 e um registro; falha injetada → rollback total, um registro `Falha` com 0 e exit não zero; retenção `0`, `366` ou não inteira → `RETENCAO_INVALIDA` antes de conectar, sem registro (não é execução auditável, G12); vazia = 30; `1` e `365` aceitas; concorrência com login/logout/Renew não remove sessão válida; runtime não consegue DELETE

### Implementation for User Story 2 — operação

- [X] T112 [P] [US2] Criar portas em `backend/src/Platform.Application/Access/Operacoes/Portas/`: `IManutencaoContas.cs`, `IRegistroOperacoesAcesso.cs`, `IVerificadorAutorizacaoOperador.cs`, `IRepositorioLimpezaSessoes.cs` e `IProvedorSenha.cs` (senha + confirmação; implementação de console no host, sintética nos testes)
- [X] T113 [US2] Implementar os casos de uso em `backend/src/Platform.Application/Access/Operacoes/`: `CasoUsoCriarConta.cs`, `CasoUsoDesativarConta.cs`, `CasoUsoReativarConta.cs`, `CasoUsoRedefinirSenha.cs`, `CasoUsoRevogarSessoes.cs`, `CasoUsoConcederPermissao.cs`, `CasoUsoRevogarPermissao.cs` e `CasoUsoLimparSessoes.cs` (`corte = InstanteOperacao − RetencaoSessoesDias`; elegível só `RevogadaEm < corte` ou `RevogadaEm` nulo e `ExpiraEm < corte`), mais `ResultadoOperacao.cs` (`sucesso`/`negado`/`falha`, código estável); mutação e registro de sucesso na mesma transação, registro `Falha`/`Negado` em transação separada quando o alvo já foi validado
- [X] T114 [P] [US2] Implementar em `backend/src/Platform.Infrastructure/Access/Operacoes/`: `ManutencaoContasIdentity.cs` (`GerenciadorUsuariosAplicacao`, G13; Id atribuído antes de `CreateAsync`; revogação de todas as sessões **reutilizando** `IRepositorioSessoes.RevogarTodasDaConta` de T097 + `UpdateSecurityStampAsync`, sem segunda implementação de revogação), `RegistroOperacoesAcesso.cs`, `VerificadorAutorizacaoOperador.cs` (`session_user = current_user`, igual a `OperacaoAcesso:LoginAutorizado` e membro de `<banco>_operador`; preflight de marcador, `IdTenant`/`IdAmbiente` dos argumentos e cadeia de migrations; não exige instalação ativa) e `RepositorioLimpezaSessoes.cs` (projeta `IdSessao`/`Versao` sem carregar tickets, anexa stubs como `Deleted` com `Versao` esperada, sem `ExecuteDelete`)
- [X] T115 [US2] Implementar o host em `backend/src/Platform.Operations/`: `Program.cs` (`Host.CreateApplicationBuilder`, sem servidor HTTP; `AdicionarInfraestruturaAcesso`; `TimeProvider.System`; `OpcoesOperacaoAcesso` validada antes de conectar), `InterpretadorComandos.cs` (parse manual, sem biblioteca de CLI), `LeitorSenhaConsole.cs` (`IProvedorSenha` sem eco, com confirmação; nunca argv/env/stdout), `ContextoOperacaoOperador.cs` (`TipoAutor.Operador`, `Autor` = login verificado, `IdOperacao` v7) e `SaidaResultadoJson.cs` (um objeto JSON em stdout com `idOperacao`, `idTenant`, `idAmbiente`, `idUsuario` quando houver, `loginOperador`, `operacao`, `resultado`, `codigo`, `registradaEm` e, em `sessao limpar`, `retencaoDias`, `instanteCorte`, `quantidadeRemovida`; sem email/nome/senha/ticket/conexão; diagnóstico correlacionado em stderr; exit 0 só para sucesso persistido)

**Checkpoint**: FR-033 completo; US2 inteira (HTTP + operação) cobre SC-002.

> **Resultado — Parada L2-3 concluída (2026-11-11)**: `restore --locked-mode`, build (0 avisos), `dotnet format --verify-no-changes` e `dotnet test --solution` 377/377 aprovados (Unit 184, Integration 138 com PostgreSQL 18.6 via Testcontainers, Contract 55). Host `Platform.Operations` com os oito comandos de operador (`criar`, `desativar`, `reativar`, `redefinir-senha`, `revogar-sessoes`, `conceder`, `revogar`, `sessao limpar`) implementado e validado. Mutações e auditoria de sucesso transacionadas atomicamente na mesma transação; falhas pós-validação registradas em transação independente. Permissões mínimas de DML e isolamento de banco comprovados.

---

## Phase 16: User Story 3 — Estado da instalação, marcador e carga isolada SC-003 (Priority: P1)

**Goal**: Provar marcador/estado antes do acesso, bloqueio de versão incompatível e 200 operações concorrentes sem acesso cruzado (FR-009, FR-011–FR-016; SC-003; G3).

**Independent Test**: com A e B ativas e sessões autenticadas antes da carga, executar a suíte SC-003 e os cenários de marcador/estado; registrar quantitativos e pico.

- [X] T116 [P] [US3] Testes em `backend/tests/Platform.IntegrationTests/Tenancy/TestesEstadoInstalacaoEMarcador.cs`: marcador divergente, banco indisponível e histórico com ID desconhecido → 503 `DEPENDENCY_UNAVAILABLE` nas cinco rotas e na readiness (nunca 200, identidade vazia ou 401 silencioso); configuração de A apontando para B bloqueia antes de dados; inativação suspende consultas sem revogar sessões nem mudar epoch/prazo; reativação retoma só sessões válidas e não revogadas
- [X] T117 [US3] Criar o suporte sintético exclusivo de teste em `backend/tests/Platform.IntegrationTests/Carga/SuporteCargaSintetica.cs`: a fixture cria `teste_sc003.registros_sinteticos` (`IdRegistro` uuid PK, `Valor` text, `GravadoEm` timestamptz) com o owner e concede `USAGE` no schema `teste_sc003` e SELECT/INSERT/UPDATE na tabela ao runtime só no teste; rota GET/POST `/api/v1/teste-sc003/registros/{id}` registrada apenas em `FabricaApiInstalacao` via `ConfigureTestServices`, passando pelo mesmo `MiddlewareCabecalhoTenant`, autenticação de sessão e exigência de `foundation.context.read` das rotas reais. Se isso exigir ponto de extensão em `backend/src/App.Api/Program.cs`, ele deve ser inerte sem registro do teste e coberto por teste que prova a ausência da rota na API distribuída
- [X] T118 [US3] Teste SC-003 em `backend/tests/Platform.IntegrationTests/Carga/TestesCargaIsolamentoSc003.cs`: autenticar em A e B antes da carga (limiter fora do cenário); 200 operações, 100 por instalação, leituras e gravações intercaladas com os mesmos `IdRegistro` em A e B; barreira + contador provando pico efetivo ≥ 20 com A e B sobrepostos; falha provocada em A com rollback verificado; zero alteração ou leitura cruzada em B, inclusive com reutilização de conexões do pool, escopos de DI e clientes HTTP entre requisições de A e de B na mesma execução; resultados válidos conferidos no banco (negar tudo reprova); registrar no output total, pico e resultados, sem segredos

**Checkpoint**: SC-003 comprovado; US3 aceita no recorte L2.

> **Resultado — Parada L2-4 concluída (2026-11-11)**: SC-003 comprovado com 200 operações concorrentes entre A e B (100 por instalação) e pico de concorrência efetivo ≥ 20 sem vazamento cruzado de dados ou conexão, e com rollback verificado em A. Estado da instalação e preflight de marcador validados com retorno 503 nas cinco rotas e na readiness quando divergente.

---

## Phase 17: User Story 5 — Contrato publicado e consumidor independente (Priority: P2)

**Goal**: Provar SC-005 com consumidor que usa só HTTP e o contrato já promovido na Phase 14 (T119/T121/T122) (FR-020–FR-023).

**Independent Test**: um consumidor só com `HttpClient` + `CookieContainer` completa a jornada e distingue 400/401/403/409/503 pelo `code`.

- [X] T120 [US5] Teste do consumidor independente em `backend/tests/Platform.IntegrationTests/Consumidor/TestesConsumidorIndependente.cs`: o host vem de `FabricaApiInstalacao`, mas a interação usa só `HttpClient` + `CookieContainer` e valida os corpos contra os esquemas lidos de `specs/001-fullstack-foundation/contracts/openapi.json`, sem tipos de `App.Api`; jornada completa A e B em jars separados; reconhece `INPUT_INVALID`, `CSRF_INVALID`, `AUTH_REQUIRED`, `TENANT_ACCESS_DENIED` e `TENANT_UNAVAILABLE`; `traceId` presente; nenhum segredo nas respostas

**Checkpoint**: SC-005 comprovado.

> **Resultado (2026-11-11)**: SC-005 comprovado com consumidor independente HTTP (`HttpClient` + `CookieContainer`), sem tipos de `App.Api`, validando esquemas contra `openapi.json`, reconhecendo os códigos de erro e confirmando ausência de segredos.

---

## Phase 18: Polish & Cross-Cutting (US1/US6 — reprodutibilidade, CI, documentação e evidências)

**Purpose**: Documentação L2, CI cobrindo o novo projeto e a infraestrutura, e evidências SC-006/SC-008.

- [X] T123 [P] Atualizar `README.md`: roles runtime/operador/owner por instalação e logins individuais de operador; variáveis do `.env.local` (G4) e recriação local com `down --volumes`; modo Development + loopback e HTTPS fora dele; key ring por instalação e certificado em produção; perfil `operations`, os oito comandos e `sessao limpar` (retenção, sem job na API); `Sessao__PrazoAbsolutoMinutos` (5–480, padrão 30); seção “Dados pessoais” (Constituição VI): campos pessoais existentes (`Email`, `NomeExibicao`), onde ficam (banco da própria instalação), o que nunca vai para logs, respostas de erro ou saída do host operacional, e que retenção e base legal são definidas pelo produto derivado; separação L2/L3/L4
- [X] T124 [P] Atualizar `specs/001-fullstack-foundation/quickstart.md`: marcar a seção L2 como implementada, ajustar comandos aos nomes reais (variáveis G4, roles derivadas do banco G6, serviço `operations`) e manter a matriz §6 como roteiro de aceite
- [X] T125 [P] Atualizar `.github/workflows/backend-ci.yml`: gatilhos também em `infra/**`; etapa `docker compose -f infra/compose.yaml config --quiet` com valores fictícios gerados na execução (não versionados); confirmar que build/format/test/auditoria/gitleaks incluem `Platform.Operations`; manter actions fixadas por SHA e permissões mínimas
- [X] T126 Executar `specs/001-fullstack-foundation/quickstart.md` §1, §2–§5 (A/B locais) e §7: restore `--locked-mode`, build, format, testes (registrar SDK de `dotnet --version`, quantidade por suíte, SC-002, SC-003 com total/pico, SC-005); exercícios negativos SC-006 — compilação, formatação, regressão controlada de revogação de sessão, GRANT de DELETE indevido ao runtime, secret fictício e uma execução sem nenhum teste descoberto, cada um devendo reprovar a validação — e confirmar retorno ao estado válido; registrar a evidência num checkpoint ao fim desta fase, sem segredos
- [X] T127 Conferir SC-008: cada tópico de FR-024 aplicável à L2 (instalação, configuração, identidades, permissões, empresas, testes, contratos, decisões) tem instrução localizável em `README.md`, `specs/001-fullstack-foundation/quickstart.md`, `specs/001-fullstack-foundation/contracts/operations.md` ou ADRs; corrigir lacunas no próprio arquivo
- [X] T131 Criar `.github/pull_request_template.md` (FR-028; Constituição, Fluxo de desenvolvimento): seções objetivo, riscos, testes executados e evidências, documentação afetada, impacto em contratos/OpenAPI e em tenants, dependências novas com justificativa/ADR, e checklist dos critérios da tabela “Restrições e critérios de qualidade” com campo para justificar itens não aplicáveis

- [X] T132 Corrigir a autoria do host operacional (G28): criar `backend/src/Platform.Application/Auditoria/IRegistroOperadorVerificado.cs`; `ContextoOperacaoOperador` sem login no construtor, implementando a interface (rejeita login vazio ou diferente do já definido); `VerificadorAutorizacaoOperador` define o login confirmado antes de retornar sucesso; `backend/src/Platform.Operations/ServicosHostOperacoes.cs` compartilhado por `Program.cs` e `EscopoOperacoesTeste`; `InterceptadorAuditoria` rejeita tipo/autor incoerentes antes do banco. Testes: autoria do operador em `conta criar`, `conta desativar` e `permissao conceder`; regressão do interceptador; unidade do contexto e da composição do host

- [X] T133 Classificar falhas do host operacional (G29): porta `backend/src/Platform.Application/Access/Operacoes/Portas/IClassificadorFalhaPersistencia.cs` e `FalhaOperacaoClassificada.cs`; `backend/src/Platform.Infrastructure/Access/Operacoes/ClassificadorFalhaPersistencia.cs` (EF/Npgsql); os 7 casos de uso de conta/permissão usam o código classificado e o diagnóstico só em stderr; `ManutencaoContasIdentity` com `EMAIL_JA_CADASTRADO`, `EMAIL_INVALIDO`, `ConcurrencyFailure` → conflito e demais erros → `FALHA_INTERNA`; `catch` de concessão filtrado pelo classificador; `EscopoOperacoesTeste` com `substituirServicos` opcional. Testes: unidade do classificador; integração de email duplicado, exceção inesperada e conflito injetado, com o código auditado

**Checkpoint (Polish)**: documentação L2, CI com Compose e `Platform.Operations`, template de PR e evidências SC-006/SC-008.

> **Resultado — Parada L2-5 concluída (2026-11-12)**, Windows 11 + Docker 29.9.3, sem segredos:
>
> - **§1 gates locais** (`dotnet --version` = **10.0.403**): `tool restore`, `restore --locked-mode` (com o `nuget.config` da raiz, G25), `build` (0 avisos/0 erros), `format --verify-no-changes` OK; `dotnet test --solution ... --minimum-expected-tests 1` = **388 aprovados / 0 falhas** — `Platform.UnitTests` 184, `Platform.ContractTests` 57, `Platform.IntegrationTests` 147 (PostgreSQL real; inclui SC-002 em `Http/*` e `Tenancy/*`).
> - **SC-003**: `[SC-003 EVIDENCIA] Total=200, OpsA=100, OpsB=100, PicoEfetivo=200, Erros=0`, com rollback verificado em A e zero leitura/gravação cruzada (G22; 3 execuções consecutivas verdes após a correção, antes 1 de 2 falhava com 53300).
> - **SC-005**: `Consumidor/TestesConsumidorIndependente` aprovado (só `HttpClient` + `CookieContainer`, esquemas de `openapi.json`).
> - **§2–§5 A/B locais** (`boilerplate-a`/`boilerplate-b`, volumes novos, removidos ao final): `config --quiet` A/B OK; imagens `api`/`migrations`/`operations` construídas; `postgres` sem porta publicada e APIs só em `127.0.0.1:8080/8081`; `init-db.sh` criou `<banco>_owner`/`_runtime`/`_operador` (NOLOGIN) + login individual `<banco>_op_local` membro do grupo, CONNECT só para as três roles; migration inicial aplicada em A e B, reaplicação = “No migrations were applied”, alvo B na configuração A rejeitado antes de DDL (exit 1); runtime sem DELETE em sessões e sem DDL, operador sem UPDATE em auditoria (`permission denied`); `sessao limpar` A e B = `sucesso`, `quantidadeRemovida=0`, `loginOperador` individual, um registro por instalação; usuário inexistente = `falha`/`USUARIO_NAO_ENCONTRADO`/exit 1 auditado; alvo divergente = `ALVO_DIVERGENTE`; `--senha` = `ARGUMENTO_INVALIDO`; health live/ready 200 em A e B; `/openapi/v1.json` com as 7 operações e sem `x-planned-only`; HTTP real: sem header 400 `TENANT_REQUIRED`, header inválido 400 `INPUT_INVALID`, tenant B em A 403 `TENANT_ACCESS_DENIED`, `/me` e `/context` sem sessão 401 `AUTH_REQUIRED`, antiforgery 200 com cookie `bps-csrf` e `Cache-Control: no-store`, login sem CSRF 400 `CSRF_INVALID`, conta inexistente 401 `AUTH_INVALID`, campo extra 400 `INPUT_INVALID`, logout sem sessão 401, rota futura 404, 6º login no minuto 429 + `Retry-After: 60` sem afetar B; logs das APIs sem string de conexão, senha ou email. **Não executado aqui**: `conta criar`/`redefinir-senha` e a jornada de login com conta real exigem TTY (prompt sem eco); cobertos pelos testes automatizados (US2/SC-005) e a executar manualmente pelo roteiro §4–§5.
> - **§7 / SC-006 — exercícios negativos** (cada um reprovou e o estado foi restaurado): (1) erro de compilação → `build` exit 1; (2) indentação fora do padrão → `format` exit 2; (3) logout sem gravar `RevogadaEm` → `TestesSessaoExpiracaoRevogacao` 2/18 falhas; (4) `DELETE` em `access.sessoes_autenticacao` concedido ao runtime na migration → `TestesPrivilegiosERestricoes` 1/30 falha; (5) token fictício no formato PAT → gitleaks `leaks found: 1`, exit 1; (6) filtro sem testes → “Violação de política de testes mínimos esperados”, exit 8. Retorno: build/format OK e as duas classes afetadas 48/48 aprovadas.
> - **Segurança**: gitleaks v8.24.3 no histórico (76 commits) = 2 falsos-positivos triados (G26), depois “no leaks found”; correção de 500→503 em falha de banco embrulhada (G23); perfil host-local sem credencial (G24). CI validado localmente no passo de Compose (valores gerados; `.env.example` sozinho falha).
> - **SC-008 (T127)** — FR-024 aplicável à L2: instalação → README “Execução local” e quickstart §2–§3; configuração → README tabela de variáveis, data-model §6, operations §1; identidades → README “Identidades do PostgreSQL”, operations §3; permissões → README comandos `permissao`, data-model §7; empresas → README “Arquitetura” e A/B, ADR 0001; testes → README “Build e testes”, quickstart §1/§6/§7; contratos → `contracts/openapi.json`, operations §4–§5; decisões → ADRs 0001–0003 e tabela G1–G27. Extensão modular (L3), publicação e recuperação (L4) fora do recorte, declarados no README e em operations §6. Lacunas corrigidas: operations §5 ainda dizia “contrato futuro”, README da L1 (rotas `/auth/login`, F5 com owner), status das ADRs 0002/0003.
>
> **Correção pós-checkpoint (2026-11-15, G28/T132)**: a afirmação acima de que `conta criar` estava “coberta pelos testes automatizados” não valia para o host real — a fixture injetava o login que o `Program.cs` não fornecia, e a primeira execução manual do §4 falhou com `CONFLITO_CONCORRENTE`. Após T132, com SDK 10.0.404: build 0 avisos/0 erros, `format --verify-no-changes` OK, `dotnet test --solution` **396 aprovados / 0 falhas** (8 testes novos). O roteiro manual §4–§5 e a matriz §6 continuam por executar.
>
> **G29/T133 (2026-11-15)**: códigos de falha do host restritos (`CONFLITO_CONCORRENTE` só para conflito real; `FALHA_INTERNA`, `EMAIL_JA_CADASTRADO`, `EMAIL_INVALIDO`). Com SDK 10.0.404: build 0 avisos/0 erros, `format --verify-no-changes` OK, `dotnet test --solution` **410 aprovados / 0 falhas** (14 testes novos). Roteiro manual §4–§5 e matriz §6 ainda por executar.
>
> **Validação manual G28/G29 no host real (2026-11-15)**: imagens `operations` A/B reconstruídas (`--profile operations`); `conta criar` com senha interativa → A `SUCESSO` (exit 0), email repetido em A `EMAIL_JA_CADASTRADO` (exit 1), B `SUCESSO` (exit 0); registros em `access.operacoes_acesso` com os mesmos códigos e `LoginOperador` = login individual de cada instalação. Registros anteriores com `CONFLITO_CONCORRENTE` vieram de imagem `operations` desatualizada. Cobre apenas `conta criar` do §4; demais comandos do §4, §5 e §6 continuam por executar.
>
> **Roteiro manual quickstart §1 (2026-11-15, commit `1b9f91d`)**: SDK 10.0.404; Docker 29.9.3 (Desktop 4.99.0, Compose v5.7.0, linux/x86_64, WSL2) responsivo; `dotnet tool restore` OK (`dotnet-ef` 10.0.12); `restore --locked-mode` OK; `build --no-restore` êxito nos 8 projetos; `format --verify-no-changes` sem alterações; `dotnet test --solution` **410 aprovados / 0 falhas / 0 ignorados** (Unit 2,9s, Contract 8,3s, Integration 1m06s; total 1m08s). §2–§6 por executar.
>
> **Roteiro manual quickstart §2–§3 (2026-11-15)**: `config --quiet` A/B sem saída; `build api migrations operations` A/B com as 3 imagens `Built`; `down --volumes` A/B (volumes `dados_postgres` removidos) e bancos recriados do zero pelo `init-db.sh`. Migration com `Migracoes__PermitirInicializarBancoVazio=true` só nesta execução (de volta a `false` em ambos os arquivos após), `EstadoTenant=Inativo`: A e B `Applying migration '20261108234429_EsquemaInicialAcessoTenant'` → `Done.` (o `Failed executing DbCommand` sobre `__EFMigrationsHistory` é a sondagem esperada do EF em banco vazio). Conferência via `psql`: marcador `foundation.identidade_banco` A = `aaaaaaaa…/11111111…` e B = `bbbbbbbb…/22222222…`; histórico só com a migration inicial; 7 tabelas de `access` com 0 linhas; roles `<BANCO_APP>_owner`, `_runtime`, `_operador` e login individual `_op_local` em cada instalação. §4–§6 por executar.
>
> **Roteiro manual quickstart §4 (2026-11-16)**: `conta criar` (senhas distintas por instalação, prompt sem eco) → A `idUsuario` `01a1e1c7-b3a5-7f7d-8c3b-4bbd1a113597` e B `01a1e1c8-8f28-768d-9136-fd1ec770d2fb`, ambos `sucesso/SUCESSO`; `permissao conceder foundation.context.read` → A e B `sucesso/SUCESSO`. `access.operacoes_acesso` em cada banco: `CriarConta` e `ConcederPermissao` `Sucesso|SUCESSO` com `LoginOperador` = `appdb_cliente_a_op_local` / `appdb_cliente_b_op_local`; 1 conta ativa e 1 permissão por instalação. `EstadoTenant=Ativo` (flag de banco vazio `false`), `up -d api` A/B (key ring em volume `chaves_protecao_dados` por instalação) e `/health/live` A `alive`, `/health/ready` A e B `ready`. §5–§6 por executar.
>
> **Roteiro manual quickstart §5 — SC-005 (2026-11-16)**: consumidor PowerShell independente (cookie jars separados `sessaoA`/`sessaoB`, credencial via `Get-Credential` removida após o login, nada salvo em arquivo). A e B: token CSRF obtido; `POST /api/v1/sessions` com `tenantId` da própria instalação e `expiresAt`; `/me` com o `idUsuario` do §4, `tenantId` próprio e `displayName` "Teste A"/"Teste B" (mesmo email nas duas instalações); `/context` só com dados da própria instalação; `DELETE /api/v1/sessions/current` → `204`. `/openapi/v1.json` em A e B sem `x-planned-only` e com as mesmas 7 operações de `contracts/openapi.json` (0 diferenças). §6 por executar.

---

## Dependencies & Execution Order (L2)

> Execução **sequencial** (decisão do mantenedor em 2026-11-08): as fases e as tarefas rodam na ordem abaixo. Marcadores `[P]` remanescentes indicam apenas independência de arquivos, não um plano de paralelização.

### Phase Dependencies

- **Phase 9 (Setup)**: depende da L1 entregue. T040 antes de T086/T088; T043 antes de T109/T115.
- **Phase 10 (P0)**: depende da Phase 9. **Bloqueia** Phases 11–18.
- **Phase 11 (P1a)**: depende da Phase 10. T055–T060 antes de T062–T069; T065 antes de T066–T068; T069/T070 antes de T073. T061 é a primeira edição L2 de `Program.cs`.
- **Phase 12 (P1b)**: depende da Phase 11. T073 → T074 → T075 → T076–T079.
- **Phase 13 (P1c)**: depende da Phase 12 e de T040 (T088 edita `Program.cs` depois de T061).
- **Phase 14 (US2 HTTP + contrato)**: depende das Phases 12 e 13. Ordem: T089 → testes T090–T094, T129, T130 → T095 → T096 → T097–T099 → T100 → T101 → T102 → T128 → T103 → T104 → T105 → T106 → T107 → T108 → T121 → T122 → T119. T106, T121, T122 e T119 entram no **mesmo commit/PR** (Constituição V).
- **Phase 15 (US2 operação)**: depende da Phase 14 (reutiliza T097, T098 e T100; não usa as rotas HTTP).
- **Phase 16 (US3)**: depende da Phase 14 (rotas, sessão e T128 reais).
- **Phase 17 (US5)**: depende da Phase 14 (contrato já promovido por T122).
- **Phase 18 (Polish)**: depende das Phases 14–17.

### User Story Dependencies

- **US2** (P1): base de toda a L2. A parte HTTP (Phase 14) e a operação (Phase 15) são testáveis de forma independente — a HTTP usa contas criadas pela fixture e a operação não usa HTTP —, mas a operação reutiliza adaptadores da Phase 14.
- **US3** (P1): reutiliza o pipeline de US2-HTTP; testável sem US5.
- **US5** (P2): reutiliza US2-HTTP e o contrato promovido na Phase 14; testável sem US3.

### Within Each Phase

- Testes escritos primeiro e falhando; portas → casos de uso → adaptadores → composição (`Program.cs`/host).

---

## Implementation Strategy (L2)

### MVP (US2 HTTP)

1. Phase 9 → Phase 10 (P0, gate de segurança) → Phase 11 → Phase 12 (modelo + migration) → Phase 13 (erros).
2. Phase 14: login/logout/`/me`/`/context` com todos os controles e contrato OpenAPI promovido no mesmo conjunto de alterações.
3. **PARAR e VALIDAR**: matriz HTTP de SC-002 em A/B. Login só é considerado liberado com P0 e P1 completos.

### Entrega incremental por parada

1. **Parada L2-1 — P0 + P1a/P1b/P1c (T040–T088)**: infraestrutura segura, modelo completo, migration única, erros/validação. SC-009 reprovado sobre a nova base.
2. **Parada L2-2 — US2 HTTP + contrato (T089–T108, T128–T130, T121, T122, T119)**: superfície autenticada e OpenAPI publicado juntos.
3. **Parada L2-3 — US2 operação (T109–T115)**: FR-033 e `sessao limpar`.
4. **Parada L2-4 — US3 + US5 (T116–T118, T120)**: SC-003 e SC-005.
5. **Parada L2-5 — Polish (T123–T127, T131)**: documentação, CI, template de PR e evidências SC-006/SC-008.

L3 (frontend, extensão modular, T035) e L4 (Dokploy/VPS, backups, agendamento externo de `sessao limpar`) continuam fora deste recorte.

---

## Notes

- [P] = arquivos diferentes, sem dependência pendente; [Story] mapeia a tarefa à user story.
- IDs seguem estáveis: T119, T121 e T122 foram movidos da Phase 17 para a Phase 14; T128–T131 foram acrescentados na remediação do `/speckit-analyze` (2026-11-08). A segunda rodada do `/speckit-analyze` (2026-11-08) acrescentou G8–G12 e ajustou T041, T042, T046, T052, T060, T082 (movida para ContractTests), T089, T090, T093, T104–T107, T111 e T119, sem novos IDs. A terceira rodada acrescentou G13–G14, renomeou `OperacaoAcesso:PapelAutorizado` para `OperacaoAcesso:LoginAutorizado` e ajustou T042, T043, T064, T089, T092, T098, T100, T114, T117, T118, T123 e T129.
- Restrições do data-model citadas entre aspas nas tarefas não ficam a critério da implementação.
- Nunca relaxar cookie seguro, usar owner na API ou apagar volume com dados para contornar falha (P0).
- Commit por tarefa ou grupo lógico, exceto endpoints + OpenAPI servido + contrato promovido (T106, T121, T122, T119), que entram juntos; validar em cada checkpoint.

---


# Histórico L1 — Recorte I1 (concluído)

> Preservado sem alterações. IDs T001–T039 (T035 reservado) não são reutilizados. Nomes em inglês abaixo refletem o estado da época; o código atual usa os nomes em Português Técnico.

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

- [X] T025 [P] [US5] Criar testes de contrato HTTP validando `/health/live`, `/health/ready`, `/openapi/v1.json`, `503 DEPENDENCY_UNAVAILABLE` (quando o marcador do banco diverge ou o banco está indisponível) e `404 NOT_FOUND` em rotas ausentes (`/api/v1/auth/login`, `/api/v1/me`, `/api/v1/context`) em `backend/tests/Platform.ContractTests/Health/TestesContratoHealthEOpenApi.cs`

### Implementation for User Story 5

- [X] T026 [P] [US5] Implementar health checks de liveness (sem acesso a banco ou cookies) e readiness (validando `InstallationTenantOptions` e `ITenantDatabaseIdentityProbe` sem rodar migrations no startup) em `backend/src/App.Api/Health/VerificacaoSaudeProntidaoInstalacao.cs`
- [X] T027 [US5] Configurar geração de OpenAPI 3.0.3 (`/openapi/v1.json`), mapeamento de `/health/live` e `/health/ready` e fallback `404 Problem Details` em `backend/src/App.Api/Program.cs`

**Checkpoint**: API executável, expondo exclusivamente a superfície contratada para o I1.

---

## Phase 6: User Story 1 - Execução Local Reprodutível com Docker Compose (Priority: P1)

**Goal**: Permitir que um desenvolvedor suba o PostgreSQL 18.6, aplique a migration via profile `migrations` e inicie a `App.Api` a partir de um clone limpo usando variáveis de ambiente por instalação, sem credenciais reais no repositório.

**Independent Test**: Configurar `infra/.env.local` (A) e `infra/.env.b.local` (B) a partir do exemplo, validar `docker compose config --quiet` em projetos distintos e aplicar a migration com conexões/owners próprios; ativar A e confirmar `200 OK` em `/health/live`, `/health/ready` e `/openapi/v1.json`.

### Tests for User Story 1 ⚠️

- [X] T028 [P] [US1] Criar testes unitários garantindo que configuração obrigatória ausente ou inválida em `InstallationTenantOptions` bloqueia a prontidão com diagnóstico seguro (sem expor secrets ou connection strings) em `backend/tests/Platform.UnitTests/Configuration/InstallationTenantOptionsValidationTests.cs`

### Implementation for User Story 1

- [X] T029 [P] [US1] Criar `Dockerfile` multi-stage para `App.Api` (runtime ASP.NET Core `10.0.12` não-root, sem SDK ou credenciais de migration) em `infra/docker/Dockerfile.api`
- [X] T030 [P] [US1] Criar `Dockerfile` para o serviço pontual `migrations` (SDK `10.0.401` com `dotnet-ef` `10.0.12` restaurado) em `infra/docker/Dockerfile.migrations`
- [X] T031 [P] [US1] Criar arquivo de exemplo com variáveis fictícias da instalação (`ApplicationId`, `EnvironmentId`, `TenantId`, `TenantDisplayName`, `TenantState`, conexões runtime/migration e `Migrations__AllowInitializeEmptyDatabase=false`), documentando configurações privadas próprias para A/B, em `infra/.env.example`
- [X] T032 [US1] Criar `infra/compose.yaml` com `postgres` (`postgres:18.6`, volume em `/var/lib/postgresql`, sem porta no host), `api` e `migrations` (profile pontual); separar credenciais runtime/owner, montar aprovação de inicialização somente no tooling e permitir projetos A/B com volumes/redes independentes e sem nomes globais em `infra/compose.yaml`
- [X] T033 [US1] Atualizar `.gitignore` para excluir `.env*.local` (incluindo A/B), arquivos privados de secrets, artefatos de build/test e key rings locais em `.gitignore`

**Checkpoint**: Ambiente local completo e reproduzível via Docker Compose para uma instalação de cliente.

---

## Phase 7: User Story 4 & User Story 6 - Guardrails Arquiteturais e CI do Backend (Priority: P2)

**Goal**: Garantir por testes automatizados que os limites das camadas (`Domain`, `Application`, `Infrastructure`, `App.Api`) não sejam violados e configurar o pipeline GitHub Actions para validar build, formatação, testes, vulnerabilidades e secrets antes da integração.

**Independent Test**: Executar a suíte de arquitetura e validar o workflow `.github/workflows/backend-ci.yml` com todos os gates de qualidade de I1.

### Tests for User Story 4 & User Story 6 ⚠️

- [X] T034 [P] [US4] Criar testes automatizados de arquitetura verificando que `Platform.Domain` não referencia EF Core, Identity ou HTTP, que `Platform.Application` não referencia `Platform.Infrastructure` e que não há dependências circulares em `backend/tests/Platform.UnitTests/Architecture/LayerDependencyTests.cs`

### Implementation for User Story 4 & User Story 6

- [X] T036 [US6] Criar workflow do backend com restore em `--locked-mode` usando lockfiles de T006, build, `dotnet format --verify-no-changes`, testes/Testcontainers com falha para zero testes, auditoria NuGet com bloqueio de achados altos/críticos não mitigados e `gitleaks`; scan ausente/falho não aprova, permissões são mínimas e actions fixadas por SHA; configurar/verificar checks obrigatórios de branch protection/ruleset com o mantenedor em `.github/workflows/backend-ci.yml`

**Checkpoint**: Limites arquiteturais e gates automatizados de qualidade e segurança ativos para o I1.

---

## Phase 8: User Story 7 & Polish - Documentação Operacional e Validação Final do I1 (Priority: P3)

**Goal**: Documentar a decisão single-tenant por deploy, registrar o guia de I1 no `README.md`, conferir lockfiles existentes e comprovar os gates com exercícios negativos controlados.

**Independent Test**: Seguir `README.md` e `specs/001-fullstack-foundation/quickstart.md` do zero, executar a bateria válida e demonstrar falha/bloqueio de cada exercício negativo da seção 5, retornando ao estado válido sem desativar checks.

- [X] T037 [P] [US7] Registrar ADR documentando a decisão de implantação dedicada por cliente (1 backend + 1 banco PostgreSQL por instalação, sem `platform_catalog`, com segredos por aplicação no Dokploy/VPS) em `docs/adr/0001-single-tenant-per-deploy.md`
- [X] T038 [P] [US7] Atualizar `README.md` com arquitetura, pré-requisitos de bancos/roles, configurações e projetos Compose próprios para A/B, aprovação de inicialização desligada por padrão e removida após uso, sequência inativar/migrar/ativar, checks obrigatórios e separação de `I1` a `I4` em `README.md`
- [X] T039 Conferir os `packages.lock.json` existentes com restore em `--locked-mode` e executar `specs/001-fullstack-foundation/quickstart.md`, incluindo prova concorrente mínima e exercícios negativos de SC-006 (compilação/tipos, formatação, persistência/isolamento, secret fictício, relatório sintético de vulnerabilidade alta/crítica, scan indisponível e zero testes); registrar check/resultado/bloqueio no workflow/PR e confirmar execução válida após descartar somente as alterações do exercício, sem secrets reais ou dependências vulneráveis

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
