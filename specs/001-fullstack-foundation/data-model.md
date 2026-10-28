# Data Model: Fundação full-stack reutilizável

**Date**: 2026-10-13
**Sources**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md).

Modelo de design, não entidades ou migrations implementadas. **I1** é backend e
migration de usuários/acesso; **I2** adiciona autenticação/sessões HTTP; operação
ampliada e publicação são posteriores. Não confundir tabelas previstas com código entregue.

## 1. Propriedade e limites

| Local | I1 | Evolução | Proibido |
|-------|----|----------|----------|
| Configuração da instalação (Dokploy / `.env` / secrets) | `ApplicationId`, `EnvironmentId`, `TenantId`, `TenantDisplayName` e conexão do banco da instalação. | Key ring/certificado e destino de backup externo. | Banco apartado de catálogo central (`platform_catalog`), secrets no Git, bundle, imagem ou logs. |
| Banco exclusivo da instalação do cliente | Marcador em `foundation`; Identity e concessões em `access`. | AuthSession I2 e schemas de módulos futuros. | Identidade global, registros de outro cliente ou dados compartilhados entre instalações. |
| Testes | Dois bancos PostgreSQL reais descartáveis simulando duas instalações de clientes + dados sintéticos. | Fixtures opcionais I3. | Distribuição na API/imagem de produção. |

Schema não substitui banco exclusivo por instalação de cliente. Email/CPF igual nas instalações A e B não vincula registros.
O par `(TenantId, UserId)` identifica a conta; um UUID local pode coincidir em bancos de instalações distintas sem representar a mesma conta.

Usar UUID não vazio, instantes UTC `timestamptz`, índices/FKs e Version explícito
onde há concorrência. Identity/EF pertencem a Infrastructure; Domain e Application
usam conceitos/portas próprias, sem herdar classes de persistência.

## 2. Configuração da instalação (sem banco de catálogo central)

Não existe banco `platform_catalog`, `CatalogDbContext`, `CatalogIdentity`, tabela `Tenant`
central ou `TenantStorageBinding`. Cada instalação de cliente recebe sua identidade e sua
conexão pelas variáveis de ambiente/secrets da própria aplicação (ex.: na aba Environment
do serviço daquele cliente no Dokploy/VPS ou em `.env.local` no desenvolvimento):

| Parâmetro de configuração | Tipo | Regra |
|---------------------------|------|-------|
| `ApplicationId` | string, 1 a 100 | Identidade confiável do produto derivado. |
| `EnvironmentId` | UUID | Identificador imutável do ambiente daquela instalação; não vazio. |
| `TenantId` | UUID | Identificador imutável do cliente/empresa daquela instalação; não vazio. |
| `TenantDisplayName` | string, 1 a 120 | Nome de apresentação da empresa naquela instalação. |
| `TenantState` | enum (`Inactive` / `Active`) | Estado operacional configurado para a instalação. |
| `ConnectionStrings:AppDb` (runtime) / credencial de migration (tooling) | secret / arquivo privado | Conexão exclusiva ao banco PostgreSQL daquela instalação; separada entre runtime (sem DDL) e tooling (owner de migration). |
| `Migrations:AllowInitializeEmptyDatabase` (somente tooling) | booleano; ausente equivale a `false` | Somente `true` autoriza inicialização do banco vazio aprovado na conexão privada, após validar identidade/argumentos, banco, role e estado inativo. Valor inválido falha; não é montado nem usado pela API. |

A API valida na inicialização e no readiness que a configuração da instalação coincide
com o marcador `TenantDatabaseIdentity` gravado no banco configurado, impedindo que uma
instalação seja apontada por engano para o banco de outro cliente.

## 3. Banco da instalação do cliente — I1

### TenantDatabaseIdentity

