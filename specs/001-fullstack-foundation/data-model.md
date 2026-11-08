# Data Model: modelo de acesso completo da L2

**Date**: 2026-11-06
**Sources**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md).
**Status**: design L2, em **construção do zero**: nada foi publicado e não há
dados. Todo o modelo abaixo é novo e o esquema é criado por uma migration inicial
única (ver §8). Nomes seguem a Constituição 2.2.0.

## 1. Propriedade e limites

Cada instalação tem uma configuração, um backend e um banco PostgreSQL próprios.
Não existe catálogo central, vínculo multi-tenant global ou busca de sessões em
outros bancos. Mesmo email/Id em A e B representa contas independentes.
Schema organiza tabelas; não substitui banco exclusivo.

Identity/EF são restritos a Infrastructure. Application usa portas e modelos
próprios; API adapta DTOs JSON aos nomes internos. Propriedades C# herdadas do
Identity (`Id`, `UserId`, `Email`, `SecurityStamp`...) não podem ser renomeadas,
pois são declaradas nas classes-base do framework e usadas pelo `UserManager`/
`UserStore`; o padrão é aplicado no **nome da coluna** via `HasColumnName` (§2).
Cada campo persistido recebe XML summary e `HasComment`, inclusive auditoria.

## 2. Convenções de nomes no banco

| Elemento | Regra | Exemplos |
|---|---|---|
| Tabela | snake_case, no schema do módulo (`foundation`, `access`). | `access.sessoes_autenticacao` |
| Coluna | PascalCase, igual à propriedade C#, salvo mapeamento do Identity abaixo. | `CriadoEm`, `CodigoPermissao` |
| PK | `Id<Entidade>`, UUID v7; singleton usa `IdUnico`. | `IdSessao`, `IdOperacao` |
| PK do Identity | Propriedade `Id` mapeada para a coluna `Id<Entidade>`. | `usuarios.Id` → coluna `IdUsuario` |
| FK (com constraint) | `FkId<Entidade>[Papel]`, mesmo tipo da chave referenciada. Papel só quando há mais de uma FK para a mesma tabela. | `FkIdUsuario`, `FkIdTenant`, `FkIdUsuarioResponsavel` |
| FK do Identity | Propriedade `UserId` mapeada para a coluna `FkIdUsuario`. | `logins_usuario.FkIdUsuario` |
| Referência sem constraint | Mantém `Id*` (não é FK). | `IdUsuarioAlvo`, `IdOperacaoCriacao` |
| Código/número de negócio | `Codigo*`/`Numero*`, nunca PK/FK (exceção em §3). | `CodigoPermissao` |
| Constraints e índices | minúsculas, prefixo do tipo, coluna em snake_case: `pk_<tabela>`, `fk_<tabela>_<tabela_ref>[_<papel>]`, `ix_<tabela>_<colunas>`, `uq_<tabela>_<colunas>`, `ck_<tabela>_<regra>`. Máximo 63 caracteres (limite do PostgreSQL, que trunca sem erro). | `pk_permissoes_usuario`, `fk_permissoes_usuario_usuarios`, `ix_sessoes_autenticacao_fk_id_usuario`, `ck_sessoes_autenticacao_prazo` |

O prefixo `Fk` existe só na persistência (entidades EF em Infrastructure e colunas).
Domain, portas/modelos de Application e DTOs usam o nome de domínio (`IdUsuario`;
JSON `id`/`tenantId`), sem expor detalhe relacional. A convenção de nomes de
constraints é aplicada centralmente no `OnModelCreating` (laço sobre o modelo) e
um teste de modelo valida: PK em `Id*`/`IdUnico`, toda FK em `FkId*` apontando
para `Id*`, UUID v7 nas PKs geradas e nomes de constraints/índices no padrão e
com até 63 caracteres. As exceções da §3 são uma lista fechada no teste; tabela nova
fora da lista que viole a regra reprova.

## 3. Política de identificadores

