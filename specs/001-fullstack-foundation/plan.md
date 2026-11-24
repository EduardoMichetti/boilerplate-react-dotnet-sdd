# Implementation Plan: L2.1 (sessão por inatividade + teto absoluto) e L3 (interface web de acesso e extensão modular)

**Branch Git**: `main` | **Date**: 2026-11-24 | **Spec**: [spec.md](spec.md) (clarificações de 2026-11-22 e 2026-11-23)
**Feature Context**: `001-fullstack-foundation`, resolvido por `SPECIFY_FEATURE_DIRECTORY`
(não há `.specify/feature.json`). O campo `BRANCH` retornado pelo setup é esse contexto,
não uma troca de branch Git.
**Input**: atualizar o plano para os esclarecimentos de 2026-11-23: primeiro a reabertura
da L2 (sessão por inatividade + teto absoluto, recorte **L2.1**), depois a L3 sobre o novo
modelo de sessão, registrando somente o delta.
**Status**: design; nenhum código, migration, container ou teste é implementado ou
executado neste comando. L3 corresponde a I3 nos artefatos anteriores.
**Revisões**: 2026-11-23 (plano L3); **2026-11-24** (L2.1 prioritária; deltas L3 de
permissões com descrição, validade da sessão na tela, nova entrada no próprio local,
várias abas, nenhum armazenamento no navegador; migration inicial regenerada);
**2026-11-24, clarificações finais** (revalidação por evento renova a inatividade; motivo
da negação de sessão só no log — D37; 409 no diálogo de nova entrada resolvido por uma
revalidação — D38).
**Plano anterior**: o plano L2 (revisão 2026-11-08) permanece no histórico Git
(`c33acbe`). Decisões vigentes em [research.md](research.md) D14–D20, D21 (revisada por
D31), [data-model.md](data-model.md), [operations.md](contracts/operations.md) e ADRs 0002/0003.

## Summary

A L2 está entregue e validada (quickstart §1–§7, Backend CI verde). A spec reabre a L2
e define a ordem:

1. **L2.1 — modelo de sessão (FR-006, SC-002), prioridade bloqueante.** A sessão expira
   no que ocorrer primeiro: **inatividade** (padrão 30 min, faixa 5–240) ou **teto
   absoluto** contado da criação (padrão 600 min, faixa 30–720). Cada requisição
   autenticada aceita renova a inatividade, nunca o teto; negadas não renovam; sem
   remember-me ou refresh. Teto e janela são fixados na emissão. Inclui novas chaves de
   configuração, colunas e CHECKs da sessão, registro de uso limitado a 1 escrita/minuto,
   regeneração da migration inicial única, campos aditivos na API, SC-002 e regressão
   revisados. **Implementada e validada antes de iniciar qualquer bloco da L3.**
2. **L3 — interface web de acesso (FR-004) e extensão modular (FR-017 a FR-019)**, já
   sobre o modelo da L2.1. O plano L3 de 2026-11-23 continua válido com os deltas:
   permissões com descrição pt-BR declarada em código e devolvida por `/context`; tela de
   contexto com teto e regra de inatividade em texto estático; **nova entrada no próprio
   local** (conteúdo escondido, estado da aba preservado em memória, diálogo de
   credenciais; mesma conta restaura, outra conta ou sair descarta); aviso entre abas só no
   logout explícito; nenhum dado gravado no armazenamento do navegador; E2E bloqueantes
   adicionais.

Mantém **1 backend + 1 PostgreSQL por cliente**. Publicação/recuperação (Dokploy/VPS,
HSTS, backups, restauração) continua L4.

Design orientado por [research.md](research.md) (L2.1: D31–D33 e D37; L3: D22–D30, D34–D36 e D38),
[data-model.md §6–§9](data-model.md), [operations.md §4 (L2.1) e §7–§9](contracts/operations.md)
e [ui.md](contracts/ui.md).

## Technical Context