| Campo | Tipo | Regra |
|-------|------|-------|
| SingletonId | inteiro | 1; uma linha. |
| TenantId | UUID | Único, imutável; identidade do banco. |
| EnvironmentId | UUID | Ambiente esperado no catálogo/configuração. |
| AccessEpoch | UUID | Geração local de acesso; começa na inicialização técnica. |
| InitializedAt | instante UTC | Obrigatório. |

Runtime somente lê marcador e histórico; tooling inicializa. AccessEpoch pode ser
renovado pelo procedimento de revogação global local/restauração em I2/I4.
TenantId/EnvironmentId nunca são trocados para encobrir referência errada.

Factory verifica marcador antes de DDL. Sem marcador, apenas banco vazio aprovado
explicitamente com `Migrations:AllowInitializeEmptyDatabase=true` pode receber migration
e inicialização. Sem aprovação, falhar antes de DDL; estrutura existente sem marcador
ou marcador divergente é erro mesmo com aprovação. Marcador correto permite aplicar
ou reaplicar migrations sem esse sinal; removê-lo ou voltar a `false` após inicialização.
UseSeeding/UseAsyncSeeding inserem o marcador desse alvo uma vez, sem HasData dependente
do tenant nem seed de usuário.

### AppUser

Modelo técnico `IdentityUser<Guid>` no `IdentityUserContext<AppUser, Guid>` do
tenant. Campos/tabelas nativos de Identity permanecem locais ao mesmo banco.
Não incluir RoleManager/editor de papéis ou provedor externo sem necessidade concreta.

| Campo | Tipo | Regra |
|-------|------|-------|
| Id | UUID | PK local, não vazio. |
| TenantId | UUID | FK para TenantDatabaseIdentity.TenantId; impede conta de outro tenant nesse banco. |
| Email / NormalizedEmail | string, até 254 | Email válido; índice único normalizado **neste banco**. |
| UserName / NormalizedUserName | campos Identity | Derivados do email pela política inicial, normalização nativa. |
| DisplayName | string, 1 a 120 | Obrigatório. |
| IsEnabled | booleano | Conta desativada não pode acessar; ciclo local ao tenant. |
| PasswordHash | campo Identity | Hash nativo; nunca senha pura, resposta ou log. |
| SecurityStamp / ConcurrencyStamp | campos Identity | Invalidação e concorrência por conta local. |
| LockoutEnd / AccessFailedCount | campos Identity | Estrutura inicial; I2 aplica 5 falhas/15 minutos somente à conta local. |

Uniqueness de email em A não impede cadastro de mesmo email em B.
Pertencimento/vínculo é representado por TenantId e estado da conta; não existe
TenantMembership global permitindo reutilizar a mesma identidade entre tenants.
Tabelas nativas auxiliares de claims/logins/tokens não habilitam fluxos externos
nem são preenchidas automaticamente pela migration.

### UserPermission

| Campo | Tipo | Regra |
|-------|------|-------|
| UserId | UUID | Parte da PK; FK AppUser local. |
| PermissionCode | string, 1 a 128 | Parte da PK; código declarado pela aplicação. |
| GrantedAt | instante UTC | Obrigatório. |
| Version | inteiro 64 bits | Concorrência. |

Concessão explícita; ausência significa negar. Código inicial para I2:
`foundation.context.read`. Sem wildcard, grant global ou editor dinâmico.
Revogação remove a concessão no banco local e não modifica A/B simultaneamente.

### Histórico de migrations

`TenantAccessDbContext` mantém histórico no schema `foundation` do banco da instalação.
Um conjunto único de migrations de acesso é aplicado separadamente ao banco de cada
instalação de cliente (validado em I1 contra dois bancos independentes A e B);
`TenantId`/`EnvironmentId` não alteram o EF model ou snapshots.

