# ADR 0002: Sessão revogável e operação de acesso local ao tenant

**Data**: 2026-11-04
**Revisão**: 2026-11-05 — esclarecimentos de sessões e auditoria;
2026-11-08 — prazo de sessão configurável e limpeza auditada (research D21);
2026-11-24 — **Revisão L2.1** (sessão por inatividade + teto absoluto), status próprio na
seção [Revisão L2.1](#revisão-l21-2026-11-24).
**Status**: aceito (2026-11-12, mantenedor) para o texto original; Revisão L2.1
**aceita** (2026-11-24, mantenedor). Implementado nas Paradas L2-2 (sessão HTTP) e
L2-3 (host operacional e `sessao limpar`), com evidências no checkpoint da Phase 18 de
[tasks.md](../../specs/001-fullstack-foundation/tasks.md).
**Contexto**: [plano L2](../../specs/001-fullstack-foundation/plan.md),
[pesquisa](../../specs/001-fullstack-foundation/research.md),
[ADR 0001](0001-single-tenant-per-deploy.md).

## Contexto

A L1 entregou contas/permissões, marcador de banco e migrations por instalação.
FR-005 a FR-008 e FR-033 agora exigem autenticação HTTP, revogação na próxima
operação e manutenção restrita de contas, sem interface administrativa.
Um cookie sem estado não impede replay após logout; um procedimento que altera
senha diretamente por SQL ignora os mecanismos Identity e não prova autorização.

## Decisão

- Usar cookies/antiforgery/Data Protection nativos ASP.NET Core e `ITicketStore`
  com sessão no PostgreSQL exclusivo da instalação, ticket protegido e referência
  tenant/epoch/sessão protegida no cookie.
- Validar conta, lockout, stamp, epoch, prazo e concessões atuais a cada operação.
  Expiração absoluta (padrão 30 minutos, configurável de 5 a 480 e fixada na
  emissão), sem sliding expiration, refresh ou cache de
  autorização. Renew nunca recria sessão revogada nem estende sua validade.
  Bloqueio por tentativas inválidas revoga todas as sessões da conta; seu término
  exige novo login, sem restaurar sessões antigas.
- Logout só confirma após commit da revogação. Login gera nova referência;
  mudança de credencial/desativação exige novo login e não afeta outros clientes.
  Instalação inativa não bloqueia obter CSRF e encerrar a própria sessão ainda
  válida; essa exceção não libera consultas de dados.
  Reativação da instalação permite retomar sessões ainda válidas/não revogadas;
  inativação não revoga automaticamente, pausa prazo ou estende expiração.
  Sessões em dispositivos distintos são independentes: login/logout só rotacionam/
  encerram a sessão corrente; reset/desativação/lockout/revogação operacional
  encerram todas as sessões da conta.
- Manter key rings/volumes/nomes lógicos isolados por aplicação, ambiente e tenant;
  certificado externo em produção, HTTPS e CSRF explícito para JSON mutável.
- Adicionar host pontual `Platform.Operations`, fora da imagem API, com autorização
  operacional por credencial PostgreSQL local distinta e ACL do ambiente.
  Ele reutiliza casos de uso/Identity e não abre porta HTTP. Também remove sessões
  encerradas além da retenção (`sessao limpar`, padrão 30 dias), com um registro
  imutável por execução atribuído ao operador; a API não tem job de limpeza.
  Cada operador utiliza login individual não compartilhado; auditoria grava
  identidade autenticada e correlação, não um nome declarado no comando.
- Adicionar auditoria local persistida; operação e registro de sucesso são atômicos.
  Falha de auditoria impede sucesso. Não registrar senha/hash/ticket/conexão.
  Contas, permissões e sessões compartilham base auditável de criação/última
  alteração e correlação; histórico operacional é imutável e separado.
  `EntidadeAuditavel` serve às entidades próprias; usuário Identity mantém
  sua herança e implementa `IEntidadeAuditavel`. Autoria vem do contexto verificado,
  não da entrada.
  Preenchimento central por `IContextoOperacao` + `InterceptadorAuditoria`, só com
  alterações rastreadas (sem `ExecuteUpdate`/`ExecuteDelete` nessas entidades) e
  instante único do `TimeProvider` por operação; detalhes em research D19.
- Construção do zero: o esquema nasce de uma migration inicial única com o modelo
  completo e as convenções da Constituição 2.2.0 (PK `Id<Entidade>`, FK
  `FkId<Entidade>`, constraints `pk_/fk_/ix_/uq_/ck_`); bancos locais são criados
  do zero. Separação efetiva runtime/operador/owner é
  pré-requisito, não garantia inferida dos nomes de variáveis do Compose.
  Runtime exige cadeia de migrations conhecida completa; tooling aceita seu
  prefixo válido, sem IDs desconhecidos. Development HTTP em Docker só é
  publicado no loopback do host, sem exigir endereço loopback do gateway interno.

## Alternativas consideradas

JWT autossuficiente, cookie sem store ou validação periódica de stamp não entregam
a revogação exigida. Redis/provedor externo ampliam infraestrutura sem necessidade.
Painel/API administrativa ampliam superfície e escopo; SQL manual para usuários ou
owner rotineiro enfraquecem invariantes/privilégios. Seed padrão cria credencial
distribuída e não substitui bootstrap autorizado.

## Consequências

Há leitura de sessão/estado e permissão por operação, além de uma migration e um
executável operacional mínimo. Isso exige testes reais de concorrência, de banco
novo/reaplicação e de rejeição de histórico desconhecido, gestão de chaves e
separação de credenciais em bancos criados do zero. Toda criação e alteração tem
autoria e data reais. Depois da primeira publicação, mudanças de esquema passam a
ser aditivas.
Não há tecnologia nova, identidade central ou framework administrativo.
Escala horizontal, recuperação/backup de produção e frontend continuam decisões
dos incrementos posteriores; sessões e contas já devem permanecer locais e isoladas.

## Verificação

[Quickstart](../../specs/001-fullstack-foundation/quickstart.md): SC-002/003/005,
regressão SC-009, CSRF, replay, Renew/logout concorrente, runtime sem DDL,
operação sem autorização, auditoria atômica e restart com key ring persistente.
Este ADR não afirma que essas provas já foram executadas.

## Revisão L2.1 (2026-11-24)

**Status**: aceita (gate R0, T135 de
[tasks.md](../../specs/001-fullstack-foundation/tasks.md)).
**Aceite**: 2026-11-24, mantenedor (inclui a ordem de filtros G31).
**Origem**: FR-006 e SC-002 revisados (clarificações de 2026-11-23 e 2026-11-24);
research D31, D32, D33, D37 e decisão G31 de tasks.md.
**Substitui** no texto acima: "Expiração absoluta (padrão 30 minutos, configurável de 5
a 480 e fixada na emissão), sem sliding expiration" e, na limpeza, o critério de
sessão vencida. As demais decisões permanecem.