**Language/Version**: backend inalterado — C# 14 / `net10.0`, SDK-base `10.0.401`
(`rollForward=latestFeature`, sem preview), ASP.NET Core/EF Core/Identity `10.0.12`.
Frontend (L3): **TypeScript 6.0.3** estrito sobre **Node.js 24.22.3 LTS** (revisão
[D24](research.md#d24--stack-do-frontend-e-pins-reverificados-2026-11-23) do pin D01).

**Primary Dependencies**:
- **L2.1**: nenhuma dependência nova (Options, EF Core, cookie handler e filtros de
  endpoint são do framework já referenciado).
- **L3**: backend sem pacote NuGet novo. Frontend com pins exatos sem ranges
  (verificados no registro npm em 2026-11-23, D24):

| Uso | Pacotes |
|---|---|
| Runtime | `react`/`react-dom` 19.3.1, `react-router` 8.5.2 |
| Build/estilo | `vite` 8.3.8, `@vitejs/plugin-react` 6.3.0, `tailwindcss`/`@tailwindcss/vite` 4.3.5 |
| Tipos | `typescript` 6.0.3, `@types/react`/`@types/react-dom` 19.3.0 |
| Qualidade | `eslint` 10.15.0, `typescript-eslint` 8.74.0, `eslint-plugin-react-hooks` 7.1.1, `prettier` 3.10.1 |
| Testes | `vitest` 5.1.1, `jsdom` 31.0.1, `@testing-library/react` 16.3.3, `@testing-library/dom` 10.4.2, `@testing-library/user-event` 14.6.13, `@playwright/test` 1.65.1, `@axe-core/playwright` 4.14.0 |

**Estado e consultas no cliente (decisão revisada, [D35](research.md#d35--nova-entrada-no-próprio-local-e-estado-preservado-revisa-d25))**:
**nenhuma biblioteca**. A preservação do estado na nova entrada usa a árvore React
mantida montada sob `hidden` + `inert`, `<dialog>` nativo com `showModal()` (foco preso,
fundo inerte) e uma fila de leituras pendentes no `ClienteApi` sobre `fetch`. TanStack
Query foi reavaliada e rejeitada: não preserva estado local de componentes, exigiria
desligar retry/refetch e acrescenta ~13 KB gzip sem necessidade presente. Sem cliente
HTTP, UI kit, CSS-in-JS, biblioteca de modal/foco, i18n, service worker, telemetria ou
gerador de tipos OpenAPI. Dependências novas exigem aprovação (Constituição X) — gate Q0.

**Storage**: PostgreSQL `18.6`, banco exclusivo.
- **L2.1**: `access.sessoes_autenticacao` troca `ExpiraEm` por `TetoAbsolutoEm` e ganha
  `JanelaInatividadeMinutos` e `UltimoUsoEm`, com CHECKs de teto (30–720), janela (5–240,
  ≤ teto) e último uso (`[CriadoEm, TetoAbsolutoEm)`) e índices revisados
  ([data-model §7](data-model.md#sessaoautenticacao--accesssessoes_autenticacao)).
  **Migration inicial única regenerada**, sem migration incremental ([D32](research.md#d32--migration-inicial-única-regenerada-em-vez-de-migration-incremental)).
- **L3**: nenhuma tabela/coluna nova; CHECK de `CodigoPermissao` com hífen entra numa
  nova regeneração da migration inicial (revisa D28). Módulos sem dados.
- Interface: **nada** no armazenamento do navegador, nem marcador de recarga (D36).

**Testing**: backend mantém xUnit v3 4.0.2/MTP/Testcontainers 4.16.0 e os três projetos,
com `TimeProvider` controlável. L2.1 acrescenta a matriz de sessão (fronteiras de
inatividade/teto, negadas não renovam, limitação de escrita, faixas de configuração,
CHECKs, concorrência registro de uso × revogação, limpeza pela expiração efetiva,
revalidação por `/me` renovando, motivo da negação no log capturado por `CapturaLogs`). L3:
ESLint (tipos), `tsc --noEmit`, Prettier, Vitest + Testing Library, Playwright (Chromium)
+ axe contra a stack local com a API servindo a interface. Todos bloqueantes.

**Target Platform**: API ASP.NET Core em Linux (Compose v2; PowerShell no Windows para
o roteiro). Navegadores (L3): duas últimas versões estáveis de Chrome, Edge e Firefox
desktop (todas com `<dialog>` e `inert` nativos); E2E bloqueante só em Chromium.

**Project Type**: monólito modular web. L2.1 não cria projeto. L3: dois projetos novos
de backend (`Platform.Modulos`, `App.Composicao`), um projeto `frontend/` e o módulo
sintético de SC-004 sob `backend/tests/`.

**Performance Goals**: L2.1 — no máximo 1 escrita de uso por minuto por sessão; nenhuma
escrita em requisições negadas; sem `Set-Cookie` em requisições normais. L3 — JavaScript
inicial ≤ **200 KiB gzip** (NFR-013, bloqueante), SC-001 ≤ 45 min.

**Constraints**: Português Técnico também no TypeScript; pastas físicas em inglês. CSP
sem `'unsafe-inline'`, nonce ou hash para scripts e estilos. Sem `localStorage`/
`sessionStorage`/IndexedDB. Sem temporizador de expiração no cliente, requisição periódica
ou login silencioso. Regras L2 (cookies `__Host-`, CSRF, ordem G2, ADR 0003, nomes,
`TimeProvider`, UUID v7, sem `ExecuteUpdate`/`ExecuteDelete` em entidade auditável)
inalteradas.

**Scale/Scope**: L2.1 — 2 chaves de configuração (1 obsoleta rejeitada), 3 colunas/3
CHECKs, 1 filtro de endpoint + 1 caso de uso, 2 campos aditivos de contrato
(`idleTimeoutMinutes`, `sessionIdleTimeoutMinutes`), `openapi.json` 2.1.0. L3 — 1 operação
nova (`GET /api/v1/tenant`), 2 campos aditivos em `/context` (`modules`,
`permissionDetails`), SPA com sete estados + diálogo de nova entrada, grupo de rotas de
módulos, módulo sintético de teste.

## Constitution Check

*GATE: conferir antes da pesquisa e depois do design.*

Constituição [2.3.0](../../.specify/memory/constitution.md). PASS refere-se ao
**desenho**, não a execução comprovada.

| Princípio | L2.1 (pré / pós) | L3 (pré / pós) | Evidência / gate de implementação |
|---|---|---|---|
| I. Arquitetura modular | PASS / PASS | PASS / PASS | L2.1: regra de validade em Application (`ValidacaoSessaoAtual`, `CasoUsoRegistrarUsoSessao`), filtro HTTP em App.Api, persistência em Infrastructure. L3: módulos como projetos próprios compostos em `App.Composicao`; frontend sem regra de negócio. |
| II. Contratos explícitos | PASS / PASS | PASS / PASS | Campos de sessão e `permissionDetails` documentados; descrições declaradas junto do código da permissão; dependências de módulos por contrato com versão maior. |
| III. Stack | PASS / PASS | PASS cond. Q0 / PASS cond. Q0 | L2.1 sem dependência. L3: pins exatos, TS estrito, desvio TS 7.0.2 → 6.0.3 (D24); nenhuma lib de estado/modal acrescentada (D35). |
| IV. Isolamento | PASS / PASS | PASS / PASS | Sessão, uso e limpeza no banco da própria instalação; cookie host-only. Interface sem armazenamento no navegador; mesmo bundle para qualquer instalação. A×B segue provado por testes de backend. |
| V. API | PASS / PASS | PASS / PASS | Mudanças aditivas (FR-021): `expiresAt`/`sessionExpiresAt` mantêm o nome e passam a significar explicitamente o teto; `openapi.json` 2.1.0 promovido **com** a implementação (teste de contrato compara a versão servida). `no-store` em toda a API. |
| VI. Segurança | PASS / PASS | PASS / PASS | Expiração verificada no servidor a cada requisição; negadas não renovam; uso nunca estende o teto; chave obsoleta impede a inicialização (sem mudança silenciosa de política); registro de uso nunca reativa sessão revogada; 401 genérico sem motivo ao cliente, motivo só no log sem id de sessão/conta, cookie ou IP (D37). L3: conteúdo protegido escondido e inerte atrás do diálogo; sem login silencioso, timer ou keep-alive; gravação nunca reenviada sem ação do usuário. |
| VII. Qualidade | PASS / PASS | PASS / PASS | Matriz L2.1 com fronteiras 4/5, 240/241, 29/30, 720/721, não inteiro, janela > teto; PostgreSQL real; concorrência determinística. Migration: validada em banco novo; "atualização de versão anterior suportada" não se aplica porque nenhuma versão foi publicada (clarificação 2026-11-06; FR-015 esclarecido em 2026-11-24). L3: E2E adicionais (expiração com conteúdo escondido, troca de conta, gravação não reenviada, várias abas, axe do diálogo). |
| VIII. Infraestrutura | PASS / PASS | PASS / PASS | L2.1: Compose e `.env.example` com as duas chaves; bancos locais recriados pelo roteiro existente. L3: estágio Node fixado na imagem da API; roteiro SC-001 automatizado. |
| IX. Manutenibilidade | PASS / PASS | PASS / PASS | Nomes em Português Técnico (`TetoAbsolutoEm`, `UltimoUsoEm`, `JanelaInatividadeMinutos`, `FiltroRegistroUsoSessao`); solução mais simples (sem sliding do handler, sem lib de estado); ADR 0002 revisada; ADRs 0004/0005 na L3. |
| X. Evolução | PASS / PASS | PASS cond. Q0 / PASS cond. Q0 | L2.1 explicita a migração de comportamento (chave antiga rejeitada, quickstart §7.1, campos aditivos). L3: cada dependência com necessidade/alternativa (D24–D26, D35). |

### Gates R0 — L2.1 (bloqueiam a implementação da L2.1)

1. **ADR 0002 revisada** pelo mantenedor: inatividade + teto, registro de uso limitado,
   chave obsoleta rejeitada, limpeza pela expiração efetiva.
2. ~~Precisão da limitação de escrita~~ — **resolvido** na clarificação de 2026-11-24
   (FR-006/SC-002 aceitam negação até 60 s antes, nunca depois).
3. ~~Migration regenerada~~ — **resolvido** na clarificação de 2026-11-24 (FR-015:
   "estado anterior suportado" = versão publicada; regenerar até a L4).
4. ~~Semântica de "requisição aceita"~~ — **resolvido** na clarificação de 2026-11-24
   (FR-006), como em D33. A chave antiga com valor impedindo a inicialização também foi
   confirmada (FR-006).

### Gates Q0 — L3 (bloqueiam a implementação da L3; exigem L2.1 aceita)

1. **Aprovação das dependências** do frontend e da troca do pin TypeScript (D24).
2. **ADR 0004** — interface servida pela API (mesma origem, CSP/cache/fallback, `buildId`,
   comportamento sem build por ambiente) e nova entrada no próprio local (D35/D36).
3. **ADR 0005** — composição de módulos em compilação, regras de id/permissão/descrição/
   contrato/dependência, módulos sem dados, OpenAPI só da fundação, base `aspnet` da
   imagem `operations`.
4. **Interpretação de SC-004 "solução distribuída"** (D29), como no plano anterior.
5. ~~Pedido de sair negado com 401~~ — **resolvido** na clarificação de 2026-11-24: conclui
   a saída localmente (descarta, `/entrar` com aviso, avisa as outras abas), sem diálogo.

Não há violação constitucional aceita no design.

## Project Structure

### Documentation (this feature)

```text
specs/001-fullstack-foundation/
├── spec.md                 # requisitos (L2.1 prioritária; recorte L3)
├── plan.md                 # este plano (L2.1 + L3)
├── research.md             # D01–D30 preservadas; L2.1 D31–D33; delta L3 D34–D36
├── data-model.md           # §6 chaves, §7 sessão L2.1, §8 migration regenerada, §9 L3
├── quickstart.md           # L1/L2 + §7.1 roteiro L2.1 + roteiro L3 (§8–§12)
├── architecture.md         # arquitetura evolutiva + L2.1 + L3
├── tasks.md                # L2.1 (T135–T169) e L3 (T170–T219) + históricos L1/L2
└── contracts/
    ├── openapi.json        # contrato L2 2.0.0 servido; 2.1.0 promovido com a L2.1 (delta em operations §4)
    ├── operations.md       # L2 + §4 “Expiração e uso da sessão (L2.1)” + §7–§9 L3
    └── ui.md               # contrato observável da interface (com diálogo de nova entrada)
```

### Source Code (repository root)

```text
backend/src/
├── Platform.Domain/
│   └── Access/                      # L3: DefinicaoPermissao, CodigosPermissao.Definicoes e formato com hífen
├── Platform.Application/
│   ├── Configuration/               # L2.1: OpcoesSessao (InatividadeMinutos, TetoAbsolutoMinutos) + validador
│   ├── Access/                      # L2.1: ValidacaoSessaoAtual (expiração efetiva), CasoUsoRegistrarUsoSessao,
│   │                                #   CasoUsoLogin (grava janela/teto/último uso), repetição de revogação;
│   │                                #   L3: ICatalogoPermissoes com descrições
│   └── Tenancy/                     # L3: ConsultarIdentidadePublicaTenant
├── Platform.Infrastructure/         # L2.1: entidade SessaoAutenticacao (TetoAbsolutoEm, JanelaInatividadeMinutos,
│                                    #   UltimoUsoEm), configuração EF, CHECKs/índices, RepositorioSessoes (registro
│                                    #   de uso, repetição de revogação), migration inicial regenerada, limpeza pela
│                                    #   expiração efetiva; L3: CHECK com hífen (nova regeneração)
├── Platform.Modulos/                # L3 NOVO: IModulo, DeclaracaoModulo (Permissoes = DefinicaoPermissao),
│                                    #   ValidadorComposicaoModulos, CatalogoPermissoesEfetivo, foundation.autorizacao v1,
│                                    #   Authorization/FiltroExigirPermissao (vindo de App.Api, G31)
├── App.Composicao/                  # L3 NOVO: lista única de módulos (vazia na base)
├── App.Api/
│   ├── Access/                      # L2.1: FiltroInstalacaoAtiva, FiltroExigirPermissao (G31; movido a
│   │                                #   Platform.Modulos na L3), FiltroRegistroUsoSessao, ContextoNegacaoSessao, emissão (cookie/ticket no teto), respostas
│   │                                #   com idleTimeoutMinutes/sessionIdleTimeoutMinutes; L3: modules, permissionDetails
│   ├── Interface/                   # L3 NOVO: arquivos estáticos, fallback, cabeçalhos, buildId
│   ├── Modules/                     # L3 NOVO: grupo /api/v1/modules/{id} (com registro de uso)
│   └── Tenancy/                     # L3 NOVO: GET /api/v1/tenant
└── Platform.Operations/             # L2.1: sessao limpar pela expiração efetiva; L3: catálogo efetivo
backend/tests/
├── Platform.UnitTests/              # L2.1: opções/faixas, expiração efetiva, limitação; L3: validador, descrições
├── Platform.IntegrationTests/       # L2.1: matriz de sessão com PostgreSQL real, CHECKs, concorrência, limpeza; L3: interface, SC-004
├── Platform.ContractTests/          # L2.1: openapi 2.1.0; L3: OpenAPI idêntico, headers, 404 JSON, arquitetura
└── Modules/                         # L3: Teste.ModuloSintetico(.Contratos)
frontend/                            # L3 NOVO
├── package.json / package-lock.json / .nvmrc / orcamento.json
├── src/
│   ├── app/          # roteador, layout
│   ├── api/          # ClienteApi (fetch, fila de leituras pendentes, ErroGravacaoNaoEnviada)
│   ├── session/      # ProvedorSessao, máquina de estados (inclui reautenticando), BroadcastChannel (logout), revalidação
│   ├── update/       # buildId, recarga única pelo tipo de navegação
│   ├── screens/      # TelaEntrada, TelaContexto (validade + permissões com descrição), …
│   ├── components/   # DialogoNovaEntrada (<dialog>), AreaProtegida (hidden+inert), ValidadeSessao, …
│   └── styles/
├── scripts/          # verificar-orcamento.mjs, plugin-id-build.ts (plugin Vite do buildId)
└── e2e/              # Playwright + axe
infra/
├── .env.example / compose.yaml      # L2.1: Sessao__InatividadeMinutos, Sessao__TetoAbsolutoMinutos (remove PrazoAbsoluto)
├── docker/Dockerfile.api            # L3: estágio Node 24.22.3
├── docker/Dockerfile.operations     # L3: base aspnet 10.0.12 (D27)
└── scripts/                         # L3: roteiro de acesso local (SC-001)
.github/workflows/                   # L3: frontend-ci.yml; backend-ci.yml + SC-004
docs/adr/                            # L2.1: 0002 revisada; L3: 0004 e 0005
```

**Structure Decision**: a L2.1 não cria projetos: a regra de validade e o registro de uso
ficam em Application (testáveis sem HTTP), o filtro que decide "requisição aceita" fica
em App.Api (único ponto que conhece o resultado da autorização) e a persistência em
Infrastructure. A estrutura L3 permanece a do plano de 2026-11-23: `Platform.Modulos`
concentra abstração e validação; `App.Composicao` é o único ponto de inclusão de módulos,
referenciado por `App.Api` e `Platform.Operations`.

## Phase 0 — pesquisa e decisões

| Decisão | Recorte | Tema |
|---|---|---|
| D31 | L2.1 | Sessão por inatividade + teto: chaves, campos persistidos, CHECKs, cookie/ticket, API, limpeza (revisa D21 e o trecho de D15) |
| D32 | L2.1 + L3 | Migration inicial única regenerada em vez de incremental (revisa D28 quanto à migration) |
| D33 | L2.1 | Registro de uso: o que renova, limitação de 1 escrita/min, concorrência e tolerância nos testes |
| D22–D30 | L3 | Mantidas (interface servida pela API, `buildId`, stack, CSP, composição, OpenAPI, CI) |
| D34 | L3 | Descrição pt-BR das permissões em código; `permissionDetails` em `/context` |
| D35 | L3 | Nova entrada no próprio local; estado preservado sem biblioteca (revisa D25) |
| D36 | L3 | Aviso entre abas só no logout; recarga única sem armazenamento (revisa D25) |
| D37 | L2.1 | Motivo da negação de sessão só no log (`EventId 1002 SessaoNegada`); 401 inalterado |
| D38 | L3 | 409 `SESSION_CONFLICT` no diálogo de nova entrada → uma única revalidação de `/me` |

Não há `NEEDS CLARIFICATION` pendente. Conflitos resolvidos no design:
(a) o plano L3 previa migration aditiva e "não reescrever a migration inicial", o que
contraria a clarificação de 2026-11-06 → regeneração (D32);
(b) o marcador `sessionStorage['bps-recarga']` (D25) contrariava a orientação de não gravar
nada no navegador → tipo de navegação (D36);
(c) D25 descartava o estado em 401 e avisava as outras abas → nova entrada e aviso só no
logout (D35/D36);
(d) gravar uso em toda requisição conflitaria com `Versao` e com revogações → limitação e
repetição de revogação (D33).

## Phase 1 — design e contratos

### L2.1 — modelo de sessão (executar e aceitar antes da L3)

1. **Configuração** ([data-model §6](data-model.md#6-configuração-sem-entidade-de-catálogo)):
   `Sessao:InatividadeMinutos` (30; 5–240) e `Sessao:TetoAbsolutoMinutos` (600; 30–720),
   parse estrito, ausente/vazio = padrão, `ValidateOnStart`; inatividade > teto, fora da
   faixa ou não inteiro → a API não inicia com diagnóstico seguro; `Sessao:PrazoAbsolutoMinutos`
   com valor → a API não inicia. `OpcoesSessao` substitui a propriedade de prazo único.
2. **Emissão** (login, inclusive troca atômica): `TetoAbsolutoEm = CriadoEm + teto`,
   `JanelaInatividadeMinutos = inatividade`, `UltimoUsoEm = CriadoEm`, todos do mesmo
   `InstanteOperacao`. Cookie `Expires` e ticket `ExpiresUtc` = `TetoAbsolutoEm`;
   `SlidingExpiration = false` mantido. Resposta 201 com `expiresAt` (teto) e
   `idleTimeoutMinutes`.
3. **Validação** (`ValidacaoSessaoAtual`): `agora < min(UltimoUsoEm + janela, TetoAbsolutoEm)`
   + condições L2. Expirada → 401 `AUTH_REQUIRED` sem revogar. `/me` devolve
   `sessionExpiresAt` (teto) e `sessionIdleTimeoutMinutes`.
4. **Registro de uso** ([D33](research.md#d33--registro-de-uso-da-sessão-limitação-de-gravação-e-concorrência)):
   `FiltroRegistroUsoSessao` no grupo de rotas protegidas (`/me`, `/context`; na L3,
   módulos), na ordem (G31) autorização → `FiltroInstalacaoAtiva` → `FiltroExigirPermissao`
   (quando exigida) → `FiltroRegistroUsoSessao` → handler, de modo que 403/409 nunca
   renovam; os casos de uso mantêm as mesmas verificações; 400 `INPUT_INVALID`/
   `CSRF_INVALID` antes do handler não renovam, 400/422 do handler renovam; grava se
   `agora − UltimoUsoEm ≥ 60 s`;
   conflito descartado; falha de banco → 503. Login, logout, antiforgery, health, OpenAPI,
   `/tenant` e arquivos estáticos nunca renovam. Revalidações da interface (`/me` por
   carga, foco ou navegação) são requisições aceitas comuns e renovam (clarificação
   2026-11-24); o servidor não as distingue.

   **Motivo da negação** ([D37](research.md#d37--motivo-da-negação-de-sessão-só-no-log-2026-11-24)):
   `ValidacaoSessaoAtual` devolve `MotivoNegacaoSessao` (`Inatividade`, `TetoAbsoluto`,
   `Revogada`, `Invalida`, `Ausente`), guardado em `IContextoNegacaoSessao` (scoped) pelo
   Retrieve; o ponto que escreve o 401 de sessão registra `EventId 1002 SessaoNegada`
   (`Information`) com motivo, `TraceId` e `IdOperacao`, sem id de sessão/conta, cookie
   ou IP. Resposta e `openapi.json` inalterados.
5. **Revogações**: logout, troca no login, revogação de todas (reset, desativação,
   bloqueio, operacional) repetem até 3 vezes quando o conflito de `Versao` vem só de
   registro de uso; disputa real mantém a regra L2 (409 no login); esgotado → 503.
6. **Persistência e migration**: colunas, CHECKs (`ck_sessoes_autenticacao_teto`,
   `…_janela_inatividade`, `…_ultimo_uso`) e índices (`…_ultimo_uso_em`,
   `…_teto_absoluto_em`) da [§7](data-model.md#sessaoautenticacao--accesssessoes_autenticacao);
   **regenerar** `EsquemaInicialAcessoTenant` (D32); GRANTs inalterados (runtime sem DELETE).
7. **Limpeza**: `sessao limpar` elegível por `RevogadaEm < corte` ou expiração efetiva
   `< corte`, com pré-filtro `UltimoUsoEm < corte`; demais regras de D21.
8. **Contratos**: [operations §4](contracts/operations.md#expiração-e-uso-da-sessão-l21);
   `openapi.json` 2.0.0 → 2.1.0 conforme o delta descrito ali, **no mesmo conjunto de
   mudanças** dos endpoints (o teste de contrato compara `info.version` servida).
9. **Infra e docs**: `.env.example`/Compose com as duas chaves (sem a antiga), README e
   quickstart §7.1, ADR 0002 revisada.
10. **Mantidos** (regressão): troca atômica, 409 `SESSION_CONFLICT`, bloqueio invalidando
    sessões, sessões por dispositivo, logout só da corrente, revogação operacional de
    todas, instalação inativa (não pausa), retenção da limpeza.

### Superfície da interface no backend (L3, inalterada)

Como no plano de 2026-11-23 e [operations §8](contracts/operations.md#8-superfície-da-interface-servida-pela-api):
arquivos do build em `wwwroot/` (Production sem build não inicia; Development/Testes
iniciam e respondem 404 JSON), `/api/*`/`/openapi/*`/`/health*` nunca devolvem a
interface, fallback `{*path:nonfile}`, arquivo com extensão inexistente → 404, CSP estrita
+ `nosniff` + `Referrer-Policy: no-referrer` em todas as respostas, `/assets/*` imutável,
`index.html` e toda a API `no-store`, static files e fallback sem lookup de sessão.

### Operação pública `GET /api/v1/tenant` (L3, inalterada)

Anônima, sem `X-Tenant-Id`, cookies ignorados; `200 { id, displayName, buildId }` com
instalação ativa ou inativa quando configuração e marcador conferem; senão 503 genérico;
`no-store`; não renova sessão. Detalhes em [operations §7](contracts/operations.md#7-contrato-http-l3).

### Interface (SPA) — L3 com deltas

Contrato observável em [ui.md](contracts/ui.md). Pontos de projeto:

- **Rotas**: `/entrar`, `/contexto` (protegida), `/` → `/contexto`, outras → "Página não
  encontrada". Após login pela tela de entrada, sempre `/contexto`; nenhum destino vem da URL.
- **Carga**: `GET /api/v1/tenant` → `GET /api/v1/me` → `GET /api/v1/context`. Falha na
  identidade do tenant → indisponibilidade sem formulário.
- **Tela de contexto (delta)**: permissões a partir de `permissionDetails` (descrição +
  código, sem tabela local); validade em texto estático no fuso local:
  "Sessão válida até 18:35, ou após 30 min sem uso" (`sessionExpiresAt` +
  `sessionIdleTimeoutMinutes`; data incluída se o teto não for hoje). Sem contagem
  regressiva, aviso prévio ou renovar.
- **Nova entrada no próprio local (delta, [D35](research.md#d35--nova-entrada-no-próprio-local-e-estado-preservado-revisa-d25))**:
  401 em chamada de aba autenticada (exceto o pedido de sair) → estado `reautenticando`: `AreaProtegida` aplica
  `hidden` + `inert` (árvore montada, estado preservado em memória) e `DialogoNovaEntrada`
  (`<dialog>` modal, Esc bloqueado, campos vazios, Entrar/Sair) é aberto. Leituras negadas
  aguardam na fila do `ClienteApi`; gravações negadas falham com `ErroGravacaoNaoEnviada`.
  Mesma conta (`/me.id`) → restaura, devolve o foco, repete leituras e revalida `/context`;
  outra conta → descarta tudo e mostra o contexto da nova conta; Sair → descarta e vai a
  `/entrar`. Empresa inativa continua "empresa indisponível", só sair.
  409 `SESSION_CONFLICT` no diálogo ([D38](research.md#d38--conflito-de-sessão-no-diálogo-de-nova-entrada-2026-11-24))
  → uma única revalidação de `/me` (a mesma do foco): mesma conta restaura sem diálogo,
  outra conta descarta, 401 mantém o diálogo com a mensagem de conflito; o login nunca é
  reenviado.
- **Várias abas (delta, [D36](research.md#d36--várias-abas-e-recarga-única-sem-armazenamento-revisa-d25))**:
  `BroadcastChannel('bps-sessao')` só com `{ tipo: 'logout' }` após logout explícito (204 ou 401 ao sair). Expiração/
  revogação não descartam outras abas; cada aba aplica a nova entrada na próxima negação ou
  na revalidação de foco; revalidação com sessão válida da mesma conta restaura sem diálogo;
  outra conta descarta.
- **Login/logout**: novo token antiforgery antes de cada envio; envio único; sem
  retentativa automática (exceto leituras pendentes após restauração da mesma conta e a
  única revalidação de `/me` após 409 no diálogo, D38).
  Logout com falha de rede/5xx permanece autenticado com "Tentar sair novamente"; logout
  401 → saída concluída localmente com aviso de sessão encerrada e aviso às outras abas
  (clarificação 2026-11-24), sem diálogo.
- **Atualização (delta)**: `buildId` diferente → aviso de nova versão; `vite:preloadError`
  → uma recarga só se a navegação atual não for recarga (sem marcador gravado); suspensa
  durante a nova entrada.
- **Proibições**: nada no armazenamento do navegador; sem timer de expiração, keep-alive ou
  login silencioso. A revalidação por foco que obtém 200 conta como uso (D33) — é
  disparada por ação do usuário, não por temporizador.

### Extensão modular (L3) — delta de permissões

Regras do plano de 2026-11-23 e [operations §9](contracts/operations.md#9-composição-de-módulos)
mantidas, com: `DeclaracaoModulo.Permissoes` = lista de `DefinicaoPermissao(Codigo,
Descricao)`; descrição pt-BR obrigatória (1–200, sem espaços nas pontas nem controle) para
fundação e módulos, validada por `ValidadorComposicaoModulos` (`PERMISSAO_DESCRICAO_INVALIDA`)
na inicialização dos dois hosts e por teste de arquitetura; `/context.permissionDetails`
com as permissões efetivas (inertes omitidas); rotas de módulos registram uso da sessão
após a autorização. CHECK de `CodigoPermissao` com hífen entra na migration inicial
regenerada (D32), validada em banco novo.

### Aceite e ordem de execução

**L2.1 — rastreabilidade**

| Requisito | Evidência exigida |
|---|---|
| FR-006, SC-002 (sessão) | Matriz da [quickstart §7.1](quickstart.md#71-l21--sessão-por-inatividade-e-teto-absoluto-alvo-ainda-não-implementado): inatividade 30 nega (29:59 aceita); uso a cada 29 min e a cada 1 min até 600 (599:59 aceita, 600:00 nega); negadas não renovam (403, 409, CSRF); inativação não pausa; limites 4/5, 240/241, 29/30, 720/721; não inteiro; inatividade > teto; chave obsoleta; fixado na emissão; limitação de escrita; CHECKs; concorrência registro de uso × revogação/login; revalidação por `/me` renovando. |
| NFR-006 (motivo da negação) | Cinco motivos com relógio controlado: respostas 401 idênticas exceto `traceId`; `SessaoNegada` com motivo e `TraceId` corretos em `CapturaLogs`; nenhum cookie, id de sessão/conta ou IP no log (D37). |
| FR-008 e controles L2 | Regressão integral da §6 (troca atômica, conflito, bloqueio, sessões por dispositivo, A×B). |
| FR-033 (`sessao limpar`) | Corte pela expiração efetiva com fronteira exata; um registro por execução. |
| SC-005, SC-009 | Consumidor independente com campos aditivos; migration inicial regenerada validada em banco novo, reaplicação e rejeição de histórico desconhecido. |
| FR-027, SC-006 | Backend CI verde; `openapi.json` 2.1.0 igual ao servido; Gitleaks e vulnerabilidades. |

**L3 — rastreabilidade (deltas em negrito)**

| Requisito | Evidência exigida |
|---|---|
| US com FR-004; NFR-011/012/013; SC-001 | E2E Chromium do plano anterior (entrada, contexto, sem permissão, saída e falha de saída, empresa indisponível, 429, indisponibilidade com referência, nova versão e recarga única, deep link, página não encontrada) **mais: validade da sessão em texto estático; permissões com descrição e código; expiração com conteúdo escondido e restauração pela mesma conta; troca de conta descartando o estado; gravação não reenviada (teste de componente bloqueante com gravação sintética + E2E provando que só leituras são repetidas após a restauração); sair com sessão já revogada (saída local + aviso às abas); várias abas (logout × expiração/revogação, restauração por foco); novas entradas simultâneas com 409 resolvido por uma revalidação de `/me` (um único `POST /sessions` por aba, D38); axe do diálogo (zero sério/crítico, foco preso e devolvido, Esc não fecha); armazenamento do navegador vazio**. Componentes: estados, envio duplo bloqueado, sem retentativa, **fila de leituras e rejeição de gravações**. Backend: cabeçalhos, cache, fallback, 404 JSON, `/tenant`, ausência de build por ambiente. Orçamento ≤200 KiB. SC-001 manual + CI ≤45 min. |
| FR-017 a FR-019, FR-021; SC-004 | Validador com todos os casos negativos **incluindo `PERMISSAO_DESCRICAO_INVALIDA` para fundação e módulo**; inicialização falha nos dois hosts; arquitetura de referências; variantes sem/com módulo (200/403/404, `modules`, **`permissionDetails`**, concessão inerte, OpenAPI idêntico); zero referências ao módulo de teste em `src/**`, publish e imagens. |
| FR-027; SC-006/SC-008 | `frontend-ci.yml` bloqueante, Gitleaks em `frontend/` e no build; ADRs 0004/0005; README/quickstart. |
| Regressão: SC-002 (com L2.1)/003/005/009 | Suites L2 e L2.1 verdes; **migration regenerada em banco novo**; consumidor independente válido com campos aditivos. |

**Blocos**

| Bloco | Depende de | Resultado verificável |
|---|---|---|
| **R0 — decisões L2.1** | L2 | ADR 0002 revisada (demais itens resolvidos na clarificação de 2026-11-24). |
| **R1 — opções e persistência** | R0 | `OpcoesSessao` + validador (faixas, cruzada, chave obsoleta; `.env.local` com a chave antiga vazia continua aceito); entidade com teto/janela/último uso e expiração efetiva; configuração EF, CHECKs, índices; migration inicial regenerada; testes de unidade e de CHECK com PostgreSQL real. |
| **R2 — emissão, validação, uso e revogações** | R1 | Login grava os três campos; cookie/ticket no teto; `ValidacaoSessaoAtual` com `MotivoNegacaoSessao` e log `SessaoNegada` (D37); `FiltroRegistroUsoSessao` + `CasoUsoRegistrarUsoSessao`; repetição de revogações; `/me` e login com campos novos; `openapi.json` 2.1.0 no mesmo conjunto; matriz §7.1 com relógio controlado. |
| **R3 — limpeza, infra, docs e aceite L2.1** | R2 | `sessao limpar` pela expiração efetiva; `.env.example`/Compose; README/quickstart §7.1; regressão completa §6; Backend CI verde; registro de evidências em tasks.md. **Fim da L2.1 — só então Q0.** |
| Q0 — decisões L3 | R3 | Dependências e pin TS aprovados; ADRs 0004/0005; SC-004 confirmado; projetos `Platform.Modulos`/`App.Composicao` e esqueleto `frontend/` com `buildId` criados (antecipados para que a imagem da Q1 já tenha `buildId`). |
| Q1 — superfície da interface (backend) | Q0 | Como no plano anterior; `openapi.json` atualizado com `/tenant`. |
| Q2 — modularidade (backend) | Q0 | Como no plano anterior **+ `DefinicaoPermissao`/descrições, `PERMISSAO_DESCRICAO_INVALIDA`, `permissionDetails`, registro de uso no grupo de módulos, CHECK com hífen por migration regenerada**. |
| Q3 — SC-004 | Q2 | Módulo sintético (com descrição de permissão), variantes, verificação de referências/imagem. |
| Q4 — frontend | Q0 (Q1 para integração real) | Projeto, pins, lint/tipos/formato, `ClienteApi` com fila, provedor com `reautenticando`, `AreaProtegida`, `DialogoNovaEntrada`, validade da sessão, permissões com descrição, multi-aba por logout, recarga sem armazenamento, testes de componente, orçamento. |
| Q5 — E2E, CI e aceite L3 | Q1, Q4 (Q3 para o job completo) | Playwright + axe com os cenários novos de [ui.md §9](contracts/ui.md#9-cenários-e2e-bloqueantes-acrescentados-l3), `frontend-ci.yml`, roteiro SC-001, quickstart §8–§12, regressão L2/L2.1. |

R1 → R2 → R3 são sequenciais (mesma entidade e migration). Q1 e Q2 independentes; Q4 pode
começar com stubs após Q0. `openapi.json` é promovido no mesmo conjunto de mudanças de
cada rota/campo (R2: 2.1.0; Q1: `/tenant`; Q2: `modules`/`permissionDetails`), nunca antes.

### Riscos

| Risco | Mitigação |
|---|---|
| Imprecisão de até 60 s na inatividade pela limitação de escrita | Sempre a favor da segurança; documentada (D33), testes de fronteira com intervalos ≥ 60 s; aceita na spec (FR-006/SC-002). |
| Registro de uso disputando `Versao` com revogação ou troca de sessão | Repetição limitada só quando a sessão continua ativa; disputa real preservada; testes com barreira. |
| Produto derivado com a chave antiga configurada | API não inicia, diagnóstico indica as chaves novas (sem mudança silenciosa de política). |
| Bancos locais/CI antigos após regenerar a migration | Recusados por histórico desconhecido (comportamento L2); roteiro §3 recria do zero; CI usa Testcontainers. A partir da L4, só migrations incrementais. |
| Revalidação por foco estendendo a inatividade | Confirmado na spec (FR-006, clarificação 2026-11-24): é ação do usuário, não periódica; sem polling nem timer; teto absoluto inalterado. |
| Log de negação vazar dado de sessão ou virar ruído | Campos fechados (motivo, `TraceId`, `IdOperacao`), nível `Information`, teste negativo de conteúdo (D37). |
| Duas abas substituindo a sessão uma da outra na nova entrada | 409 resolvido por uma revalidação de `/me`, sem reenviar login (D38); E2E com duas abas. |
| Conteúdo protegido no DOM durante o diálogo | `hidden` + `inert` (invisível, não focável, fora da árvore de acessibilidade); E2E verifica ausência de texto visível/acessível; dado continua só em memória da aba, como antes. |
| Garantia "gravação não reenviada" sem gravação real na interface L3 | Teste de componente bloqueante com gravação sintética; E2E prova que só leituras são repetidas; revalidar com o primeiro módulo de negócio que tiver gravação. |
| `<dialog>`/`inert` em navegador fora do suporte | Fora da matriz suportada (Chrome/Edge/Firefox atuais têm suporte nativo). |

## Complexity Tracking

| Item | Por que é necessário | Alternativa mais simples rejeitada porque |
|---|---|---|
| Registro de uso com limitação de 1 escrita/min e repetição de revogações (L2.1) | Renovar a inatividade sem gravar a cada requisição nem quebrar revogações (FR-006) | Gravar sempre: dobra escritas e gera conflitos de `Versao` entre requisições paralelas e com revogações; sliding do handler: não respeita teto nem "negada não renova" |
| Regenerar a migration inicial (L2.1 e L3) | Clarificação de 2026-11-06: esquema por uma migration inicial única, sem dados publicados | Migration incremental: cria caminho de upgrade e backfill para estado inexistente |
| Dois projetos novos (`Platform.Modulos`, `App.Composicao`) (L3) | Composição única compartilhada pelos dois hosts (FR-017/019) | Lista em cada host: divergência silenciosa entre API e operador |
| Imagem `operations` em base `aspnet` (L3) | `Platform.Modulos` referencia o framework ASP.NET Core | Separar declaração e HTTP com duas listas: mais projetos e dois pontos de edição |
| Árvore protegida mantida montada sob `hidden` + `inert` (L3) | Preservar todo o estado da aba sem serializar nem gravar no navegador (FR-004) | Desmontar e guardar store global: perde estado local; biblioteca de estado: não resolve estado local e adiciona dependência |

O item "marcador em `sessionStorage`" do plano anterior foi **removido** (D36).
Exceções registradas da L2 permanecem (data-model §3). A L2.1 e a L3 não acrescentam
exceção de nomes ou identificadores. Tasks geradas em 2026-11-24 (T135–T219, Phases
19–29, preservando o histórico L1/L2); próximo passo: gate R0 (T135).