I1 valida banco vazio, reaplicação sem duplicação/perda e falhas explícitas.
Persistência em A/B inclui operações comprovadamente sobrepostas (pico >=2, ao menos
uma por banco), identificadores/emails iguais, restrições locais e commit/rollback
independentes com credenciais runtime. A carga completa de SC-003 permanece em I2.
Atualização de versão anterior suportada ganha teste quando esse estado existir.
Não usar EnsureCreated junto a migrations nem chamar Migrate no startup da API.

## 4. Sessões — somente I2

### AuthSession

Pertence ao banco do tenant, com migration posterior separada.

| Campo | Tipo | Regra |
|-------|------|-------|
| Id | UUID | Referência opaca local de sessão. |
| UserId | UUID | FK AppUser local. |
| AccessEpoch | UUID | Igual à geração vigente do marcador. |
| ProtectedTicket | bytes opcionais | Ticket protegido; eliminado ao revogar. |
| IssuedAt / ExpiresAt | instantes UTC | Validade absoluta inicial de 30 minutos. |
| RevokedAt | instante UTC opcional | Impede uso quando preenchido. |
| Version | inteiro 64 bits | Controle de renew/logout concorrentes. |

Chave retornada por ITicketStore: TenantId/AccessEpoch/SessionId, protegida pelo
cookie handler nativo antes de ir ao cliente. Retrieve/Renew/Remove resolvem apenas
esse tenant validado; não procuram sessões em vários bancos.

Sessão exige conta habilitada, stamp atual, epoch vigente e concessões atuais.
Renew condicionado à sessão ativa, sem upsert, reinserção ou extensão da expiração.
Logout confirma revogação persistida antes da resposta; falha é erro explícito.
Modificar senha, bloquear ou revogar A não interfere na conta de mesmo email em B.

## 5. Evolução operacional posterior

OperationalRun e BackupManifest registram somente alvo, ambiente, tipo, versão,
instante, resultado seguro e referência de investigação. Cadastro de usuários
ou seus dados não migram para o catálogo por conveniência de auditoria.

| Transição | Condição |
|-----------|----------|
| Banco vazio -> estrutura inicial | Target aprovado, inativo, owner autorizado e preflight antes de DDL. |
| Marcador correto -> reaplicação | Mesma identidade/ambiente; inicialização é no-op e dados preservados. |
| Marcador ausente em estrutura existente/divergente | Bloqueio antes da migration; sem correção automática. |
| Conta habilitada -> desativada | Alteração local; I2 invalida sessões na próxima operação. |
| Sessão válida -> revogada/expirada | Acesso negado, sem fallback a outro tenant. |
| Active -> Maintenance -> Inactive | Operação futura aprovada, tráfego drenado e versão/dados verificados. |
| Restore de tenant | Reconciliar alvo, renovar AccessEpoch, revogar sessões e revisar concessões antes de ativar. |

Backup por instalação de cliente inclui acesso e dados de negócio futuros. Restore de A não
restaura contas de B. Não existe banco de catálogo central para restaurar ou reconciliar.

## 6. Contextos e validação

- **InstallationTenantOptions**: `ApplicationId`, `EnvironmentId`, `TenantId`, `TenantDisplayName`
  e estado vindos da configuração segura da instalação; confirmados contra `TenantDatabaseIdentity`.
- **AuthenticatedTenantContext**, I2: conta e sessão verificadas no banco da própria instalação,
  `TenantId` coincidente e permissões locais; scoped e imutável.
- **ModuleDefinition**, I3: contratos e dependências, sem acesso às entidades internas
  de outro módulo. Fixtures comerciais existem somente nos testes.

SC-009 é validado com dois bancos reais (simulando duas instalações de clientes), contas de email igual, hashes independentes,
consulta/alteração separadas, FK/uniqueness, commit/rollback e reaplicação sem perda.
Apontar a configuração da instalação A para o banco B ou usar a credencial de A em B deve falhar.
SC-002/SC-003 acrescentam sessão, matriz de acesso e 200 operações/20 simultâneas
quando seus incrementos forem implementados; não são testes executados neste documento.