### Contexto

A expiração absoluta única encerra sessões em uso ativo ou mantém sessões ociosas por
tempo demais. A spec passou a exigir expiração pelo que ocorrer primeiro entre
inatividade e teto absoluto, renovação só por requisição autenticada aceita e motivo
da negação investigável sem ser revelado ao cliente.

### Decisão

- **Expiração efetiva** = `min(UltimoUsoEm + JanelaInatividadeMinutos, TetoAbsolutoEm)`,
  verificada no servidor a cada requisição com o `TimeProvider`; sessão expirada nunca
  volta a autorizar e não é revogada por expirar (D31).
- **Configuração**: `Sessao:InatividadeMinutos` (inteiro 5–240, padrão 30) e
  `Sessao:TetoAbsolutoMinutos` (inteiro 30–720, padrão 600), ausente ou vazio = padrão;
  fora da faixa, não inteiro ou inatividade maior que o teto impedem a inicialização com
  diagnóstico seguro. A chave obsoleta `Sessao:PrazoAbsolutoMinutos` **com valor** impede
  a inicialização, com diagnóstico que indica as chaves novas; vazia é tratada como
  ausente. Não há mudança silenciosa de política.
- **Fixado na emissão**: `TetoAbsolutoEm`, `JanelaInatividadeMinutos` e
  `UltimoUsoEm = CriadoEm` gravados no login; mudar a configuração não altera sessões
  emitidas. Cookie `Expires` e ticket `ExpiresUtc` = `TetoAbsolutoEm`;
  `SlidingExpiration = false`; sem remember-me ou refresh.
