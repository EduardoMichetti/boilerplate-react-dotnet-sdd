# Contracts: EF/health e operação restrita L2

**Date**: 2026-11-05 | **Revisão**: 2026-11-24 — **L2.1**: sessão por inatividade + teto absoluto (research D31–D33, [§4](#expiração-e-uso-da-sessão-l21)) e descrições de permissão em `/context` (L3, D34); 2026-11-08 — prazo de sessão configurável e `sessao limpar` (research D21); decisões G1–G21 de [tasks.md](../tasks.md) (roles derivadas do banco, ordem de validação, cookie CSRF, rotas REST, emissão do cookie de sessão).
**Status**: EF/health L1 e contrato HTTP L2 (§4) entregues na Parada L2-2; host
operacional (§5) entregue na Parada L2-3. Recuperação/produção (§6) segue para a L4.
A subseção “Expiração e uso da sessão (L2.1)” da §4 é o **alvo L2.1** (2026-11-24), a
implementar e validar antes da L3. §7–§9 são o **alvo L3** (design 2026-11-23, research
D22–D30 e D34–D36), ainda não implementado. `openapi.json` só é atualizado junto com cada
rota/campo (o teste de contrato exige versão igual à servida); o delta 2.1.0 está na §4. Interface: [ui.md](ui.md).
**References**: [plano](../plan.md), [modelo](../data-model.md),
[API L2 entregue](openapi.json),
[quickstart](../quickstart.md).

## 1. Compatibilidade e configuração

`openapi.json` e `/openapi/v1.json` descrevem o contrato L2 de sete operações (cinco de
acesso + dois health checks), promovido na Parada L2-2 junto com os endpoints e testes.
Não publicar contrato fictício.
`/api/v1/me/tenants`, cadastro público e rotas de negócio continuam ausentes/404.

Nomes efetivos de configuração: `CodigoAplicacao`,
`IdAmbiente`, `IdTenant`, `NomeExibicaoTenant`, `EstadoTenant`, `StringsConexao:BancoApp`.
No ambiente usar `StringsConexao__BancoApp`. Identificadores de CLI confirmam o alvo
privado; nunca escolhem conexão ou outro banco.
Erro de configuração tem diagnóstico seguro, sem imprimir valores privados.

## 2. EF CLI — contrato existente L1, reutilizado em L2

Ferramenta local `dotnet-ef 10.0.12`; contexto `AcessoTenantDbContext`,
project `Platform.Infrastructure`, startup técnico `App.Api`.
Argumentos após `--`: `--tenant-id UUID --environment-id UUID`.
Os nomes desses argumentos existentes não mudam com a nomenclatura interna.

1. Operador fornece banco/roles/configuração privada; instalar bancos não é função da API.
2. Parar writers/API, colocar `EstadoTenant=Inativo` e usar owner de migrations.
3. Factory/preflight valida configuração, argumentos, role/banco e marcador antes de DDL.
   Histórico deve ser um prefixo válido da cadeia conhecida; migration de versão
   desconhecida bloqueia upgrade, mesmo sem pendentes, sem apagar histórico.
4. Marcador divergente ou estrutura sem marcador: falhar antes de alteração.
5. Somente banco vazio aprovado admite
   `Migracoes__PermitirInicializarBancoVazio=true`; ausente/false nega e valor
   inválido falha. Marcador correto permite aplicar/reaplicar com false.
6. Usar lock EF, migration versionada e callbacks idempotentes do marcador,
   sem conta/senha/padrão e sem corrigir identidade automaticamente.
7. Remover aprovação após inicialização, validar versão/dados, só então reativar.

O esquema é criado por uma migration inicial única com o modelo completo
([data-model §8](../data-model.md#8-migration-inicial-e-validação)); não se executa
`Migrate`/`EnsureCreated` no startup. Bancos locais de desenvolvimento podem ser
criados do zero a qualquer momento, pois não têm dados. Em banco com dados de
cliente, apagar volume ou histórico para contornar falha de migration/preflight é
proibido: a falha é investigada e corrigida.
Exit code EF 0 significa conclusão; qualquer falha é não zero, sem fallback.

## 3. Identidades técnicas e pré-requisitos L2

| Identidade | Privilégios necessários | Proibido |
|---|---|---|
| API runtime | CONNECT/USAGE locais; SELECT marcador/histórico/permissões; DML de usuários para Identity/lockout; SELECT/INSERT/UPDATE de sessões, sem DELETE. | Owner, DDL, superuser, CREATEDB/CREATEROLE, conexão a B ou alteração de marcador/auditoria operacional. |
| Operador de acesso | CONNECT local; leitura de marcador/contas/permissões; DML de contas/concessões/sessões (único com DELETE de sessões, para `sessao limpar`); INSERT de auditoria. | DDL, modificar identidade/epoch, UPDATE/DELETE de auditoria, conexão a outro tenant. |
| Owner de migrations | DDL/grants do alvo em manutenção. | Montagem na API ou uso rotineiro para FR-033. |
| Provisionador | Criar/restringir banco e roles por procedimento técnico. | Identidade do servidor web. |

**Nomes das roles (G6)**: derivados do nome do banco da instalação — `<banco>_owner`
(owner do banco e dos schemas `foundation`/`access`), `<banco>_runtime` (LOGIN da API) e
`<banco>_operador` (grupo NOLOGIN). Cada operador tem login individual membro de
`<banco>_operador`. As roles são criadas antes da migration (pelo `infra/docker/init-db.sh`
no Compose ou pela fixture nos testes); **os GRANTs por tabela são emitidos pela própria
migration inicial**, que deriva os nomes de `current_database()` e falha com mensagem
clara se `<banco>_runtime` ou `<banco>_operador` não existir. O script de inicialização
concede apenas CONNECT/USAGE de banco e schema.

Revogar CONNECT de PUBLIC e conceder somente aos roles daquela instalação.
Sem permissão pública de criação de schema. Configuração exemplificada com owner
no runtime não atende ao contrato, mesmo que o arquivo diga “runtime”.
Separar secrets efetivamente; testes tentam DDL/conexão cruzada com runtime e
tentam executar operações de conta com papel runtime (deve negar no host).

O host verifica `session_user=current_user`, igualdade com o login individual configurado em
`OperacaoAcesso:LoginAutorizado` e pertença a `<banco>_operador`, além dos grants mínimos. Não aceita SET ROLE,
`--actor`, username declarado ou cookie como prova de autorização.
Cada operador possui login PostgreSQL individual e segredo próprio, provisionados
pelo responsável do ambiente; não compartilhar o login entre pessoas. O papel
autenticado identifica esse operador no registro, sem nome fornecido no comando.
O acesso ao processo/segredo é autorizado por ACL do SO/orquestrador por instalação.
Não substituir isso por uma flag booleana `--authorized`.

## 4. Contrato HTTP L2

### Ordem de validação

Ordem efetiva nas cinco rotas L2 (G2 revisada, G9, G15):

1. Rate limiter (somente `POST /api/v1/sessions`) → 429 `RATE_LIMITED`.
2. `X-Tenant-Id` presente e com formato válido → 400 `TENANT_REQUIRED`/`INPUT_INVALID`.
3. Tenant coincide → 401 `AUTH_INVALID` no login; 403 `TENANT_ACCESS_DENIED` nas demais.
4. Marcador/versão do banco → 503 `DEPENDENCY_UNAVAILABLE` (antes de qualquer consulta a conta/sessão).
5. Autenticação do cookie de sessão, quando presente. Logout sem sessão válida → 401
   `AUTH_REQUIRED` aqui, antes de CSRF (G9).
6. Corpo JSON + FluentValidation (somente login; logout é `DELETE` sem corpo) → 400 `INPUT_INVALID`.
7. CSRF (login e logout) → 400 `CSRF_INVALID`.
8. Estado da instalação → credenciais/autorização.

Os itens 1–4 são middleware (antes da autenticação e do binding). Cookie antiforgery
(G7): `__Host-bps-csrf` em produção e `bps-csrf` somente em Development localhost; header
`X-CSRF-TOKEN`. Forwarded headers ficam desligados na L2: `CabecalhosEncaminhados:ProxiesConfiaveis`
vazio significa não processar `X-Forwarded-*`. Opções numéricas opcionais com valor vazio
(`""`) equivalem a ausentes (G10): `Sessao:InatividadeMinutos` (padrão 30),
`Sessao:TetoAbsolutoMinutos` (padrão 600, L2.1) e `OperacaoAcesso:RetencaoSessoesDias`
(padrão 30); qualquer outro valor segue a validação estrita (inclusive `" 30"` rejeitado).

- Health/OpenAPI não fazem lookup de conta/sessão, mesmo com cookies enviados.
- Nas cinco operações L2 exigir um único `X-Tenant-Id`, UUID não vazio.
  Ausência: 400/TENANT_REQUIRED. Formato, duplicação ou lista: 400/INPUT_INVALID.
- Tenant divergente no login: 401/AUTH_INVALID sem verificar conta.
  Nas demais rotas: 403/TENANT_ACCESS_DENIED sem lookup de sessão/credencial.
  Header nunca altera a conexão configurada.
- Conferir configuração/marcador/versão do banco local antes de dados de acesso.
  Divergência ou falha de dependência: 503/DEPENDENCY_UNAVAILABLE.
  Runtime exige cadeia de migrations conhecida completa, sem pendentes nem
  IDs desconhecidos; “nenhuma pendente” sozinho não valida uma versão futura.
- Login em instalação inativa: 401/AUTH_INVALID sem verificar senha.
  Em `/me` e `/context`, sessão ausente/inválida é 401; sessão válida com
  instalação inativa é 409/TENANT_UNAVAILABLE. Logout e GET antiforgery não exigem
  instalação ativa: somente obter o token e revogar a própria sessão ainda válida,
  sem consultas de dados. Não é enumeração de tenants.
- Conta desativada, bloqueada, stamp/epoch inválidos ou sessão expirada/revogada
  não autorizam nova operação: 401/AUTH_REQUIRED.
- Permissão ausente em `/context`: 403/TENANT_ACCESS_DENIED.

Limite de login é aplicado antes do processamento de credenciais; uma requisição
limitada retorna 429 sem provar a validade de headers/corpo/conta.
Demais erros seguem a ordem acima; toda entrada externa é validada antes de uso.
Falha de dependência nunca é convertida em 401 silencioso ou lista vazia.

### Jornada

1. GET `/api/v1/antiforgery-token` com tenant: obter requestToken e cookie CSRF
   HttpOnly, sem autenticar ou enumerar contas. Com cookie válido, token corresponde
   à identidade corrente; cookie inválido/expirado é tratado como anônimo nesse GET.
2. POST `/api/v1/sessions` (login → 201 Created) com `email`, `password`, tenant e `X-CSRF-TOKEN`.
   Validar explicitamente o antiforgery JSON. Se houver sessão corrente válida,
   o CSRF deve corresponder a ela; após validar credenciais, trocar a sessão desse
   cookie numa única transação: revogar a anterior com `Versao` esperada e inserir a
   nova chave, nunca reutilizá-la; commit antes do `Set-Cookie`. Conflito de
   `Versao` (outra troca, logout, Renew ou revogação concorrente): rollback,
   409/SESSION_CONFLICT, sem cookie novo. Falha de persistência: rollback, 503.
   Nos dois casos a anterior mantém estado e prazo; repetir exige novo GET
   antiforgery. Sessões em outros dispositivos/clientes da mesma conta permanecem válidas.
3. Obter novo token CSRF em **outro GET** após login (nova identidade).
4. GET `/api/v1/me`: somente a própria conta e validade da sessão.
   GET `/api/v1/context`: também exige `foundation.context.read` atual.
5. DELETE `/api/v1/sessions/current` (logout): exige sessão e CSRF correntes. Revogar no banco,
   confirmar commit e depois remover cookie/responder 204. Replay passa a 401.
   Instalação inativa não bloqueia esse encerramento; GET antiforgery continua
   disponível para obter o token necessário sem expor identidade/contexto.
6. Obter CSRF anônimo novo antes de um novo login depois do logout.

Inativar a instalação não revoga sessões nem renova epoch/prazo. Enquanto inativa,
consultas continuam bloqueadas; após reativar, apenas sessões ainda válidas e não
revogadas podem retomar. Expiração conta inclusive durante a inativação.
Não confundir essa suspensão com desativação/bloqueio da conta, que revoga sessões.

Payloads, formatos, limites e nomes JSON camelCase estão em `openapi.json`;
não renomeá-los para coincidir com nomes das classes internas.
Sem JWT, localStorage, refresh, listagem de tenants, cadastro ou reset por HTTP.
Não aceitar campos extras, senha/corpo vazio, tenant/lista/secret no payload ou
tipos incompatíveis; responder 400 Problem Details sem ecoar valores sensíveis.

### Cookies, store e autorização

- Produção: `__Host-bps-session`, HttpOnly, Secure, SameSite=Lax, Path `/`, sem
  Domain. CSRF usa cookie separado com mesmas proteções de transporte.
- Development HTTP somente localhost: `bps-session`, sem prefixo `__Host-`;
  cookies/host/mode incompatíveis causam erro, não fallback.
  Compose publica API somente em loopback do host. O listener pode usar a interface
  interna do container; não rejeitar o gateway Docker por não ser loopback nem
  usar Host/X-Forwarded-For livre como prova de acesso local. Sem exposição remota
  desse modo; para acesso não local, exigir HTTPS.
- Mesmo host com portas diferentes não isola cookies. Na validação A/B usar
  cookie jars separados e reenviar cookie de A a B deliberadamente no teste cruzado.
  Em publicação, usar hosts por instalação, nunca Domain compartilhado.
- ITicketStore usa referência `IdTenant/EpocaAcesso/IdSessao` protegida pelo handler
  nativo; ticket persistido também é protegido. Sem DbContext global/scoped capturado.
- SetApplicationName inclui produto/ambiente/tenant; volume/key ring por instalação,
  certificado externo obrigatório em produção e permissões mínimas dos arquivos.
- Sessão por **inatividade + teto absoluto** (L2.1, ver subseção abaixo), ambos fixados
  na emissão. Handler de cookie sem sliding expiration; cookie e ticket expiram no teto e
  não são reemitidos por uso. Mudar a configuração não altera sessões já emitidas.
  Retrieve verifica registro, conta/lockout/stamp/epoch e expiração efetiva em cada operação.
- Permitir sessões simultâneas da mesma conta em clientes/cookie jars distintos,
  sem identificação física do dispositivo. Login rotaciona somente a sessão do
  cookie recebido; logout revoga só a atual. Reset/desativação/lockout/revogação
  operacional afetam todas as sessões da conta no banco local.
- Renew só atualiza registro ainda ativo com versão esperada. Nunca upsert,
  reativação ou INSERT após revogação. Corrida/logout tem teste determinístico.
- Validação de principal rejeitado não pode deixar refresh pendente reemitir
  autorização. Configuração sem sliding e testes dos callbacks nativos são obrigatórios.
- Concessões lidas atualmente no banco; `/me` e logout têm autorização de recurso
  próprio explícita, não uma política que libera qualquer usuário autenticado.

### Expiração e uso da sessão (L2.1)

Alvo L2.1 (research [D31](../research.md#d31--sessão-por-inatividade-com-teto-absoluto-revisa-d21-2026-11-24)–[D33](../research.md#d33--registro-de-uso-da-sessão-limitação-de-gravação-e-concorrência)). Substitui o prazo absoluto único de D21.

| Item | Regra |
|---|---|
| Configuração (API) | `Sessao:InatividadeMinutos` 5–240 (padrão 30) e `Sessao:TetoAbsolutoMinutos` 30–720 (padrão 600), inteiros, limites inclusivos; ausente/vazio = padrão. Fora da faixa, não inteiro ou inatividade maior que o teto: a API não inicia (diagnóstico cita só chaves/faixas). `Sessao:PrazoAbsolutoMinutos` presente com valor: a API não inicia (chave obsoleta). |
| Emissão (login 201) | Grava `TetoAbsolutoEm = CriadoEm + teto`, `JanelaInatividadeMinutos` e `UltimoUsoEm = CriadoEm`. Cookie `Expires` e ticket `ExpiresUtc` = `TetoAbsolutoEm`. |
| Validade | `agora < min(UltimoUsoEm + janela, TetoAbsolutoEm)` + regras L2 (revogação, conta, lockout, stamp, epoch). Expirada → 401 `AUTH_REQUIRED`, sem revogar a linha. Verificada no servidor a cada requisição. |
| Renovação | Somente por **requisição autenticada aceita**: operação protegida (`/me`, `/context`; na L3, rotas de módulos) que passou por tenant, marcador, sessão, instalação ativa, CSRF e autorização e chegou ao handler. Ordem dos filtros (G31): autorização de recurso próprio → `FiltroInstalacaoAtiva` → `FiltroExigirPermissao` (em `/context`: `foundation.context.read`) → `FiltroRegistroUsoSessao` → handler; 400 `INPUT_INVALID`/`CSRF_INVALID` emitidos antes do handler não renovam, 400/422 do próprio handler renovam. Inclui as revalidações da interface por carga, foco/visibilidade ou navegação (`GET /me`), que o servidor não distingue. No máximo 1 escrita de `UltimoUsoEm` por minuto por sessão; nunca ultrapassa o teto; nunca emite `Set-Cookie`. |
| Não renovam | Negações antes do handler (400/401/403/409/429/503), login, logout, GET antiforgery, `GET /api/v1/tenant`, health, OpenAPI, arquivos da interface. |
| Instalação inativa | Não pausa teto nem inatividade; 409 não renova. |
| Concorrência | Conflito no registro de uso: descartado (a requisição segue). Falha de banco no registro: 503, handler não executa. Logout, troca no login e revogações em conflito só com registro de uso: recarregam e repetem até 3 vezes; disputa real mantém a regra L2 (login → 409 `SESSION_CONFLICT`); esgotado → 503. |
| Tolerância | A sessão é negada no máximo `janela` após a última requisição aceita e nunca antes de `janela − 60 s` (efeito da limitação de escrita; direção segura). |
| Motivo da negação ([D37](../research.md#d37--motivo-da-negação-de-sessão-só-no-log-2026-11-24)) | Resposta sempre 401 `AUTH_REQUIRED` genérico, idêntica em todos os motivos (exceto `traceId`). O backend registra `EventId 1002 SessaoNegada` (`Information`) com `Motivo` ∈ {`Inatividade`, `TetoAbsoluto`, `Revogada`, `Invalida`, `Ausente`}, `TraceId` igual ao da resposta e `IdOperacao`; nunca id de sessão, ticket, cookie, id/email da conta ou IP. |
| Sem | remember-me, refresh, sliding do handler, renovação silenciosa, job de expiração. |

**Delta de `openapi.json` 2.0.0 → 2.1.0** (aplicado no mesmo conjunto de mudanças que
implementa os campos, nunca antes: o teste de contrato compara a versão servida):

```json
"LoginResponse": {
  "required": ["tenantId", "expiresAt", "idleTimeoutMinutes"],
  "properties": {
    "expiresAt": { "type": "string", "format": "date-time", "description": "Teto absoluto da sessão emitida (UTC); nunca estendido pelo uso." },
    "idleTimeoutMinutes": { "type": "integer", "minimum": 5, "maximum": 240, "description": "Janela de inatividade fixada na emissão: a sessão expira após esse tempo sem requisição autenticada aceita, ou no teto, o que ocorrer primeiro." }
  }
},
"CurrentUser": {
  "required": ["id", "tenantId", "email", "displayName", "sessionExpiresAt", "sessionIdleTimeoutMinutes"],
  "properties": {
    "sessionExpiresAt": { "type": "string", "format": "date-time", "description": "Teto absoluto da sessão corrente (UTC)." },
    "sessionIdleTimeoutMinutes": { "type": "integer", "minimum": 5, "maximum": 240, "description": "Janela de inatividade da sessão corrente, em minutos." }
  }
}
```

`info.version` passa a `2.1.0` e `info.description` troca “Expiração absoluta fixada na
emissão (padrão 30 minutos, configurável de 5 a 480)” por “Expiração por inatividade
(padrão 30 min, 5–240) ou teto absoluto (padrão 600 min, 30–720), o que ocorrer primeiro,
ambos fixados na emissão; o uso renova só a inatividade”. Mudança aditiva (FR-021): o
consumidor independente L2 continua válido; o instante de expiração por inatividade não é
exposto (muda a cada uso).

### Política de senha e proteção de tentativas

Usar PasswordHasher/validadores do Identity, sem hash próprio.
Preparação/reset exigem 12–128 caracteres; habilitar as regras nativas de
maiúscula, minúscula, dígito e não alfanumérico. Login aceita 1–128 caracteres:
senha fora da política resulta em 401 genérico após a verificação, sem revelar a
política por um 400 de validação (não há senhas L1 a preservar).
Não exigir email confirmado enquanto não há fluxo de confirmação aprovado.

Habilitar lockout em toda conta desde a criação: cinco falhas por conta,
15 minutos; login válido só zera contagem quando a conta não está bloqueada.
Confirmar lockout, revogar todas as sessões da conta e mudar stamp na mesma
transação. Ao terminar o bloqueio, somente novo login permite acesso;
cookies anteriores continuam inválidos mesmo dentro do prazo original.
Concorrência não pode perder contagens nem sobrescrever revogação: conflito de
`ConcurrencyStamp` na contagem recarrega a conta e reavalia até três vezes; esgotado, 503.
Rate limiter nativo: cinco requisições/minuto/IP efetivo/instalação,
janela fixa e fila zero; rejeição 429/RATE_LIMITED com Retry-After.
Forwarded headers só de proxies explicitamente conhecidos; nunca confiar em
X-Forwarded-For livre. Uma réplica por instalação; escala horizontal exigiria
reavaliar o limite em memória, não acrescentar Redis preventivamente.

### Erros e cache

| HTTP | code |
|---|---|
| 400 | INPUT_INVALID, TENANT_REQUIRED, CSRF_INVALID |
| 401 | AUTH_INVALID (login genérico) ou AUTH_REQUIRED (sessão) |
| 403 | TENANT_ACCESS_DENIED |
| 409 | TENANT_UNAVAILABLE, somente `/me` e `/context` com sessão válida e origem inativa; SESSION_CONFLICT, somente no login que perde a troca da mesma sessão anterior |
| 429 | RATE_LIMITED + Retry-After |
| 503 | DEPENDENCY_UNAVAILABLE |
| 500 | INTERNAL_ERROR |

Usar Problem Details existente com type/title/status/code e traceId hexadecimal de
32 caracteres; sem redirect HTML, secrets, stack, senha/hash ou cookie.
INPUT_INVALID inclui `errors` por campo (chaves camelCase sem `$.`) para JSON
malformado, campo desconhecido, tipo incompatível e regra de validação, sem ecoar
valores. As rotas L2 não emitem 422 nem códigos de módulo; categorias, catálogo e
mecanismos em [ADR 0003](../../../docs/adr/0003-tratamento-de-erros-e-validacao.md).
Respostas de acesso, antiforgery e respectivos erros: `Cache-Control` contendo `no-store`
(em erros originados de exceção o framework acrescenta `no-cache`, `Pragma` e `Expires`, G18).
Falha ao persistir revogação não apaga cookie nem confirma saída; 503 seguro.

## 5. Host operacional L2 — FR-033

Executável `Platform.Operations`, sem servidor HTTP, publicado apenas no perfil
pontual Compose `operations` (imagem `infra/docker/Dockerfile.operations`). Não faz
migrations nem cria banco/roles. Comandos disponíveis:

Invocação (instalação A; ver [README](../../../README.md#3-contas-e-permissões-pelo-host-operacional-perfil-operations)):
`docker compose --project-name boilerplate-a -f infra/compose.yaml --env-file infra/.env.local --profile operations run --rm operations <comando>`.
`conta criar` e `conta redefinir-senha` exigem terminal interativo (TTY) para o prompt sem eco.

```text
conta criar --id-tenant UUID --id-ambiente UUID --email EMAIL --nome-exibicao NOME
conta desativar --id-tenant UUID --id-ambiente UUID --id-usuario UUID
conta reativar --id-tenant UUID --id-ambiente UUID --id-usuario UUID
conta redefinir-senha --id-tenant UUID --id-ambiente UUID --id-usuario UUID
conta revogar-sessoes --id-tenant UUID --id-ambiente UUID --id-usuario UUID
permissao conceder --id-tenant UUID --id-ambiente UUID --id-usuario UUID --codigo foundation.context.read
permissao revogar --id-tenant UUID --id-ambiente UUID --id-usuario UUID --codigo foundation.context.read
sessao limpar --id-tenant UUID --id-ambiente UUID
```

UUIDs não vazios; parâmetros desconhecidos/duplicados falham. Conta é sempre local.
`criar` usa UserManager e não concede permissão automaticamente. A senha e sua
confirmação são lidas por prompt sem eco; nunca argumento/env de linha de comando,
stdout, arquivo versionado ou exemplo com senha pronta.
Testes exercitam o caso de uso com entrada sintética, sem depender de console interativo.

Preflight exige configuração/argumentos/marcador/versão/papel correspondentes.
Não exige estado Ativo para uma manutenção autorizada; não concede acesso de usuário
enquanto a instalação estiver inativa. Papel runtime/owner ou alvo divergente nega
antes de mutação de conta. O operador possui somente segredo da própria instalação.

Desativar/reset/revogar-sessoes atualizam stamp e revogam sessões na mesma transação
da auditoria; reativar não ressuscita sessões nem remove lockout sem procedimento
explícito. Reset autorizado remove lockout/contagem, redefine senha via UserManager
e exige novo login. Recuperação L2 é esse reset restrito, não envio de email/token.
Concessão duplicada/revogação ausente são no-op auditado; conflito concorrente real
é erro explícito, não sobrescrita silenciosa: violação de PK em concessão simultânea,
zero linhas no DELETE de retirada simultânea ou `ConcurrencyFailure` do `UserManager`
geram `resultado=falha`, código `CONFLITO_CONCORRENTE` e saída não zero, sem repetição
automática. Também é conflito real a exceção de persistência com concorrência otimista ou
SQLSTATE `23505` (unicidade, inclusive duas criações simultâneas do mesmo email), `40001`
ou `40P01`. Código de permissão desconhecido falha.

Códigos de falha das operações de conta/permissão (G29): `CONFLITO_CONCORRENTE` somente
nos casos acima; `EMAIL_JA_CADASTRADO` quando `conta criar` encontra o email já em uso
(verificação determinística, não corrida); `EMAIL_INVALIDO` quando o Identity recusa o
email; `SENHA_FORA_DA_POLITICA`, `USUARIO_NAO_ENCONTRADO` e `PERMISSAO_DESCONHECIDA`
como já descritos; `FALHA_INTERNA` para qualquer outra exceção ou erro não previsto
(restrição de auditoria, privilégio, conexão, defeito). Todos são `resultado=falha`,
auditados em transação separada e com saída não zero. O resultado e o registro trazem
só o código; tipo e mensagem da exceção vão apenas para o diagnóstico em stderr/journal.

### `sessao limpar`

Remove registros de sessões encerradas há mais que a retenção; não há job,
`IHostedService` ou timer na API (agendamento externo é L4).

- Retenção em `OperacaoAcesso:RetencaoSessoesDias`: inteiro 1–365, padrão 30 quando
  ausente. Não há argumento para alterá-la. Valor fora da faixa ou não inteiro
  encerra antes de conectar: não zero, `codigo=RETENCAO_INVALIDA` em stderr/journal,
  sem registro no banco (o alvo ainda não foi validado).
- Após preflight e papel autorizados: `corte = InstanteOperacao - RetencaoDias`.
  Elegível somente: revogada com `RevogadaEm < corte`, ou não revogada com expiração
  efetiva `min(UltimoUsoEm + JanelaInatividadeMinutos, TetoAbsolutoEm) < corte` (L2.1:
  retenção contada da expiração efetiva ou da revogação). Encerrada exatamente no corte ou depois é mantida; sessão
  válida nunca é elegível, inclusive em concorrência com login/logout/Renew.
- Uma transação: delete rastreado das elegíveis com `Versao` esperada (sem
  `ExecuteDelete`) + INSERT do registro `Sucesso` com a quantidade removida.
  Conflito ou erro: rollback completo (nenhuma removida), registro `Falha`
  (`QuantidadeRemovida=0`) em transação separada e saída não zero. Sem repetição automática.
- Exatamente um `RegistroOperacaoAcesso` por execução com alvo validado,
  `TipoOperacao=LimparSessoes`, `LoginOperador` individual, `RetencaoDias`,
  `InstanteCorte`, `QuantidadeRemovida`, `Resultado`, `CodigoResultado`, `IdOperacao`;
  sem ids de sessões ou contas e sem autoria Sistema. Zero elegíveis é `sucesso`.
  Se nem o registro de falha puder ser gravado, vale a regra do journal protegido.
- Não altera contas, concessões, stamp, epoch nem sessões fora do critério.

Resultado em stdout: um objeto JSON com `idOperacao`, `idTenant`, `idAmbiente`,
`idUsuario` (quando conhecido), `loginOperador` autenticado individual,
`operacao`, `resultado`, `codigo`, `registradaEm`; em `sessao limpar`, também
`retencaoDias`, `instanteCorte` e `quantidadeRemovida`, sem `idUsuario`.
Sem email/nome/senha/ticket/conexão. `resultado` é `sucesso`, `negado` ou `falha`;
exit code 0 somente para sucesso persistido/auditado. Qualquer falha/negação retorna
não zero e diagnóstico seguro correlacionado em stderr. A ACL/journal protegido
do processo guarda falhas anteriores à possibilidade de auditoria no banco.
Se INSERT da auditoria falha, rollback da alteração e saída não zero.

Contas, permissões e sessões compartilham os [metadados auditáveis](../data-model.md):
autor/data de criação e última alteração e UUID de correlação `IdOperacao`.
O host fornece autoria pelo login individual autenticado; backend fornece usuário
somente após validar identidade/credenciais. Ação anônima ou automática não é
atribuída ao usuário declarado na entrada. Campos de auditoria não são recebidos
em payloads/argumentos e não são expostos nos DTOs HTTP já contratados.
Preencher dados e metadados na mesma transação; preservar os de criação.
RegistroOperacaoAcesso permanece imutável e separado, sem snapshots de secrets.
No-op de concessão/revogação mantém metadados da entidade, mas gera evento
operacional. Todo registro nasce com criação completa (autor, data e correlação).
O preenchimento é central (`IContextoOperacao` + `InterceptadorAuditoria`), com um
único `InstanteOperacao` do `TimeProvider` por comando, compartilhado com
`registradaEm`. Alterações do host são rastreadas e salvas com o token da entidade
(`ConcurrencyStamp` na conta via `UserManager`, `Versao` na sessão; concessão é só
INSERT/DELETE); `ExecuteUpdate`/`ExecuteDelete` não são usados em entidades auditáveis.
`idOperacao` e demais `Id*` gerados são UUID v7 do `InstanteOperacao` do comando.

## 6. Recuperação e produção — L4 preservada

Backup externo é do **banco de cada instalação**, incluindo acesso; não existe
backup/restauração de catálogo central. Validar origem/destino, renovar epoch,
invalidar sessões e revisar permissões antes de reabrir após restauração.
Não usar rollback destrutivo automático ou presumir que reverter imagem desfaz DDL.
RPO/RTO/retenção/responsáveis e teste de restauração são gates antes de produção.
L2 prepara as verificações de epoch e persistência de chaves, mas não executa deploy.

## 7. Contrato HTTP L3

Alvo L3. As cinco rotas L2 e a ordem G2 (§4) permanecem; mudanças abaixo são aditivas.

### `GET /api/v1/tenant` — identidade pública da instalação

| Item | Regra |
|---|---|
| Autenticação | Anônima. Sem `X-Tenant-Id` (header presente é ignorado, não validado). Cookies ignorados, sem lookup de sessão. |
| Ordem | Configuração da instalação → marcador/versão do banco → resposta. |
| 200 | `{ "id": uuid, "displayName": string(1–120), "buildId": string }` com instalação **ativa ou inativa**. |
| 503 | `DEPENDENCY_UNAVAILABLE` genérico para configuração inválida, marcador divergente, cadeia de migrations incompleta ou banco indisponível. |
| Cache | `Cache-Control: no-store`. |
| Não faz | Não enumera tenants, não revela estado ativo/inativo, versão, commit, data ou ambiente; não concede acesso; não emite cookies. |

`buildId`: 32 caracteres hexadecimais minúsculos (hash de conteúdo do build da
interface servida) ou `sem-interface` quando não há build (somente Development/Testes).
operationId `getPublicTenant`, tag `TenantContext`.

### `GET /api/v1/context` — campo aditivo `modules`

```json
"modules": [ { "id": "teste-sintetico", "contracts": [ { "id": "teste-sintetico.eco", "majorVersion": 1 } ] } ]
```

Sempre presente (lista vazia sem módulos), ordenado por `id`. Exige o mesmo que hoje:
sessão válida + `foundation.context.read`. `permissions` passa a listar somente códigos
do catálogo efetivo (concessões inertes omitidas).

**Campo aditivo `permissionDetails`** (D34): mesma lista de `permissions`, ordenada por
`code`, com a descrição pt-BR declarada em código:

```json
"permissionDetails": [ { "code": "foundation.context.read", "description": "Consultar o próprio contexto de acesso" } ]
```

`code` 1–128 (formato de `CodigoPermissao`), `description` 1–200. Sempre presente (vazio
sem concessões). A interface usa somente este campo para exibir permissões e não mantém
tabela própria. `permissions` (lista de códigos) é mantido para compatibilidade.
`user` continua sendo `CurrentUser` e, portanto, traz `sessionExpiresAt` (teto) e
`sessionIdleTimeoutMinutes` (L2.1), usados na exibição estática da validade. Mudança compatível (FR-021):
consumidores que ignoram campos desconhecidos não são afetados; o consumidor
independente L2 continua válido.

### Rotas de módulos

`/api/v1/modules/{idModulo}/…`, mapeadas por cada módulo composto, fora do documento
OpenAPI servido. Ordem: tenant (400/403) → marcador (503) → sessão (401
`AUTH_REQUIRED`) → corpo/validação → CSRF nos métodos não seguros (400 `CSRF_INVALID`) →
instalação ativa (409 `TENANT_UNAVAILABLE`) → permissão declarada vigente no catálogo
efetivo (403 `TENANT_ACCESS_DENIED`) → registro de uso da sessão (L2.1) → handler. Módulo omitido: rota inexistente → 404
JSON. Respostas `no-store`. Códigos de erro de módulo seguem ADR 0003
(`<MODULO>_<REGRA>`, 422).

### Cache e 404 em toda a API

Toda resposta de `/api/*`, `/openapi/*` e `/health*` (sucesso, erro do framework,
Problem Details e 404) contém `Cache-Control: no-store`. Caminho inexistente sob esses
prefixos → 404 Problem Details com `code` `NOT_FOUND` e `traceId`, nunca HTML.

## 8. Superfície da interface servida pela API

| Requisição | Resposta |
|---|---|
| `GET`/`HEAD` caminho sem extensão fora de `/api`, `/openapi`, `/health` | `index.html`, 200, `no-store` |
| `GET /assets/<nome-com-hash>` existente | arquivo, `public, max-age=31536000, immutable` |
| arquivo com extensão inexistente (ex.: `/assets/velho.js`) ou com ponto inicial (`/.vite/…`) | 404 Problem Details |
| outro arquivo estático existente na raiz | arquivo, `no-store` |
| Development/Testes sem build | navegações → 404 Problem Details; API inicia normalmente |
| Production sem `index.html` ou sem `.vite/id-build.txt` | inicialização falha com diagnóstico seguro (sem caminho absoluto) |

Cabeçalhos em **todas** as respostas (interface e API):

```text
Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'
X-Content-Type-Options: nosniff
Referrer-Policy: no-referrer
```

Sem HSTS (L4). Static files e fallback não autenticam cookies nem consultam banco.
Desenvolvimento: `vite dev` (porta local) encaminha `/api`, `/openapi`, `/health` à API;
os cabeçalhos acima só são verificados na interface servida pela API.

## 9. Composição de módulos

Seleção em compilação: o produto inclui um módulo referenciando seus projetos e
adicionando-o à lista de `App.Composicao`; omitir é remover a referência. Sem ativação
por configuração. `App.Api` e `Platform.Operations` validam a composição na
inicialização; falha encerra o processo com exit code não zero e diagnóstico seguro
contendo somente o código abaixo e ids declarados (nunca stack ou configuração).

| Código | Condição |
|---|---|
| `MODULO_ID_INVALIDO` | id fora de `^[a-z][a-z0-9-]{1,31}$` |
| `MODULO_ID_RESERVADO` | id `foundation` |
| `MODULO_DUPLICADO` | id repetido |
| `MODULO_DADOS_NAO_SUPORTADOS` | declaração de dados não vazia (L3) |
| `PERMISSAO_PREFIXO_INVALIDO` | permissão sem prefixo `<id>.` ou fora do formato |
| `PERMISSAO_DUPLICADA` | código repetido no catálogo efetivo |
| `PERMISSAO_DESCRICAO_INVALIDA` | descrição pt-BR ausente, vazia, >200 caracteres, com espaços nas pontas ou caracteres de controle (fundação ou módulo, D34) |
| `PERMISSAO_DESCRICAO_INVALIDA` | descrição pt-BR ausente, vazia, >200 caracteres, com espaços nas pontas ou caracteres de controle (fundação ou módulo, D34) |
| `CONTRATO_INVALIDO` | id de contrato fora de `<id>.<nome>` ou versão maior < 1 |
| `CONTRATO_DUPLICADO` | id de contrato publicado por mais de um módulo |
| `DEPENDENCIA_NAO_ATENDIDA` | contrato exigido ausente ou com versão maior diferente |
| `DEPENDENCIA_CICLICA` | ciclo entre módulos, inclusive auto-dependência |

Testes de arquitetura (bloqueantes) cobrem cada código com declarações sintéticas e
verificam referências: implementação de módulo só referencia `Platform.Modulos`,
`Platform.Application`, `Platform.Domain` e `*.Contratos` de outros módulos; projetos de
`backend/src/**` não referenciam nenhum módulo de `backend/tests/**`;
`Platform.Operations` não inicia servidor HTTP.

Contrato da fundação para módulos — `foundation.autorizacao` v1: identidade do usuário
corrente (id, tenant) e `ExigirPermissao(codigo)` em endpoints do grupo do módulo,
avaliada contra a concessão atual e o catálogo efetivo. Mudança incompatível incrementa a
versão maior (FR-021).

Host operacional: `permissao conceder` aceita somente código do catálogo efetivo
(fora → resultado `PERMISSAO_DESCONHECIDA`, existente); `permissao revogar` aceita código
de formato válido existente na conta, inclusive inerte. Demais regras da §5 inalteradas.
