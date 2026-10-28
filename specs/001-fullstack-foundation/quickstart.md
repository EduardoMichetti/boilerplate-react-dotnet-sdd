# Quickstart: backend e migration de usuários/acesso — I1

**Date**: 2026-10-13
**References**: [plano](plan.md), [pesquisa](research.md), [modelo](data-model.md),
[contrato I1](contracts/openapi.json) e [operação](contracts/operations.md).

Roteiro para executar **depois da implementação de I1**. Projetos, migrations,
manifest de ferramentas, templates e serviços Compose citados ainda não foram
criados por este planejamento. Não executar como se o backend já estivesse entregue.
O roteiro não depende de frontend, login HTTP, painel ou Platform.Tools.

## 1. Pré-requisitos e alvos

- Git, Docker com engine Linux e Compose v2.
- SDK 10.0.401, ferramenta local dotnet-ef 10.0.12 e lockfiles entregues.
  O profile `migrations` usará imagem SDK com ferramentas/pacotes restaurados no build.
- PostgreSQL 18.6 com volume nomeado em `/var/lib/postgresql`.
- Banco vazio da instalação local (e segundo banco de validação para comprovar isolamento
  SC-009), com owners de migration e runtime mínimo distintos. Não existe banco `platform_catalog`.
- Configurações privadas aprovadas por instalação (`.env.local` para A e `.env.b.local`
  para B) contêm `ApplicationId`, `EnvironmentId`, `TenantId` e conexões/secrets próprios.
  Somente o tooling recebe `Migrations__AllowInitializeEmptyDatabase=true` para a
  primeira inicialização do banco vazio aprovado; ausente/`false` nega essa inicialização
  e valor inválido falha explicitamente.
- Nenhuma credencial real/padrão em exemplos, migrations ou logs.

A preparação inicial de bancos/papéis é pré-requisito do operador, documentado na
implementação. Testes fazem isso com Testcontainers, não com banco/usuário de produção.
Não é necessário criar uma suíte de cadastro/provisionamento comercial.

## 2. Configuração e build

PowerShell na raiz do repositório:

```powershell
Copy-Item .\infra\.env.example .\infra\.env.local
Copy-Item .\infra\.env.example .\infra\.env.b.local
```

Preencher cada arquivo privado com as variáveis e arquivos de credenciais da respectiva
instalação, usando os identificadores abaixo e bancos/roles próprios. Definir
`TenantState=Inactive` durante DDL e aprovar explicitamente cada banco vazio somente no
tooling. O exemplo versionado mantém `Migrations__AllowInitializeEmptyDatabase=false`;
exemplos não contêm valores de senha utilizáveis. A API monta somente o secret runtime
da própria instalação. A credencial owner de migration é montada apenas no profile
`migrations`, assim como o sinal de inicialização. Arquivos privados e key rings ficam
fora do Git/contexto de build.

```powershell
$compose = '.\infra\compose.yaml'
$localA = '.\infra\.env.local'
$localB = '.\infra\.env.b.local'
$environmentA = '11111111-1111-4111-8111-111111111111'
$environmentB = '22222222-2222-4222-8222-222222222222'
$tenantA = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
$tenantB = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
docker compose --project-name boilerplate-a -f $compose --env-file $localA config --quiet
docker compose --project-name boilerplate-b -f $compose --env-file $localB config --quiet
docker compose --project-name boilerplate-a -f $compose --env-file $localA build api migrations
docker compose --project-name boilerplate-b -f $compose --env-file $localB build migrations
docker compose --project-name boilerplate-a -f $compose --env-file $localA up -d postgres
docker compose --project-name boilerplate-b -f $compose --env-file $localB up -d postgres
```

**Esperado**: configuração e imagens válidas, PostgreSQL saudável, targets vazios
aprovados, volumes/redes independentes por projeto e roles runtime sem DDL/CONNECT a
outros tenants. PostgreSQL não publica porta no host; somente a API A será exposta neste
roteiro. Não imprimir o resultado completo de `compose config` com secrets ou usar
`down -v` como rotina. Interromper o roteiro se qualquer comando falhar.

## 3. Aplicar migrations entregues

