# ADR 0002: Sessão revogável e operação de acesso local ao tenant

**Data**: 2026-11-04
**Revisão**: 2026-11-05 — esclarecimentos de sessões e auditoria;
2026-11-08 — prazo de sessão configurável e limpeza auditada (research D21).
**Status**: proposto no planejamento L2; implementação e aceite pendentes.
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
