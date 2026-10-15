# Data Model: Fundação full-stack reutilizável

**Date**: 2026-10-13
**Sources**: [spec.md](spec.md), [plan.md](plan.md), [research.md](research.md).

Modelo de design, não entidades ou migrations implementadas. **I1** é backend e
migration de usuários/acesso; **I2** adiciona autenticação/sessões HTTP; operação
ampliada e publicação são posteriores. Não confundir tabelas previstas com código entregue.

## 1. Propriedade e limites

| Local | I1 | Evolução | Proibido |
|-------|----|----------|----------|
| Catálogo, schema `platform` | CatalogIdentity, Tenant e StorageBinding técnicos. | Estado operacional técnico. | Contas, credenciais, concessões, sessões ou dados de negócio de tenants. |
| Banco exclusivo do tenant | Marcador em `foundation`; Identity e concessões em `access`. | AuthSession I2 e schemas de módulos futuros. | Identidade global, registros de outro tenant ou dados compartilhados automaticamente. |
| Testes | Dados sintéticos e credenciais de teste descartáveis. | Fixtures opcionais I3. | Distribuição na API/imagem de produção. |
| Arquivos/configuração privados | Aliases confiáveis e referências permitidas de secrets. | Certificado/key ring e backup externo. | Secrets no catálogo, Git, bundle, imagem ou logs. |

Schema não substitui banco por tenant. Email/CPF igual em A e B não vincula registros.
O par `(TenantId, UserId)` identifica a conta entre contextos; um UUID local pode
coincidir em bancos distintos sem representar a mesma conta.

Usar UUID não vazio, instantes UTC `timestamptz`, índices/FKs e Version explícito
onde há concorrência. Identity/EF pertencem a Infrastructure; Domain e Application
usam conceitos/portas próprias, sem herdar classes de persistência.

## 2. Catálogo técnico — I1

### CatalogIdentity

| Campo | Tipo | Regra |
|-------|------|-------|
| SingletonId | inteiro | 1; uma linha. |
| ApplicationId | string, 1 a 100 | Identidade confiável do produto. |
| EnvironmentId | UUID | Ambiente imutável; não vazio. |
| CreatedAt | instante UTC | Obrigatório. |

Verificar contra configuração antes de resolver bancos. Inicialização explícita
por tooling em banco novo aprovado; não criar/corrigir automaticamente no startup.

### Tenant

| Campo | Tipo | Regra |
|-------|------|-------|
| Id | UUID | PK imutável. |
| DisplayName | string, 1 a 120 | Nome de apresentação, não conexão. |
| State | enum | I1: Inactive ou Active. Operação futura acrescenta Maintenance. |
| CreatedAt / UpdatedAt | instantes UTC | Obrigatórios. |
| Version | inteiro 64 bits | Concorrência; incrementa ao alterar. |

Instalação de teste registra alvos Inactive para migrations. Active só atende
operações de uso após identidade, versão e configuração confirmadas.
I1 não cria workflow de contratação, cobrança, provisioning ou painel.

### TenantStorageBinding

| Campo | Tipo | Regra |
|-------|------|-------|
| TenantId | UUID | PK/FK Tenant; relacionamento 1:1. |
| ServerAlias | string, até 64 | Alias existente em configuração confiável. |
| DatabaseName | string, até 63 | Nome aprovado; instalação usa `tenant_` + UUID sem hífens. |
| RuntimeSecretRef | string, até 128 | Referência opaca do papel runtime, não senha/conexão. |
| MigrationSecretRef | string, até 128 | Referência do owner, acessível somente ao tooling. |
| Version | inteiro 64 bits | Concorrência do binding. |

Índice único `(ServerAlias, DatabaseName)` impede dois tenants no mesmo alvo.
Referências aceitam alfanuméricos, `_` e `-`; não são URI, path, `..` ou conexão.
O mapeamento privado resolve referências para arquivos confiáveis.
Runtime não recebe o arquivo de migration; não abrir path vindo do cliente ou catálogo.

Registro técnico inicial vem de configuração de instalação restrita, não endpoint
anônimo. Reaplicação verifica registros existentes, sem upsert que troque seus bancos
ou reative tenant. Alterar binding demanda target inativo e procedimento autorizado.

## 3. Banco do tenant — I1

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
explicitamente pode receber migration e inicialização. Estrutura existente sem
marcador é erro. UseSeeding/UseAsyncSeeding inserem o marcador desse alvo uma vez,
sem HasData dependente do tenant nem seed de usuário.

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

`CatalogDbContext` mantém histórico no schema `platform`;
`TenantAccessDbContext` mantém histórico no schema `foundation`.
Um conjunto único de migrations de acesso é aplicado separadamente a A e B;
TenantId/EnvironmentId não alteram EF model ou snapshots.

I1 valida banco vazio, reaplicação sem duplicação/perda e falhas explícitas.
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

Backup por tenant inclui acesso e dados de negócio futuros. Restore de A não
restaura contas de B. Restore de catálogo exige revisar bindings/estados técnicos,
não recuperar sessões globais inexistentes. Nenhuma transação distribuída é presumida.

## 6. Contextos e validação

- **ResolvedTenantTarget**: TenantId/EnvironmentId/binding vindos de catálogo
  confiável; não é identidade autenticada nem autorização de negócio.
- **AuthenticatedTenantContext**, I2: conta e sessão verificadas no banco de origem,
  tenant coincidente e permissões locais; scoped e imutável.
- **ModuleDefinition**, I3: contratos e dependências, sem acesso às entidades internas
  de outro módulo. Fixtures comerciais existem somente nos testes.

SC-009 é validado com dois bancos reais, contas de email igual, hashes independentes,
consulta/alteração separadas, FK/uniqueness, commit/rollback e reaplicação sem perda.
Reapontar binding de A para banco B ou usar role de A em B deve falhar.
SC-002/SC-003 acrescentam sessão, matriz de acesso e 200 operações/20 simultâneas
quando seus incrementos forem implementados; não são testes executados neste documento.