- **Registro de uso** (D33): só requisição autenticada aceita renova a inatividade, nunca
  o teto. `UltimoUsoEm` é gravado no máximo 1 vez por minuto por sessão
  (`IntervaloMinimoRegistroUso` = 60 s, não configurável), por entidade rastreada com
  `Versao` esperada; conflito descartado; falha de banco → 503 sem executar o handler.
  A negação por inatividade pode antecipar-se em até 60 s, nunca atrasar (tolerância a
  favor da segurança). Nenhum `Set-Cookie` em requisições normais. Login, logout,
  antiforgery, `/tenant`, health, OpenAPI e arquivos da interface não renovam;
  revalidações da interface por evento (`GET /me`) renovam.
- **Ordem de filtros (G31)**: estado da instalação e permissão passam a filtros de
  endpoint executados depois da autenticação/autorização de recurso próprio e antes do
  registro de uso: autorização → `FiltroInstalacaoAtiva` → `FiltroExigirPermissao`
  (quando exigida) → `FiltroRegistroUsoSessao` → handler. Assim 403/409 nunca renovam. Os
  casos de uso mantêm as mesmas verificações (defesa em profundidade, mesma resposta).
- **Revogações** (logout, troca no login, reset, desativação, bloqueio, revogação
  operacional) repetem até 3 vezes quando o conflito de `Versao` vem só do registro de
  uso; disputa real mantém a regra original (login 409 `SESSION_CONFLICT`); esgotado →
  503 sem confirmar. O registro de uso nunca reativa sessão revogada.
- **Motivo da negação só no log** (D37): resposta 401 `AUTH_REQUIRED` idêntica para
  `Inatividade`, `TetoAbsoluto`, `Revogada`, `Invalida` e `Ausente`; o backend registra
  `EventId 1002 SessaoNegada` (`Information`) com motivo, `TraceId` e `IdOperacao`, sem id
  de sessão/conta, ticket, cookie, email ou IP.
- **Limpeza**: `sessao limpar` elegível por `RevogadaEm < corte` ou expiração efetiva
  `< corte`, com pré-filtro indexado `UltimoUsoEm < corte` e `corte` como parâmetro;
  demais regras inalteradas (retenção, registro por execução, delete rastreado).
- **Migration** (D32): como nenhuma versão foi publicada (FR-015, clarificação
  2026-11-24), a migration inicial única é **regenerada** com as colunas, os CHECKs
  `ck_sessoes_autenticacao_teto`, `…_janela_inatividade`, `…_ultimo_uso` e os índices
  revisados, preservando o bloco de GRANTs; bancos locais/CI são recriados do zero e
  bancos com o histórico anterior são recusados como histórico desconhecido. A partir
  da primeira publicação (L4), só migrations incrementais.

### Alternativas consideradas

`SlidingExpiration` do handler de cookie: não respeita o teto, renova em requisições
negadas e emite `Set-Cookie`. Gravar uso em toda requisição: dobra escritas e gera
conflitos de `Versao` com requisições paralelas e revogações. Migration incremental:
cria caminho de upgrade para um estado nunca publicado. Revelar o motivo da negação ao
cliente: ajuda enumeração e não é necessário para a interface.

### Consequências

Uma escrita de sessão por minuto em uso ativo e três colunas/CHECKs a mais. Imprecisão
de até 60 s na inatividade, documentada e testada. Produtos derivados com a chave antiga
precisam migrar a configuração antes de iniciar. Bancos locais precisam ser recriados.

### Verificação

Matriz da [quickstart §7.1](../../specs/001-fullstack-foundation/quickstart.md) com
relógio controlado (fronteiras 29:59/30:00 e 599:59/600:00, faixas 4/5, 240/241, 29/30,
720/721, negadas não renovam, fixado na emissão, concorrência, motivo no log) e regressão
da §6 — tasks T137–T169. Esta revisão não afirma que essas provas já foram executadas.