Migrations iniciais versionadas são geradas na implementação e revisadas; o usuário
do boilerplate aplica essas versões, não gera uma migration nova a cada instalação.
Serviço pontual `migrations` tem working directory na pasta `backend/src` da imagem
Linux e EF local restaurado; nomes de pastas abaixo não dependem do separador do host.

```powershell
docker compose --project-name boilerplate-a -f $compose --env-file $localA --profile migrations run --rm migrations dotnet ef database update --project Platform.Infrastructure --startup-project App.Api --context TenantAccessDbContext -- --tenant-id $tenantA --environment-id $environmentA
docker compose --project-name boilerplate-b -f $compose --env-file $localB --profile migrations run --rm migrations dotnet ef database update --project Platform.Infrastructure --startup-project App.Api --context TenantAccessDbContext -- --tenant-id $tenantB --environment-id $environmentB
```

Cada comando usa a conexão e o owner do arquivo correspondente. Os argumentos de
identidade apenas confirmam o alvo: não escolhem outro banco. Após inicializar, remover
`Migrations__AllowInitializeEmptyDatabase` ou voltar a `false` em ambos os arquivos;
marcador correto permite reaplicação sem essa autorização.

Para executar EF nativamente no Windows, usar `Push-Location .\backend\src`, os
mesmos nomes de projetos e configuração privada de tooling, retornando depois
com `Pop-Location`. Não passar paths Windows para o processo Linux do container.

**Esperado**:

- Não existe banco `platform_catalog` nem `CatalogDbContext`.
- Cada banco possui marcador do próprio cliente/ambiente (`TenantDatabaseIdentity`) e tabelas Identity/concessões.
- UseSeeding/UseAsyncSeeding não criam usuário, senha ou permissões padrão.
- Não há migration automática no startup da API, mesmo se alguma estiver pendente.
- Target ausente, UUID/ambiente divergente, banco existente sem marcador ou role
  insuficiente falha explicitamente, sem fallback ou correção silenciosa.

## 4. Iniciar o backend e confirmar superfície mínima

Concluídas as migrations, definir `TenantState=Active` no arquivo privado de A.
A API não recebe a credencial owner nem o sinal de inicialização.

```powershell
docker compose --project-name boilerplate-a -f $compose --env-file $localA up -d api
Invoke-RestMethod 'http://localhost:8080/health/live'
Invoke-RestMethod 'http://localhost:8080/health/ready'
Invoke-RestMethod 'http://localhost:8080/openapi/v1.json'
```

**Esperado**: status `alive`, `ready` e contrato com somente as duas operações I1.
Rotas de login, usuários, `/me`, `/me/tenants` e `/context` retornam 404; não há
painel ou página web. Falha de configuração ou divergência do marcador do banco da
instalação retorna 503 seguro em readiness, não em liveness.

## 5. Executar testes e gates de I1

Os testes de integração preparam seus próprios targets e usuários sintéticos,
com Identity/UserManager para hashing e validação de senhas. Não usar SQL com senha
em texto puro ou migration de contas padrão para facilitar demonstração.

```powershell
dotnet tool restore
dotnet restore .\backend\Boilerplate.slnx --locked-mode
dotnet build .\backend\Boilerplate.slnx --no-restore
dotnet format .\backend\Boilerplate.slnx --verify-no-changes --no-restore
dotnet test --solution .\backend\Boilerplate.slnx --no-restore --minimum-expected-tests 1
dotnet package list .\backend\Boilerplate.slnx --vulnerable --include-transitive --format json
gitleaks git --redact --exit-code 1 .
```

MTP usa `--solution`/`--project`, sem adaptador VSTest. Scans usam versões fixadas,
checksum e histórico completo; auditoria NuGet requer interpretação de severidade,
pois somente listar achados não bloqueia por si só. Falha/ausência de resultado não aprova.

