# Contracts: migration inicial e acesso futuro

**Date**: 2026-10-13
**Status**: design, não interfaces implementadas.
**References**: [API I1](openapi.json), [API futura I2](future-openapi.json),
[modelo](../data-model.md), [plano](../plan.md) e [quickstart I1](../quickstart.md).

## 1. Fronteira do primeiro incremento

I1 entrega backend e migrations de estrutura de usuários/acesso. HTTP expõe apenas
health checks e o contrato I1; `/api/v1/auth/*`, `/me`, `/context` e cadastro de usuários
não são mapeados. Rotas ausentes retornam 404 Problem Details, nunca simulam sucesso.
`/openapi/v1.json` publica somente as operações efetivamente entregues.

Não há Platform.Tools obrigatório, painel, autocadastro, cobrança ou fluxo comercial.
Não interpretar os contratos futuros como tarefas necessárias ao aceite SC-009.

## 2. Contrato EF CLI — I1

Ferramenta local `dotnet-ef` 10.0.12, contexto/factory explícitos e migrations
versionadas. Factories ficam em Infrastructure; API é startup project técnico,
sem iniciar web host nem aplicar DDL no startup.

| Contexto | Argumentos extras após `--` | Target e finalidade |
|----------|----------------------------|---------------------|
| `TenantAccessDbContext` | `--tenant-id UUID --environment-id UUID` | Banco exclusivo configurado para a instalação daquele cliente/ambiente (sem `CatalogDbContext`). |

Gerar migrations de acesso em `TenantAccessDbContext`. Aplicar via EF CLI no profile
Compose `migrations`, com credenciais owner daquele banco, não com o role usado pela API.
Nenhum argumento aceita senha, conexão livre, path enviado por HTTP ou tenant padrão.
Os argumentos de identidade devem coincidir com a configuração privada; não selecionam
conexão nem banco. A/B exigem configurações e credenciais próprias, nunca a mesma conexão
com apenas `--tenant-id` trocado.

### Preflight e inicialização

1. Validar configuração de tooling (`ApplicationId`, `EnvironmentId`, `TenantId`) e conexão do banco-alvo.
2. Exigir janela de manutenção/instância inativa para aplicar DDL.
3. Conectar com owner do alvo, verificar banco/role e marcador (`TenantDatabaseIdentity`) **antes** de DDL.
4. Marcador errado, ambiente divergente ou estrutura existente sem marcador:
   falhar sem aplicar migration, corrigir identidade ou selecionar outro banco.
5. Inicialização somente com banco vazio conhecido e aprovação explícita por
   `Migrations:AllowInitializeEmptyDatabase=true` na configuração privada de tooling;
   nunca adotar banco arbitrário existente.
6. Aplicar migration com lock nativo EF em janela sem writers/DDL concorrentes.
7. UseSeeding/UseAsyncSeeding inicializam marcador técnico apenas para alvo novo
   aprovado; marcador correto existente é no-op. Não criar contas ou senhas.

O callback ocorre depois da migration; preflight é obrigatório para não alterar
estrutura errada antes de detectar divergência. EF lock não autentica operador
nem torna atômicas alterações concorrentes feitas fora desse processo.

`Migrations__AllowInitializeEmptyDatabase` é a variável de ambiente correspondente.
Ausência ou `false` nega inicialização sem marcador antes de DDL; valor inválido falha
explicitamente. `true` só vale para a identidade e o banco aprovados nessa configuração,
com argumentos correspondentes e todas as verificações de preflight satisfeitas.
Marcador divergente ou estrutura existente sem marcador sempre bloqueia, mesmo com
`true`. Marcador correto permite aplicar/reaplicar sem autorização de inicialização.
Após inicializar, remover o sinal ou voltar a `false`; ele nunca é montado na API.
Testes devem cobrir ausência, `false`, valor inválido, aprovação válida, alvo divergente
e reaplicação com marcador correto e sinal desligado.

Migrations não criam bancos/papéis automaticamente: o operador fornece esses
pré-requisitos em procedimento documentado. Testcontainers faz isso em testes,
sem exigir suíte de provisionamento de produção.

EF CLI reporta sucesso com exit code 0; qualquer preflight, migration ou seeding
falho retorna não zero, diagnóstico seguro e alvo/correlação sem secrets.
Não fabricar relatório JSON ou exit codes de uma CLI administrativa que não existe.

### Privilégios

| Identidade | Permissão | Onde é usada |
|------------|-----------|--------------|
| Runtime da instalação | CONNECT ao próprio banco; SELECT marcador/histórico; DML de acesso local conforme necessidade. | API da instalação e testes runtime; autenticação HTTP só I2. |
| Owner de migration | DDL no banco-alvo da instalação, com grants controlados. | Somente processo EF tooling (`migrations`). |
| Instalador | Criação restrita de bancos/papéis. | Pré-requisito local/instalação, nunca API. |

Revogar CONNECT de PUBLIC nos bancos da aplicação e conceder somente aos roles
correspondentes; remover criação pública de schema. Runtime não é superuser,
owner, CREATEDB ou CREATEROLE e não recebe CONNECT a outros bancos.
Owner/provisionador não é montado na API nem reaproveitado em seus testes runtime.

## 3. Configuração

