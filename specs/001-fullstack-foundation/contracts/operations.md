# Contracts: EF/health e operação restrita L2

**Date**: 2026-11-05 | **Revisão**: 2026-11-08 — prazo de sessão configurável e `sessao limpar` (research D21).
**Status**: EF/health L1 entregues; autenticação e host operacional abaixo são
contratos planejados, não comandos/endpoints já disponíveis.
**References**: [plano](../plan.md), [modelo](../data-model.md),
[API entregue L1](openapi.json), [API alvo L2](future-openapi.json),
[quickstart](../quickstart.md).

## 1. Compatibilidade e configuração

Durante o planejamento, `openapi.json` e `/openapi/v1.json` continuam descrevendo L1.
Na implementação L2, promover o contrato de sete operações (cinco de acesso +
dois health checks) junto com os endpoints e testes. Não publicar contrato fictício.
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

Revogar CONNECT de PUBLIC e conceder somente aos roles daquela instalação.
Sem permissão pública de criação de schema. Configuração exemplificada com owner
no runtime não atende ao contrato, mesmo que o arquivo diga “runtime”.
Separar secrets efetivamente; testes tentam DDL/conexão cruzada com runtime e
tentam executar operações de conta com papel runtime (deve negar no host).

O host verifica `session_user=current_user` e papel exato configurado em
`OperacaoAcesso:PapelAutorizado`, além dos grants mínimos. Não aceita SET ROLE,
`--actor`, username declarado ou cookie como prova de autorização.
Cada operador possui login PostgreSQL individual e segredo próprio, provisionados
pelo responsável do ambiente; não compartilhar o login entre pessoas. O papel
autenticado identifica esse operador no registro, sem nome fornecido no comando.
O acesso ao processo/segredo é autorizado por ACL do SO/orquestrador por instalação.
Não substituir isso por uma flag booleana `--authorized`.

## 4. Contrato HTTP L2

### Ordem de validação

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

1. GET `/api/v1/auth/antiforgery` com tenant: obter requestToken e cookie CSRF
   HttpOnly, sem autenticar ou enumerar contas. Com cookie válido, token corresponde
   à identidade corrente; cookie inválido/expirado é tratado como anônimo nesse GET.
2. POST `/api/v1/auth/login` com `email`, `password`, tenant e `X-CSRF-TOKEN`.
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
5. POST `/api/v1/auth/logout`: exige sessão e CSRF correntes. Revogar no banco,
   confirmar commit e depois remover cookie/responder 204. Replay passa a 401.
   Instalação inativa não bloqueia esse encerramento; GET antiforgery continua
   disponível para obter o token necessário sem expor identidade/contexto.
6. Obter CSRF anônimo novo antes de um novo login depois do logout.

Inativar a instalação não revoga sessões nem renova epoch/prazo. Enquanto inativa,
consultas continuam bloqueadas; após reativar, apenas sessões ainda válidas e não
revogadas podem retomar. Expiração conta inclusive durante a inativação.
Não confundir essa suspensão com desativação/bloqueio da conta, que revoga sessões.

Payloads, formatos, limites e nomes JSON camelCase estão em `future-openapi.json`;
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
- Sessão com prazo absoluto `Sessao:PrazoAbsolutoMinutos` (inteiro 5–480, padrão 30;
  fora da faixa ou não inteiro impede a inicialização), fixado na emissão:
  `ExpiraEm = CriadoEm + prazo`. Sem sliding expiration ou renovação de prazo;
  mudar a configuração não altera sessões já emitidas.
  Retrieve verifica registro, conta/lockout/stamp/epoch e prazos em cada operação.
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
Respostas de acesso, antiforgery e respectivos erros: `Cache-Control: no-store`.
Falha ao persistir revogação não apaga cookie nem confirma saída; 503 seguro.

## 5. Host operacional L2 — FR-033

Executável planejado `Platform.Operations`, sem servidor HTTP, publicado apenas
no perfil pontual Compose `operations`. Não faz migrations nem cria banco/roles.
Comandos abaixo são **contrato futuro**, ainda indisponível:

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
automática. Código de permissão desconhecido falha.

### `sessao limpar`

Remove registros de sessões encerradas há mais que a retenção; não há job,
`IHostedService` ou timer na API (agendamento externo é L4).

- Retenção em `OperacaoAcesso:RetencaoSessoesDias`: inteiro 1–365, padrão 30 quando
  ausente. Não há argumento para alterá-la. Valor fora da faixa ou não inteiro
  encerra antes de conectar: não zero, `codigo=RETENCAO_INVALIDA` em stderr/journal,
  sem registro no banco (o alvo ainda não foi validado).
- Após preflight e papel autorizados: `corte = InstanteOperacao - RetencaoDias`.
  Elegível somente: revogada com `RevogadaEm < corte`, ou não revogada com
  `ExpiraEm < corte`. Encerrada exatamente no corte ou depois é mantida; sessão
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