Segue a Constituição IX: `Id*` é chave técnica opaca, nunca código de negócio.
PKs `Id*` são UUID v7 gerados no servidor, inclusive `UsuarioAplicacao.Id`
([D20](research.md#d20--tabelas-auxiliares-identity-concorrência-por-entidade-relógio-dos-uuids-e-troca-de-sessão-2026-11-07)):

- Geração única por `IdentificadorV7.Gerar(instante)`, helper puro em Domain sobre
  `Guid.CreateVersion7(DateTimeOffset)`, com o `InstanteOperacao` da operação
  (offset zero exigido). O timestamp do UUID coincide com `CriadoEm` em milissegundos;
  ordem entre Ids do mesmo milissegundo não é garantida nem usada.
- PKs `Guid` são `ValueGeneratedNever()`: o gerador do provider fica desligado e o
  Id é atribuído antes do `SaveChanges`/`CreateAsync`. Id vazio é rejeitado.
- Proibidos no código de produção, por teste de arquitetura: `Guid.NewGuid()` e
  `Guid.CreateVersion7()` sem argumento (leem o relógio do sistema ou geram v4).
  `SecurityStamp`/`ConcurrencyStamp` continuam gerados pelo Identity; são carimbos
  aleatórios, não `Id*`.

Exceções registradas (lista fechada no teste de modelo):

| Item | Justificativa |
|---|---|
| `IdentidadeBancoTenant.IdUnico` (int) | Singleton fixo `IdUnicoPadrao=1`, previsto na Constituição. |
| PK `(FkIdUsuario, CodigoPermissao)` de `PermissaoUsuario` | `CodigoPermissao` vem de catálogo fechado declarado no código-fonte (`CodigosPermissao`, constantes versionadas com o produto; não vem de env/Key Vault), sem tabela de catálogo. A PK impede repetição por usuário; teste garante códigos únicos e com formato válido; CHECK de formato no banco; código desconhecido é rejeitado; código nunca é reutilizado nem renomeado sem migration de dados. |
| Tabelas auxiliares Identity (`reivindicacoes_usuario`, `logins_usuario`, `tokens_usuario`) | Tipos de PK impostos pelas classes-base do Identity e exigidos pelo `UserOnlyStore`: reivindicação usa `int` identity (coluna `IdReivindicacaoUsuario`), login usa `(LoginProvider, ProviderKey)` e token usa `(FkIdUsuario, LoginProvider, Name)`. Sem uso na L2; detalhes na §7. |

## 4. Base auditável

Contas, permissões e sessões implementam `IEntidadeAuditavel` com a mesma base
conceitual de metadados. `EntidadeAuditavel` contém somente esses metadados e
implementa o contrato; não define uma PK única genérica nem exclusão lógica.
`PermissaoUsuario`/`SessaoAutenticacao` herdam essa classe-base.
`UsuarioAplicacao` conserva `IdentityUser<Guid>` e implementa a interface com os
mesmos campos, pois não pode herdar outra classe. Mapeamento/preenchimento
compartilhados em Infrastructure aplicam as mesmas regras aos três tipos.
Tipos puros da base/contrato ficam em Domain; não capturam HTTP/Identity/EF.

Exclusão lógica (`Excluido`) não entra na base: conta usa `EstaAtivo`, sessão usa
`RevogadaEm` e retirada de concessão é exclusão física registrada em
`RegistroOperacaoAcesso`. Um filtro global conflitaria com o índice único de email
do Identity e com a PK da concessão. Módulo futuro que precise disso define contrato
opcional próprio no seu plano.

Campos comuns nas três tabelas mutáveis:

| Campo | Tipo / regra |
|---|---|
| `CriadoEm` | UTC `timestamptz`, obrigatório. Em permissões é o instante da concessão; em sessões, o da emissão. Não há `ConcedidaEm`/`EmitidaEm` separados. |
| `TipoAutorCriacao` | enum `Operador`, `Usuario`, `Sistema`, `Anonimo`. |
| `AutorCriacao` | string até 256, referência textual do autor (não é chave): login individual do operador, UUID do usuário verificado ou identidade técnica do sistema; nulo somente para Anonimo. |
| `IdOperacaoCriacao` | UUID de correlação gerado pelo servidor, obrigatório. |
| `AlteradoEm` | UTC `timestamptz` opcional, preenchido somente quando há alteração efetiva. |
| `TipoAutorAlteracao` | mesmo enum, opcional até a primeira alteração. |
| `AutorAlteracao` | referência textual conforme tipo, nula somente para ação anônima ou ausência de alteração. |
| `IdOperacaoAlteracao` | UUID, obrigatório quando `AlteradoEm` estiver preenchido. |

`AutorCriacao`/`AutorAlteracao` não usam prefixo `Id` porque guardam login ou
identidade técnica textual, não chave técnica (Constituição IX).

### Preenchimento central e contexto da operação

- A porta `IContextoOperacao` em Application expõe `IdOperacao`, `TipoAutor`,
  `Autor` e `InstanteOperacao`. Cada host fornece sua implementação: API por
  requisição (usuário autenticado ou anônimo) e `Platform.Operations` (operador
  autenticado, inclusive na limpeza de sessões). Nunca vem de DTO ou argumento do
  consumidor. `Sistema` permanece no enum, sem produtor na L2.
- `InstanteOperacao` é lido **uma vez** do `TimeProvider` por operação e normalizado
  por `InstanteUtc`; o mesmo valor alimenta `CriadoEm`/`AlteradoEm`, `ExpiraEm`,
  `RevogadaEm` e `RegistradaEm` daquela operação.
- `InterceptadorAuditoria` (`SaveChangesInterceptor`) em Infrastructure percorre o
  ChangeTracker: em `Added` preenche criação; em `Modified` com alteração efetiva
  preenche última alteração e, se a entidade implementa `IEntidadeVersionada`,
  incrementa `Versao`; rejeita alteração dos campos de criação. Entidade sem
  alteração efetiva não muda metadados.
- **Somente alteração rastreada.** Entidades auditáveis são alteradas carregando a
  entidade e chamando `SaveChanges`. `ExecuteUpdate`/`ExecuteDelete` são proibidos
  nelas porque não passam pelo interceptor. Teste de arquitetura barra
  `ExecuteUpdate`/`ExecuteDelete` sobre essas entidades.

#### Concorrência por entidade

O token pertence a quem escreve a linha; não há dois tokens na mesma tabela.

| Entidade | Token | Escrita permitida | Regra no interceptor |
|---|---|---|---|
| `UsuarioAplicacao` | `ConcurrencyStamp` nativo (renovado pelo `UserStore` a cada `UpdateAsync`) | somente via `UserManager` | Preenche metadados; não toca em `ConcurrencyStamp`; sem `Versao`. Mudança só de `ConcurrencyStamp` não é alteração efetiva. |
| `PermissaoUsuario` | nenhum (imutável) | INSERT e DELETE | Rejeita `Modified`; metadados de alteração ficam nulos. Retirada registra `RegistroOperacaoAcesso`. |
| `SessaoAutenticacao` | `Versao` int64 | store e casos de uso de sessão; DELETE só pelo caso de uso de limpeza (operador) | Preenche metadados e incrementa `Versao`; `Deleted` não altera metadados e usa `Versao` como token. |
| `RegistroOperacaoAcesso` | nenhum (append-only) | INSERT | Rejeita `Modified` e `Deleted`. |

Conflito (`IdentityResult` com `ConcurrencyFailure`, `DbUpdateConcurrencyException`,
violação de PK em concessão duplicada concorrente ou zero linhas em DELETE de
concessão) é falha segura explícita, nunca upsert, no-op silencioso ou repetição cega.
Única repetição permitida: contagem de falhas de senha, que não pode perder
tentativas; o caso de uso recarrega a conta e reavalia senha e lockout até três
vezes e, esgotadas, responde 503 sem emitir sessão.

Metadados de criação são imutáveis. Nas alterações, preservar criação e preencher
última alteração; mesma transação dos dados. A auditoria não armazena snapshots,
senha/hash/ticket/conexão nem faz diff de campos sensíveis.
Tentativa de senha inválida é Anonimo, não o usuário indicado no email.
Login com credencial verificada pode atribuir mudanças à conta autenticada.
Não há limpeza automática: a remoção de sessões é o comando `sessao limpar`,
atribuído ao operador individual autenticado e registrado em `RegistroOperacaoAcesso`
(remoção não preenche metadados de alteração). `IdOperacao` é UUID gerado pelo servidor/processo,
distinto do `traceId` HTTP de 32 caracteres, mas associado a ele no diagnóstico.
Um no-op não altera metadados; exclusão de concessão mantém o evento operacional,
sem transformar auditoria de última alteração em histórico completo de versões.
Marcador e tabelas auxiliares Identity não recebem essa base nesta L2.

CHECKs (`ck_<tabela>_auditoria_*`) mantêm pares consistentes:
- Criação: `CriadoEm`, `TipoAutorCriacao` e `IdOperacaoCriacao` sempre presentes;
  `AutorCriacao` presente exceto para Anonimo.
- Última alteração: instante/tipo/correlação presentes juntos ou todos nulos;
  autor presente exceto para Anonimo.

## 5. Política de data e hora

| Regra | Aplicação |
|---|---|
| Tipo | `DateTimeOffset` UTC (offset zero) em C#, `timestamptz` no PostgreSQL. Sem `DateTime` para instantes. |
| Fonte | `TimeProvider` injetado (`TimeProvider.System` registrado na API, em `Platform.Operations` e no tooling; nos testes, implementação controlável própria, sem pacote novo). |
| Precisão | `InstanteUtc.Normalizar` converte para UTC e trunca para microssegundos, como o PostgreSQL, evitando divergência após gravar e ler. |
| Validação | `InstanteUtc.Validar(valor, nomeCampo)` rejeita `default` e offset diferente de zero; chamado pelos `Validar()` das entidades. |
| Relógio único | Sem `now()`/`HasDefaultValueSql` em colunas de data e sem comparação com o relógio do banco; `agora` vai como parâmetro (ex.: `agora >= ExpiraEm`). Sem exceções. |
| Proibições | `DateTime.Now/UtcNow/Today` e `DateTimeOffset.Now/UtcNow` fora dos testes, barrados por teste de arquitetura. |
| Valor padrão | Entidades não inicializam datas com o relógio; a ausência é detectada pela validação. |
| Saída JSON | Serialização padrão de `DateTimeOffset` (ISO 8601 com `+00:00`); teste de contrato fixa offset zero. |
| Exibição | Conversão para fuso local (ex.: America/Sao_Paulo) é da interface (L3), nunca do backend. |

`InstanteUtc` é um helper estático puro em Domain, só para normalizar e validar;
não lê o relógio. Foi rejeitado um `DateHelper` com `UtcNow` estático e conversão de
fuso: não é controlável em teste, e conversão de exibição não é papel do backend.

## 6. Configuração, sem entidade de catálogo

| Nome vigente | Regra |
|---|---|
| `CodigoAplicacao` | string de 1 a 100; identificação textual do produto (não secreta, não é `Id*`). Valor atual `boilerplate-react-dotnet-sdd`. |
| `IdAmbiente` / `IdTenant` | UUIDs não vazios, imutáveis, coincidentes com o marcador do banco. |
| `NomeExibicaoTenant` | string de 1 a 120; não concede acesso. |
| `EstadoTenant` | `Inativo` ou `Ativo`; ausência/valor inválido é erro. |
| `StringsConexao:BancoApp` | Secret local da identidade do processo; API usa runtime sem DDL. |
| `StringsConexao__BancoAppProprietario` | Entrada privada do Compose para o processo EF; nunca API. |
| `Migracoes:PermitirInicializarBancoVazio` | Tooling somente; ausente/false nega inicialização, valor inválido falha. |
| `ProtecaoDados:DiretorioChaves` | Volume privado persistente por instalação, gravável pela API. |
| `ProtecaoDados:ArquivoCertificado` / `ArquivoSenhaCertificado` | Arquivos privados externos; produção exige certificado e acesso à chave privada. Não argv/build/Git. |
| `OperacaoAcesso:PapelAutorizado` | Role de login PostgreSQL individual exata do operador autorizado; configuração protegida do host pontual e segredo não compartilhado. |
| `Sessao:PrazoAbsolutoMinutos` | API somente; inteiro decimal 5–480 inclusive, ausente = 30. Fora da faixa ou não inteiro impede a inicialização com diagnóstico seguro ([D21](research.md#d21--prazo-de-sessão-configurável-e-limpeza-auditada-de-sessões-2026-11-08)). Aplicado só na emissão. |
| `OperacaoAcesso:RetencaoSessoesDias` | `Platform.Operations` somente; inteiro decimal 1–365 inclusive, ausente = 30. Fora da faixa ou não inteiro impede `sessao limpar` antes de qualquer acesso ao banco. |

Para variáveis hierárquicas usar `__`, por exemplo `StringsConexao__BancoApp`.
Não introduzir aliases `ConnectionStrings`, `TenantId` ou `ApplicationId` no código.
O nome da chave não indica a origem (env, arquivo ou Key Vault): a mesma chave pode
vir de fontes diferentes por ambiente. O que é segredo é definido pela lista acima e
pelo [operations.md](contracts/operations.md); exemplos versionados nunca têm valores secretos.
Nome lógico Data Protection deriva de `CodigoAplicacao/IdAmbiente/IdTenant`; não é
escolhido pelo consumidor. Chaves/certificados nunca são compartilhados entre A/B.
Exceção Development localhost sem certificado é explícita, não fallback de produção.

## 7. Entidades

### IdentidadeBancoTenant — `foundation.identidade_banco`

| Campo | Tipo / regra |
|---|---|
| `IdUnico` | inteiro fixo `IdUnicoPadrao=1`, singleton (PK `pk_identidade_banco`). |
| `IdTenant` | UUID não vazio, chave alternativa (`uq_identidade_banco_id_tenant`) e identidade imutável do banco. |
| `IdAmbiente` | UUID não vazio, ambiente imutável. |
| `EpocaAcesso` | UUID não vazio, geração local de sessões. |
| `InicializadoEm` | UTC `timestamptz`, instante da inicialização técnica, do `TimeProvider` do tooling. |

Runtime lê o marcador/histórico, nunca altera identidade ou epoch.
O tooling EF só inicializa banco vazio explicitamente autorizado, com preflight
antes do DDL. Estrutura existente sem marcador ou marcador divergente sempre bloqueia.
Reaplicação com marcador correto é no-op de inicialização.
Rotação de epoch por restauração/revogação global é operação privilegiada futura L4;
L2 já rejeita sessão com epoch antigo e testa essa transição em fixture.

### UsuarioAplicacao — `access.usuarios`

`IdentityUser<Guid>` em `IdentityUserContext<UsuarioAplicacao, Guid>`.

| Campo | Tipo / regra |
|---|---|
| `Id` → coluna `IdUsuario` | UUID v7 não vazio, PK herdada (`pk_usuarios`). |
| `FkIdTenant` | FK para `identidade_banco.IdTenant` (`fk_usuarios_identidade_banco`), mesma instalação. |
| `Email` / `NormalizedEmail` | até 254; unicidade normalizada no próprio banco. |
| `UserName` / `NormalizedUserName` | derivados do email, normalização Identity. |
| `NomeExibicao` | string obrigatória de 1 a 120. |
| `EstaAtivo` | conta habilitada; false nega login e próximas operações protegidas. |
| `PasswordHash` | hash nativo Identity; nunca resposta/log/SQL de senha pura. |
| `SecurityStamp` | estado atual para invalidação de sessões. |
| `ConcurrencyStamp` | token de concorrência da conta (§4), gerenciado pelo Identity; a conta não tem `Versao`. |
| `LockoutEnabled` / `LockoutEnd` / `AccessFailedCount` | Lockout habilitado em toda conta desde a criação, cinco falhas/15 minutos; confirmação do bloqueio revoga sessões e muda stamp atomicamente. |
| Base auditável | Campos da §4. |

As tabelas auxiliares nativas `reivindicacoes_usuario`, `logins_usuario` e
`tokens_usuario` permanecem locais como exceção registrada (§3): `UserId` mapeado
para `FkIdUsuario`, `Id` da reivindicação para `IdReivindicacaoUsuario`, constraints
na convenção central e `HasComment` em cada coluna. Nenhum fluxo L2 as lê ou grava:
login verifica senha sem fábrica de claims, reset usa `UserManager` sem token
persistido e não há login externo ou 2FA. Runtime e operador não têm
INSERT/UPDATE/DELETE nelas; teste prova que continuam vazias após a suíte. Não
recebem base auditável. Sua existência não habilita cadastro público, OIDC,
recuperação por email ou contas globais; usá-las exige novo plano.
L2 define explicitamente as opções Identity, inclusive lockout; não presumir que o
default do framework prova a política.

### PermissaoUsuario — `access.permissoes_usuario`

| Campo | Tipo / regra |
|---|---|
| `FkIdUsuario` | UUID, FK local (`fk_permissoes_usuario_usuarios`), parte da PK. |
| `CodigoPermissao` | string de 1 a 128 do catálogo `CodigosPermissao`, parte da PK (exceção da §3); L2 admite `foundation.context.read`. |
| Base auditável | Campos da §4; `CriadoEm` é o instante da concessão. Concessão é imutável: sem `Versao`, metadados de alteração sempre nulos. |

Ausência significa negar. `/context` lê a concessão atual, não confia em claims
copiados no login. Revogação não afeta B nem exige logout para valer na próxima
operação. `/me` só autoriza a própria conta validada; logout, só a própria sessão.

### SessaoAutenticacao — `access.sessoes_autenticacao`

| Campo | Tipo / regra |
|---|---|
| `IdSessao` | UUID v7 não vazio, PK opaca gerada pelo servidor. |
| `FkIdUsuario` | UUID, FK local (`fk_sessoes_autenticacao_usuarios`), exclusão restrita para preservar rastreabilidade. |
| `EpocaAcesso` | UUID não vazio, cópia da geração vigente na emissão. |
| `TicketProtegido` | `bytea` anulável; serialização nativa protegida por Data Protection; inserido na emissão e removido na revogação. Pode permanecer após expiração até a remoção da linha por `sessao limpar`, mas nunca autoriza uso. |
| `ExpiraEm` | UTC, `CriadoEm` + `Sessao:PrazoAbsolutoMinutos` vigente na emissão (padrão 30); imutável. CHECK `ck_sessoes_autenticacao_prazo`: diferença entre 5 e 480 minutos. Mudança posterior da configuração não altera sessões emitidas. |
| `RevogadaEm` | UTC anulável; estado da sessão (não metadado de auditoria), preenchido na revogação com o mesmo instante de `AlteradoEm`. |
| `Versao` | int64 positivo, token de concorrência incrementado pelo `InterceptadorAuditoria`. |
| Base auditável | Campos da §4; `CriadoEm` é o instante de emissão. |

Índices: PK `IdSessao`, `ix_sessoes_autenticacao_fk_id_usuario` para revogação da
conta, `ix_sessoes_autenticacao_expira_em` e `ix_sessoes_autenticacao_revogada_em`
para o critério de `sessao limpar`. CHECKs: UUIDs válidos, prazo de 5–480 minutos
e sessão revogada sem ticket. Expiração é verificada no uso; não depende de limpeza,
e a API não executa remoção em segundo plano. O índice por conta não é único: uma conta pode ter
sessões simultâneas em clientes/dispositivos distintos. Não modelar fingerprint de
dispositivo. Não há coluna de tenant duplicada: a conta tem FK ao marcador único;
a referência do cookie também carrega tenant/epoch e ambos são validados.

O ticket contém conta, tenant, epoch, stamp e prazos, sem senha/hash/conexão nem
permissões usadas como autorização permanente. Nomes no protocolo existente são
mantidos; nomes dos novos tipos internos são portugueses.

#### Transições e concorrência

| Origem → destino | Regra / efeito |
|---|---|
| Ausente → ativa | Store somente após validar instalação, conta e senha; INSERT antes do sucesso HTTP. |
| Anterior ativa → revogada + nova ativa (login com cookie) | Uma transação: revoga a anterior com `Versao` esperada e insere a nova; commit antes do `Set-Cookie`. Conflito de `Versao` faz rollback (409 SESSION_CONFLICT); falha de persistência faz rollback (503). Em ambos a anterior mantém estado e prazo e nenhuma sessão nova existe. |
| Ativa → ativa (Renew) | Carrega a sessão rastreada, confere ativa/não expirada e epoch/stamp, e salva com `Versao` esperada; não altera criação/expiração. Só é alteração (metadados + versão) se `TicketProtegido` mudar; bytes iguais são no-op. Sem upsert. |
| Ativa → revogada | Carrega rastreada e salva com `Versao` esperada: `RevogadaEm` preenchido, ticket removido, metadados de alteração e versão pelo interceptor. |
| Ativa → expirada | `agora >= ExpiraEm`; nenhum Retrieve/Renew concede acesso. |
| Revogada/expirada → ativa | Proibido; novo login exige outra `IdSessao`. |
| Revogada/expirada → removida | Somente `sessao limpar`, quando `RevogadaEm < corte` ou (`RevogadaEm` nulo e `ExpiraEm < corte`), com `corte = InstanteOperacao - RetencaoSessoesDias`. Delete rastreado com `Versao` esperada, na transação do registro da execução. Ativa → removida é impossível pelo critério. |

Logout só confirma após commit. Disputa Renew/logout não pode restaurar ticket;
conflito de `Versao` (zero linhas atualizadas) nunca autoriza INSERT.
Revogação de todas as sessões da conta carrega as sessões ativas dessa conta e
salva cada uma na mesma transação, sem `ExecuteUpdate`. Erros de persistência
propagam como falha segura, não sessão ausente ou sucesso.
Estado inativo da instalação bloqueia consultas, não a obtenção de CSRF e a
revogação da própria sessão ainda válida; nenhuma transição de volta para ativa
é necessária para encerrar o acesso.
Inativação da instalação não é revogação de conta/sessão: mantém epoch, registros
e prazos, bloqueando somente o uso. Reativação permite sessão ainda válida e
não revogada; tempo inativo conta normalmente para a expiração.
Login com cookie anterior não reutiliza sua chave: revogar e emitir nova referência
na mesma transação; `StoreAsync` participa da unidade de trabalho da requisição e o
login nunca chama `RenewAsync` com a chave anterior. Dois logins com a mesma sessão
anterior: o segundo commit encontra `Versao` alterada e perde, sem sessão adicional.
Essa rotação e logout afetam somente o cookie/sessão corrente, não outras sessões
da mesma conta; revogação operacional de acesso afeta todas.
Na emissão, revalidar conta/stamp dentro da transação para não ignorar alteração
concorrente de acesso. Desativação/reset revogam todas as sessões da conta
e mudam stamp; mudança de permissões é consultada diretamente em cada operação.
Bloqueio por tentativas inválidas também revoga todas as sessões e muda stamp
na transação que confirma o lockout. O término dos 15 minutos não altera
`RevogadaEm` nem restaura tickets; somente novo login emite outra sessão.

### RegistroOperacaoAcesso — `access.operacoes_acesso`

Auditoria persistida da operação restrita FR-033, sem novo serviço de logging.
Registro histórico append-only, sem campos de última alteração nem herança
mutável `EntidadeAuditavel`. Metadados das entidades não substituem esse histórico.

| Campo | Tipo / regra |
|---|---|
| `IdOperacao` | UUID v7 não vazio, PK do evento e correlação do comando, compartilhada pelos metadados das entidades alteradas nesse comando. |
| `IdTenant` / `IdAmbiente` | UUIDs do alvo validado, nunca identidade de outro banco. Referências sem constraint. |
| `IdUsuarioAlvo` | UUID opcional; referência **sem** FK, para preservar a auditoria após evolução da conta; por isso não usa prefixo `Fk`. |
| `LoginOperador` | string de 1 a 128; login PostgreSQL individual autenticado (`session_user=current_user`), atribuído a um único operador, não argumento livre ou credencial compartilhada. Referência textual, não chave, por isso sem prefixo `Id`. |
| `TipoOperacao` | enum fechado das operações em [operations.md](contracts/operations.md). |
| `CodigoPermissao` | string opcional até 128, somente quando relevante. |
| `RegistradaEm` | UTC `timestamptz`, o mesmo `InstanteOperacao` das alterações do comando. |
| `Resultado` | enum `Sucesso`/`Negado`/`Falha`; jamais incluir senha/token/ticket. |
| `CodigoResultado` | string até 100, código estável seguro, sem mensagem SQL. |
| `RetencaoDias` | int 1–365, somente `LimparSessoes`: retenção aplicada. |
| `InstanteCorte` | UTC `timestamptz`, somente `LimparSessoes`: `RegistradaEm - RetencaoDias`. |
| `QuantidadeRemovida` | int ≥ 0, somente `LimparSessoes`; 0 quando nada era elegível ou houve falha (rollback). |

CHECK `ck_operacoes_acesso_limpeza`: os três campos de limpeza estão todos presentes
quando `TipoOperacao=LimparSessoes` e todos nulos nos demais tipos; na limpeza
`IdUsuarioAlvo` e `CodigoPermissao` são nulos. O registro nunca lista sessões ou
contas removidas. Cada execução de `sessao limpar` com alvo validado gera
exatamente um registro (`Sucesso`, inclusive com zero removidas, ou `Falha`), sempre
com `LoginOperador`; não existe autoria Sistema. Invocação rejeitada antes da
validação do alvo segue a regra de falha pré-auditoria abaixo.

Índices `ix_operacoes_acesso_id_usuario_alvo_registrada_em` e por instante.
Operador pode INSERT, não UPDATE/DELETE dos registros. API não altera auditoria
operacional. Mutation e registro de sucesso usam a mesma transação/contexto; erro de
auditoria faz rollback da alteração. Tentativas negadas/falhas são registradas
separadamente quando o alvo for validado e estiver disponível; falha antes disso
sai com código não zero e diagnóstico correlacionado para o journal protegido do
processo. Não gravar nada num banco de identidade divergente apenas para registrar a negativa.

## 8. Migration inicial e validação

1. Uma **única** migration inicial cria todo o modelo deste documento. A migration
   de rascunho hoje no repositório (`20261102141948_EsquemaInicialAcessoTenant`,
   Designer e snapshot) é descartada e gerada de novo a partir do modelo.
2. Bancos locais A/B são criados do zero pelos scripts de banco/grants (runtime,
   operador e owner distintos), e a migration é aplicada pelo owner.

Histórico segue em `foundation.__EFMigrationsHistory`.
Runtime/readiness aceitam somente a cadeia conhecida completa. Tooling aceita
prefixo válido dela para aplicar o restante (necessário a partir da segunda
migration). Migration desconhecida no histórico é incompatibilidade explícita,
mesmo sem migrations pendentes; não apagar histórico nem reescrever o marcador
para contornar.

Testes devem provar: banco novo A/B, reaplicação sem duplicação ou perda,
rejeição de histórico desconhecido, restrições/transações,
revogação/expiração/stamp/epoch, disputa Renew/logout, reset/bloqueio de A sem
efeito em B, preenchimento/imutabilidade de auditoria, autoria
individual/anônima (sem produtor de Sistema), `ExpiraEm` com prazos 5/30/480 e CHECK
de prazo, limpeza com fronteira exata do corte e um registro por execução, SC-003 com resultados persistidos corretos, instantes
com offset zero e microssegundos iguais após gravar e ler, conflito de `Versao` em
Renew/revogação/troca de sessão no login (falha preserva a anterior; disputa conclui
uma única troca), `ConcurrencyStamp` na conta sem `Versao`, concessão imutável,
UUID v7 nas PKs geradas com timestamp igual ao `InstanteOperacao` do relógio de teste,
PKs `Guid` sem geração pelo provider, tabelas auxiliares Identity vazias e listadas
como exceção, convenções de nomes da §2 e ausência de `ExecuteUpdate`/relógio
proibido (inclusive `Guid.NewGuid()`/`Guid.CreateVersion7()` sem argumento) no
código de produção.
O modelo não adiciona dado de negócio, seed de conta ou tabelas centrais.
Restauração, retenção de auditoria e políticas de backup de produção continuam L4,
antes de uso real; logs e auditoria ficam restritos ao operador.
