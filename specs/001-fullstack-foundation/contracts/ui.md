# Contrato da interface web de acesso — L3

**Date**: 2026-11-23 | **Revisão**: 2026-11-24 — validade da sessão (teto + inatividade),
permissões com descrição, nova entrada no próprio local, aviso entre abas só no logout,
nenhum armazenamento no navegador (research D34–D36). | **Status**: alvo L3, não
implementado; depende da L2.1 aceita.
**References**: [spec FR-004, NFR-011–013](../spec.md), [plano](../plan.md),
[research D22–D26, D34–D36](../research.md#revisão-l3--2026-11-23),
[HTTP L3](operations.md#7-contrato-http-l3), [sessão L2.1](operations.md#expiração-e-uso-da-sessão-l21),
[estado](../data-model.md#9-l3--catálogo-efetivo-de-permissões-e-estado-da-interface).

Comportamento observável pelo usuário e verificado por testes de componente e E2E.
Somente pt-BR. Textos abaixo são a intenção; a redação final pode variar sem mudar a
categoria. A interface não contém regra de negócio nem decide autorização: apresenta o
que a API responde.

## 1. Rotas

| URL | Comportamento |
|---|---|
| `/` | redireciona para `/contexto` |
| `/entrar` | formulário de entrada; com sessão válida, vai para `/contexto` |
| `/contexto` | protegida; sem sessão na carga → `/entrar` (sem parâmetro de retorno) |
| qualquer outra | “Página não encontrada”, com link para `/contexto` |

Após login pela tela de entrada o destino é sempre `/contexto`. Parâmetros de URL nunca
definem destino de redirecionamento. A nova entrada no próprio local (§4) não navega:
restaura a tela em memória da aba.

## 2. Telas e estados

| Estado | Conteúdo | Ações |
|---|---|---|
| Carregando | indicador com rótulo acessível, anunciado (`role="status"`) | — |
| Indisponível | “Serviço indisponível” + referência copiável (quando a API a devolveu) | Tentar novamente (manual) |
| Entrada | nome da empresa (de `GET /api/v1/tenant`), email, senha, botão Entrar | Entrar |
| Contexto | conta (nome, email), empresa corrente, validade da sessão (abaixo), permissões efetivas com **descrição e código** (de `permissionDetails`) | Sair |
| Sem permissão de contexto | conta e empresa; aviso “Sua conta não tem permissão para ver o contexto de acesso” | Sair |
| Empresa indisponível | empresa e aviso “Empresa indisponível no momento” | Sair |
| Nova entrada (diálogo) | §4: conteúdo protegido escondido; diálogo modal com nome da empresa, aviso “Sua sessão foi encerrada. Entre novamente para continuar.”, email, senha | Entrar, Sair |
| Saída por outra aba | tela de entrada com aviso “Você saiu em outra aba.” | Entrar |
| Nova versão | aviso não modal “Nova versão disponível” sobreposto a qualquer estado | Recarregar |

A empresa corrente é visível em todo estado autenticado. Nenhuma tela lista módulos.

**Validade da sessão** (tela de contexto): texto estático no fuso do navegador, via
`Intl` pt-BR, a partir de `user.sessionExpiresAt` (teto absoluto) e
`user.sessionIdleTimeoutMinutes`: “Sessão válida até 18:35, ou após 30 min sem uso.”
Se o teto não for no dia local corrente, inclui a data (“até 25/11 às 04:35”). Sem
contagem regressiva, sem aviso prévio de expiração, sem botão de renovar. O texto só
muda quando a tela é recarregada a partir de `/me` (ex.: após nova entrada).

**Permissões**: lista com a descrição como texto principal e o código como texto
secundário (ex.: “Consultar o próprio contexto de acesso — `foundation.context.read`”),
na ordem de `permissionDetails`. A interface não tem tabela própria de descrições; código
sem descrição nunca chega (a API não inicia).

## 3. Erros por categoria

| Resposta da API | Categoria | Mensagem / comportamento |
|---|---|---|
| login 401 `AUTH_INVALID` | credenciais inválidas | “Email ou senha inválidos.” Sem referência, sem distinguir conta, bloqueio ou empresa. Vale para a tela de entrada e para o diálogo. |
| login 429 `RATE_LIMITED` | muitas tentativas | “Muitas tentativas. Tente novamente em cerca de N minuto(s).” (de `Retry-After`); botão desabilitado até o prazo, depois liberado sem reenvio. |
| login 409 `SESSION_CONFLICT` | conflito de sessão | “Não foi possível concluir a entrada. Tente novamente.” (nova tentativa manual a partir do estado atual). No diálogo de nova entrada (§4), antes da mensagem: uma única revalidação de `/me` (mesma conta → restaura sem diálogo; outra conta → descarta; 401 → mantém o diálogo com a mensagem; 409 `TENANT_UNAVAILABLE` → empresa indisponível; rede/503 → indisponibilidade com referência no diálogo), conforme [D38](../research.md#d38--conflito-de-sessão-no-diálogo-de-nova-entrada-2026-11-24). |
| 503, falha de rede, `GET /api/v1/tenant` falho | indisponibilidade | mensagem + referência copiável (`traceId`) quando presente + tentar novamente |
| 500 ou resposta inesperada (inclui 400 `CSRF_INVALID`/`TENANT_REQUIRED`, 403 fora de `/context`) | erro inesperado | “Ocorreu um erro inesperado.” + referência copiável quando presente |
| 401 `AUTH_REQUIRED` em chamada de aba autenticada (`/me`, `/context`, revalidação) | sessão encerrada | **nova entrada no próprio local** (§4); estado preservado e escondido; outras abas não são avisadas |
| 401 `AUTH_REQUIRED` no pedido de sair | saída concluída localmente | descarta todo o estado, vai a `/entrar` com “Sua sessão foi encerrada.” e avisa as outras abas como logout (§6); não abre o diálogo |
| 401 `AUTH_REQUIRED` em `/me` na carga inicial (aba ainda sem estado) | sem sessão | `/entrar`, sem aviso |
| 409 `TENANT_UNAVAILABLE` | empresa indisponível | estado “Empresa indisponível”, só Sair |
| 403 em `/context` com `/me` 200 | sem permissão de contexto | estado “Sem permissão de contexto”, só Sair |

## 4. Nova entrada no próprio local

Disparo: 401 `AUTH_REQUIRED` em qualquer chamada de uma aba autenticada, **exceto o pedido de sair** (§5) — sessão
expirada por inatividade ou teto, revogada (operador, bloqueio, logout em outro
dispositivo da mesma sessão) ou ausente. Vários 401 simultâneos abrem um único diálogo.

| Etapa | Comportamento exigido |
|---|---|
| Esconder | Toda a área protegida recebe `hidden` + `inert` **antes** de o diálogo aparecer: nenhum dado protegido visível, focável ou lido por leitor de tela atrás do diálogo. A árvore continua montada (estado da aba preservado em memória). |
| Diálogo | `<dialog>` nativo com `showModal()`, título associado (`aria-labelledby`), aviso de sessão encerrada, nome da empresa, campos email/senha vazios (sem pré-preencher dado da conta encerrada), Entrar e Sair. Esc não fecha (evento `cancel` bloqueado). Foco inicial no campo email; foco preso no diálogo. |
| Entrar | `GET antiforgery` → `POST /sessions` → `GET /me`, com as mesmas regras de envio único, indicador anunciado e categorias de erro da tela de entrada. |
| Mesma conta (`/me.id` igual ao preservado) | Fecha o diálogo, remove `hidden`/`inert`, devolve o foco ao elemento que o tinha (ou ao título da tela) e restaura a tela exatamente como estava. Leituras (`GET`) negadas são repetidas automaticamente; `/context` é revalidado. Gravações negadas **não** são reenviadas: a tela mostra “A ação não foi concluída. Repita após entrar.” e o usuário decide. |
| Outra conta | Descarta todo o estado da aba (identidade, permissões, telas, leituras pendentes) e mostra `/contexto` da nova conta. |
| Sair (no diálogo) | Descarta todo o estado e vai a `/entrar`. Não chama o servidor (a sessão já é inválida) e não avisa outras abas. |
| Empresa inativa após entrar (`/me` 409) | Estado “Empresa indisponível”, conteúdo continua escondido, só Sair. |

Proibições: nada gravado no armazenamento do navegador; sem temporizador de expiração
no cliente; sem requisição periódica que mantenha a sessão viva; sem login silencioso
ou reenvio de credenciais guardadas. O estado preservado se perde ao fechar a aba.

## 5. Interação

- Login e logout obtêm novo token em `GET /api/v1/antiforgery-token` imediatamente antes
  do envio. Botão desabilitado e indicador anunciado enquanto pendente; um único envio.
- Nenhuma retentativa automática de login, `/me`, `/context` ou `/tenant`, exceto a
  repetição das leituras pendentes após nova entrada da mesma conta (§4) e a única
  revalidação de `/me` após 409 `SESSION_CONFLICT` no diálogo de nova entrada (§3). O
  login nunca é reenviado automaticamente.
- Sair com falha (rede, 5xx, 503): permanece autenticado, mostra indisponibilidade com
  referência e “Tentar sair novamente”; não avisa outras abas.
- Sair com 401 (sessão já expirada/revogada): conclui a saída localmente — descarta o
  estado, vai a `/entrar` com aviso de sessão encerrada e avisa as outras abas (§6).
- Sair com sucesso (204): descarta estado, avisa outras abas (§6), vai a `/entrar`.

## 6. Revalidação, várias abas e atualização

- Eventos que revalidam: carga, retorno de foco/visibilidade, navegação entre rotas. Em
  cada um: `GET /api/v1/tenant` (compara `buildId`) e, se a aba está autenticada ou em
  nova entrada, `GET /api/v1/me`. No máximo uma revalidação em andamento; sem polling.
  Um `/me` aceito renova a inatividade da sessão (operations §4), por isso só esses
  eventos o disparam.
- Resultado da revalidação de `/me`: 200 mesma conta → se a aba estava no diálogo,
  restaura **sem diálogo** (nova entrada feita em outra aba; o cookie é compartilhado) e
  repete leituras pendentes; 200 outra conta → descarta o estado e mostra `/contexto` da
  nova conta; 401 → §4.
- `BroadcastChannel('bps-sessao')`, mensagem `{ "tipo": "logout" }` sem dados, enviada
  **somente** após logout explícito (204, ou 401 no pedido de sair). Abas que recebem
  descartam o estado e vão a `/entrar` com “Você saiu em outra aba.” Expiração, revogação
  e Sair do diálogo de nova entrada não geram mensagem: cada aba aplica §4 na próxima chamada negada ou revalidação de foco. Sem o
  aviso de logout, a revalidação no foco ou a próxima chamada 401 leva ao diálogo, sem
  exibir dados da conta encerrada.
- `buildId` da API ≠ `buildId` embutido → aviso “Nova versão disponível”. Falha ao
  carregar arquivo da interface (`vite:preloadError`) → uma recarga automática, somente
  se a navegação atual não for ela própria uma recarga (tipo de navegação do navegador,
  sem marcador gravado); caso contrário, tela de indisponibilidade com “Recarregar”
  manual. Durante a nova entrada (§4) a recarga automática é suspensa e aparece o aviso de
  nova versão. Nunca exige limpar cache.

## 7. Acessibilidade e navegador

WCAG 2.1 AA nas telas e no diálogo: operação completa por teclado, foco visível e movido
ao título/mensagem ao trocar de tela, rótulos associados, erros com `role="alert"`,
contraste AA, `lang="pt-BR"`. Diálogo: foco preso enquanto aberto, fundo inerte, foco
devolvido ao fechar. E2E com axe: zero violações sérias ou críticas em cada estado,
**inclusive com o diálogo aberto**. Suporte: duas últimas versões estáveis de Chrome, Edge
e Firefox desktop (todas com `<dialog>`/`inert` nativos); E2E bloqueante em Chromium.

## 8. Privacidade e armazenamento

Sem telemetria ou envio de erros (NFR-006). Dados de conta, contexto, empresa e o estado
preservado da nova entrada existem só em memória da aba. **Nada** é gravado em
`localStorage`, `sessionStorage`, IndexedDB ou cookies legíveis por script (verificado ao
fim das jornadas E2E). Bundle sem identificador de tenant ou segredo.

## 9. Cenários E2E bloqueantes acrescentados (L3)

| Cenário | Verificação |
|---|---|
| Expiração com conteúdo escondido e restauração pela mesma conta | Sessão revogada pelo operador (ou expirada com configuração mínima no ambiente E2E) → próxima ação abre o diálogo; nenhum texto da tela de contexto visível/acessível; nova entrada da mesma conta restaura a tela e o foco. |
| Troca de conta | No diálogo, entrar com outra conta → estado anterior descartado; `/contexto` mostra a nova conta; nada da conta anterior no DOM. |
| Gravação não reenviada | A interface L3 não tem gravação além de login/logout, então a garantia é bloqueante em **teste de componente** do `ClienteApi`/provedor com gravação sintética (401 → diálogo → mesma conta → zero reenvio, aviso “A ação não foi concluída”). No E2E de expiração/restauração, o registro de rede prova que, após a restauração, só leituras (`GET`) foram repetidas e nenhum método não seguro foi reenviado. |
| Sair com sessão já revogada | 401 no sair → entrada com aviso de sessão encerrada, sem diálogo; outra aba aberta recebe o aviso de logout. |
| Várias abas: logout × expiração | Logout 204 na aba A → aba B vai a `/entrar` com aviso. Revogação da sessão → aba A abre o diálogo; aba B mantém o conteúdo até a próxima chamada/foco; nova entrada da mesma conta em A → B, ao ganhar foco, restaura sem diálogo; nova entrada de outra conta em A → B descarta. Novas entradas simultâneas em A e B com a mesma conta → a que recebe 409 revalida `/me` uma vez e restaura sem diálogo, com um único `POST /sessions` por aba. |
| Acessibilidade do diálogo | axe sem violações sérias/críticas com o diálogo aberto; Tab/Shift+Tab não saem do diálogo; Esc não fecha; foco devolvido ao fechar. |
| Armazenamento | `localStorage`, `sessionStorage` e IndexedDB vazios após as jornadas. |