| Configuração prevista | Uso e proteção |
|-----------------------|----------------|
| `ApplicationId` / `EnvironmentId` | Identidade confiável do produto e ambiente da instalação. |
| `TenantId` / `TenantDisplayName` / `TenantState` | Identidade e estado da empresa atendida por aquela instalação (via variáveis de ambiente no Dokploy/Compose). |
| `ConnectionStrings:AppDb` / arquivo de credencial runtime | Conexão runtime exclusiva ao banco da instalação (sem DDL); secret privado no Dokploy/ambiente. |
| Credencial de migration (tooling) | Credencial owner do banco da instalação; montada somente no profile `migrations`. |
| `Migrations:AllowInitializeEmptyDatabase` | Booleano privado exclusivo do tooling; ausente equivale a `false`. Autoriza apenas inicialização vazia aprovada, sem dispensar validação do alvo. |
| `KeyRingPath` / `ProtectionCertificateFile` | Apenas I2; key ring persistente e proteção em repouso em produção. |

Conexões são validadas com parsing seguro; o servidor nunca aceita string de conexão
ou path vindo do consumidor HTTP. Todos os arquivos privados e segredos ficam fora do
Git e do contexto de build; no Dokploy ficam nas variáveis/secrets da aplicação daquele
cliente na VPS (ou em cofre externo); `.env.example` só indica parâmetros fictícios.

## 4. Contrato de autenticação — somente I2

1. Obter antiforgery com header do tenant, preservando cookie e requestToken.
2. Login com `X-Tenant-Id`, email/senha e CSRF; validar alvo antes de consultar
   somente suas credenciais. Tenant desconhecido/inativo e login inválido produzem
   AUTH_INVALID genérico, sem consultar ou listar contas de outro banco.
3. Sessão emitida é tenant-local; obter novo CSRF após login.
4. `/me` e `/context` exigem header coincidente com o tenant protegido no cookie.
   Cookie inválido não autoriza lookup; header divergente não seleciona outro banco.
5. Logout revoga somente a sessão de origem antes da resposta; replay permanece negado.

Chave ITicketStore contém TenantId/AccessEpoch/SessionId dentro da proteção nativa.
Store não mantém DbContext/tenant mutável global, resolve apenas o alvo protegido
e cria scope por chamada. Não pesquisar email/sessionId em todos os bancos.

SameSite não substitui CSRF. Cookie HttpOnly/Secure/Path `/`/sem Domain em produção,
30 minutos absolutos, sem token em localStorage, JWT próprio ou refresh.
Todas as respostas de conta/contexto e erros usam `Cache-Control: no-store`.
Renew não faz upsert, reativa sessão ou altera ExpiresAt. Logout 503 não confirma saída.

Email igual em A/B significa duas contas. Para usar B, autenticar a conta de B,
cancelar operações/desconsiderar respostas antigas de A e estabelecer novo contexto.
Retirar `/api/v1/me/tenants`; essa rota retorna 404, não uma lista vazia fingida.

| Status I2 | Significado |
|-----------|-------------|
| 400 | Corpo/header/CSRF inválido. |
| 401 | Credencial/tenant de login inválidos genericamente ou sessão inválida. |
| 403 | Header divergente ou concessão local ausente; sem lookup em outro tenant. |
| 409 | Tenant de origem de sessão válida está inativo. |
| 429 | Rate limit de login, com Retry-After. |
| 503 | Dependência indisponível, sem conexão exposta ou sucesso aparente. |
| 500 | Falha inesperada segura com code/traceId. |

JSON camelCase, UUID não vazio e datas UTC. Erros seguem Problem Details, inclusive
binding/JSON/CSRF. Health é isolado de lookup de sessão e catálogo em liveness;
AllowAnonymous sozinho não é evidência desse isolamento.

## 5. Administração e recuperação — posteriores

FR-033 é procedimento técnico restrito por tenant, não requisito de painel ou
suíte de comandos em I1. Criação/reset usam UserManager e entrada de senha protegida;
desativação/revogação alteram somente a conta/concessão local e invalidam sessões.
Não manter usuário global ou gestão de várias empresas pela mesma identidade.

FR-029 a FR-032 são gates antes de produção: backup externo por catálogo/tenant,
manifesto de alvo/ambiente/versão/checksum, restauração validada sem tráfego e
recuperação que trate aplicação e dados. Não executar rollback destrutivo automático.
Dump/restore inclui contas e permissões do próprio tenant; sessão antiga não volta
a ser utilizável após restauração: revogar registros e renovar AccessEpoch.

Catálogo não contém identidades de acesso. Sua restauração exige reconciliar
bindings e estados com os bancos, não restaurar contas globais ou varrer sessões.
Backup externo criptografado, RPO/RTO, retenção, responsáveis e testes de restauração
são definidos antes de produção. `pg_restore` usa no-owner/no-acl/exit-on-error,
com reaplicação dos grants mínimos do alvo.

## 6. Evidências por incremento

- I1: dois bancos reais, email igual em contas independentes, unicidade local,
  FK de TenantId, commit/rollback, reaplicação e role de A sem acesso ao banco B.
- I1: marcador errado bloqueia **antes** de DDL; migration sem seeding de usuários;
  readiness sem dependência de todos os tenants; API não oferece login/cadastro.
- I2: header divergente não toca B; replay pós-logout, expiração/stamp/epoch, CSRF,
  bloqueio/reset/revogação em A sem efeito em B; contrato futuro sem enumeração.
- I3/I4: módulos opcionais de teste, frontend, publicação e restauração independentes.

Contratos/documentação não afirmam que essas evidências já foram executadas.