| Prova de I1 | Resultado exigido |
|------------|-------------------|
| Migration inicial | Estrutura de acesso criada nos dois bancos reais. |
| Reaplicação | Mesma versão aplicada novamente, sem duplicação nem perda de registros. |
| Conta local | Mesmo email em A/B, contas e hashes independentes; unicidade dentro de cada banco. |
| Persistência | Consulta e alteração corretas, FK/grants, commit e rollback, sem mocks substituindo PostgreSQL. |
| Concorrência mínima | Barreira comprova operação de A sobreposta a operação de B (pico >=2), com IDs/emails iguais e credenciais runtime próprias; resultados válidos persistem somente no alvo e falha/rollback em A não altera B. Não exige a carga completa de SC-003. |
| Inicialização autorizada | Sem marcador, sinal ausente/`false` nega antes de DDL; valor inválido falha; `true` só inicializa banco vazio aprovado. Estrutura sem marcador ou identidade divergente bloqueia mesmo com aprovação; reaplicação correta funciona com sinal desligado. |
| Target errado | A apontando a B é bloqueado antes de DDL/acesso a contas; B não sofre mudança. |
| Role runtime | Credencial de A não conecta a B nem executa DDL. |
| API mínima | Contrato I1 corresponde à API; rotas futuras ausentes, sem sucesso simulado. |
| Saúde | Liveness independente de catálogo/cookies; readiness não derruba todos por falha de um tenant. |
| Arquitetura | Domain sem HTTP/EF/Identity; referências sem ciclos e fixtures não distribuídas. |
| Secrets | Nenhuma senha/ticket/conexão em log, imagem, Git ou resposta. |

Registrar IDs/quantidade de testes executados; uma execução com zero testes falha.
Checks pertinentes devem bloquear integração em GitHub Actions com permissões
mínimas e sem secrets de produção. Não exige pipeline de frontend ou deploy.

### Prova negativa dos gates — SC-006 no backend

Após uma execução válida, usar alterações controladas em branch descartável para
provocar, separadamente, falha de compilação/tipos C#, formatação e testes de
persistência/isolamento. Exercitar o secret scan com credencial fictícia reconhecível
pelo scanner e a política de auditoria com relatório NuGet sintético de severidade
alta/crítica; não adicionar dependência vulnerável nem secret real.
Também comprovar falha explícita para scan indisponível e execução com zero testes.

Registrar o check afetado, evidência de falha e bloqueio de integração pelos checks
obrigatórios de branch protection/ruleset. A aplicação dessa proteção exige permissão
do mantenedor; sua ausência não pode ser registrada como bloqueio comprovado.
Descartar somente as alterações do exercício e confirmar novamente a execução válida,
sem desativar checks ou manter fixtures negativas no código distribuído.

## 6. Aceite SC-009

1. Comprovar estrutura e persistência separadas em A/B.
2. Para reaplicar, parar a API A com `docker compose --project-name boilerplate-a -f $compose --env-file $localA stop api` e definir `TenantState=Inactive` no alvo; repetir os comandos da seção 3 com o sinal de inicialização desligado e confirmar dados/quantidades preservados. Reativar A e repetir a seção 4.
3. Demonstrar backend vivo/pronto sem frontend, painel ou módulo de negócio.
4. Registrar resultados dos testes e ausência de credenciais padrão.

I1 só está concluído após essa prova, não por existir um projeto ou uma migration.

## 7. Validação posterior, fora do aceite I1

- **I2**: seguir [contrato futuro](contracts/future-openapi.json); login requer
  tenant, cookies e CSRF. Usar contas separadas, mesmo email e senhas diferentes.
  Sessão A com header B falha sem tocar B; logout/revogação/expiração/stamp/epoch
  invalidam acesso. Não há `/me/tenants` nem identidade global.
- **I2**: SC-002/SC-005 e isolamento runtime SC-003, com 200 operações, 100 por tenant
  e pico observado >=20, incluindo resultados válidos e efeitos corretos.
- **I3**: interface, SC-001 e módulos sintéticos opcionais SC-004, sem domínio comercial.
- **I4**: Dokploy, persistência pós-restart, backup externo de catálogo e tenants;
  restauração local renova epoch/revoga sessões/revisa concessões sem alterar outro tenant.
  SC-007 e metas RPO/RTO/retenção/responsáveis antes de produção.
- **Transversal**: SC-006/SC-008 acompanham as superfícies entregues; não esperar
  o frontend para conferir documentação, compilação, testes e scans do backend.

Este guia não afirma execução de aplicação, testes, migration, container ou deploy
durante o planejamento.
