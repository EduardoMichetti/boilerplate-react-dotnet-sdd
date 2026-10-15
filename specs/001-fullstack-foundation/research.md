# Research: Fundação full-stack reutilizável

**Date**: 2026-10-13
**Project**: `boilerplate-react-dotnet-sdd`
**Context**: [spec.md](spec.md), [plan.md](plan.md) e
[Constituição](../../.specify/memory/constitution.md).

Esta pesquisa registra decisões do desenho, justificativas e alternativas.
Não implementa bibliotecas, serviços ou infraestrutura. Autenticação por cookie
e indicação de tenant por header foram confirmadas pelo usuário.
Revisão atual: I1 é somente backend e migration de usuários/acesso. Autenticação HTTP,
frontend e operação ampliada são posteriores. A matriz de versões anterior é preservada;
esta revisão não é uma atualização de dependências.

## D01 — Matriz de versões e compatibilidade

**Decision**: adotar a matriz estável abaixo, verificada em registros públicos dos
publicadores com corte em 2026-10-12 às 15:40 UTC. Os pins representam a base inicial,
não uma autorização para deixar de aplicar atualizações de segurança.

| Componente | Versão escolhida | Restrição de compatibilidade |
|------------|------------------|-----------------------------|
| .NET SDK / C# | 10.0.401 / C# 14 | Target `net10.0`; feature band SDK 10.0.4xx. |
| .NET e ASP.NET Core | 10.0.12 LTS | Suporte .NET 10 até 2028-11-14. |
| EF Core | 10.0.12 | Usar a mesma linha em runtime, Design e ferramentas EF. |
| Npgsql EF provider / driver | 10.0.3 / 10.0.3 | Provider exige EF >=10.0.4 e <11; target net10.0. |
| PostgreSQL | 18.6 | Major 18 suportado até 2030-11-14. |
| Node.js | 24.21.0 LTS | Satisfaz Vite, Vitest e jsdom; Node 26 ainda não é LTS no corte. |
| React / React DOM | 19.3.0 / 19.3.0 | Manter versões correspondentes. |
| TypeScript | 7.0.2 | Release estável publicada, não versão preview. |
| Vite / plugin React | 8.3.4 / 6.1.2 | Node >=22.12 ou ramo 20 compatível; plugin requer Vite 8. |
| Tailwind / plugin Vite | 4.3.3 / 4.3.3 | Plugin aceita Vite 8. |
| React Router DOM | 7.18.4 | URLs, deep links e back/forward nas três telas. |
| xUnit | `xunit.v3` 4.0.2 | Produto xUnit v3; integração MTP v2, não VSTest. |
| Testcontainers PostgreSQL | 4.16.0 | PostgreSQL real 18.6 em container dedicado. |
| Vitest | 5.0.4 | Node 24 e Vite 8 compatíveis; publicado antes do corte. |
| Testing Library React / DOM | 16.3.3 / 10.4.2 | React 19; integração DOM 10. |
| jsdom | 30.1.2 | Node >=24.15 no ramo 24. |
| Playwright | 1.64.0 | Package e binários de browser da mesma versão. |

Fixar SDK e runner MTP em `global.json`, versões NuGet diretas e transitivas em
manifests/lockfiles e dependências npm sem ranges nos manifests, com `package-lock.json`.
Tipagens React 19 compatíveis devem receber pin exato no lockfile de implementação;
a matriz não finge que uma seleção de família já é um lockfile completo.

Usar referências de imagens SDK 10.0.401, ASP.NET 10.0.12, Node 24.21.0 e PostgreSQL
18.6; verificar e registrar digests da plataforma Linux no lock de imagens antes do
primeiro build reproduzível. Nenhum manifesto ou imagem é gerado ou baixado neste plano.

O runner escolhido é `Microsoft.Testing.Platform`, registrado no `global.json`;
não misturar projetos VSTest, adaptador Visual Studio ou argumentos legados de runner.

**Rationale**: a stack é obrigatória; versões flutuantes e combinações incompatíveis
violam reprodutibilidade. Nenhuma versão preview será usada.

**Alternatives considered**: acompanhar `latest` a cada build; misturar majors de
EF Core e Npgsql; fixar versão sem evidência pública de disponibilidade. Rejeitadas
por incompatibilidade, falta de suporte ou ausência de prova.

**Sources**:
[.NET release metadata](https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json),
[.NET support](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core),
[EF releases](https://learn.microsoft.com/en-us/ef/core/what-is-new/),
[Npgsql provider record](https://api.nuget.org/v3/registration5-gz-semver2/npgsql.entityframeworkcore.postgresql/10.0.3.json),
[PostgreSQL support](https://www.postgresql.org/support/versioning/),
[Node 24.21.0](https://nodejs.org/en/blog/release/v24.21.0),
[npm publisher registry](https://registry.npmjs.org/),
[NuGet publisher catalog](https://api.nuget.org/v3/index.json),
[Tailwind compatibility](https://tailwindcss.com/docs/compatibility),
[xUnit runners](https://xunit.net/docs/getting-started/v3/getting-started),
[dotnet test runners](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test),
[Testcontainers PostgreSQL](https://dotnet.testcontainers.org/modules/postgres/).

Para npm, a evidência exata é o manifesto `registry.npmjs.org/{package}/{version}`
e o campo de data `time[version]` no registro do pacote. Para NuGet, usar o registro
da versão e `catalogEntry.published`, não extrapolar disponibilidade pelo calendário.

## D02 — Monólito modular e projetos por camada

**Decision**: Domain, Application, Infrastructure e API em projetos separados.
I1 aplica migrations com EF CLI e factories nativas; não exige projeto Tools.
Nenhum módulo de negócio, mesmo de referência, é entregue.

**Rationale**: referências de projeto permitem testar limites. O processo EF usa
credenciais de migration separadas da API, sem construir uma suíte administrativa.
API e frontend posterior compartilham uma implantação, não arquitetura distribuída.

**Alternatives considered**: microsserviços, todos os arquivos em um único projeto,
infraestrutura compartilhada sem limites e scaffold completo de módulos comerciais.
Rejeitadas por violar o escopo ou dificultar verificação das dependências.

## D03 — Identidade tenant-local e sessão futura revogável

**Decision**: Identity no banco de cada tenant. I1 usa
`IdentityUserContext<AppUser, Guid>` para contas e tabelas nativas, mais concessões
diretas por usuário; não precisa de papéis/grupos nem conta global com múltiplos vínculos.
Senha é validada e hasheada por Identity, nunca por implementação criptográfica própria.

I2 acrescenta AuthSession no mesmo banco, com cookie e `ITicketStore`.
StoreAsync devolve referência tenant/epoch/session; o handler nativo protege essa
referência no cookie e a desprotege antes de RetrieveAsync. Só então o adapter
resolve o target pelo tenant protegido, nunca pelo header não confiável.
O ticket persistido também é protegido por Data Protection.

Verificar sessão, validade do usuário e security stamp em cada operação protegida;
configurar validação de stamp sem intervalo de tolerância. Pertencimento e concessões
são consultados no banco dessa conta, não copiados como autorização permanente.
Logout revoga a sessão antes de responder e elimina o cookie. Validade absoluta
inicial de 30 minutos, sem renovação deslizante, remember-me ou refresh token.
Renovação técnica do ticket não estende a expiração do registro nem recria sessão
revogada, inclusive quando concorre com logout.

**Rationale**: um cookie assinado sem estado de servidor não resolve logout de uma
cópia já emitida. Validação periódica padrão pode manter acesso após revogação.
Conta global não atende a credenciais e ciclo de acesso independentes por tenant.
A especificação exige negar novas operações imediatamente após o commit da revogação.
Operações já iniciadas continuam no próprio contexto; não passam a outro tenant.

**Alternatives considered**: JWT autossuficiente com revogação apenas por expiração,
cookie sem store, token em localStorage, Redis e provedor OIDC externo. Rejeitadas
por atraso de revogação, exposição no navegador, serviço adicional ou escopo não aprovado.

**Sources**:
[cookie validation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0),
[ITicketStore](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.cookies.iticketstore?view=aspnetcore-10.0),
[cookie handler](https://source.dot.net/Microsoft.AspNetCore.Authentication.Cookies/CookieAuthenticationHandler.cs.html).

O source browser confirma a sequência do handler, mas seu snapshot não é um SHA
de release .NET 10. A API de versão 10 e o comportamento devem ser exercitados
pelos testes de I2. Adapter é stateless e cria scope por lookup; não captura
DbContext scoped nas cookie options nem depende da autenticação já concluída.

## D04 — Mesma origem, antiforgery e integração independente

**Decision**: somente em I2/I3, servir o build web e `/api/v1` pela API em produção.
I1 não possui tela ou endpoint de login. Vite posterior usa proxy
local no desenvolvimento. Cookies de produção são `HttpOnly`, `Secure`,
`SameSite=Lax`, Path `/` e sem Domain, com prefixo `__Host-`.

Validar antiforgery nativo explicitamente em operações JSON que alteram estado,
incluindo login e logout. Emitir token por endpoint de obtenção e recebê-lo no
header `X-CSRF-TOKEN`. Obter novo token após mudança de identidade. Não considerar
SameSite, CORS ou tratamento automático de formulários suficientes para esse contrato.

Cookies de Development em HTTP localhost não usam o prefixo `__Host-` nem `Secure`.
Esse modo deve falhar fora de Development. Consumidores independentes podem usar
cookie jar e o mesmo fluxo CSRF; emissão OAuth ou cliente mobile não é implementada.

**Rationale**: a opção confirmada é cookie. Proteção CSRF também é necessária para
JSON e login; manter mesma origem simplifica configuração sem ampliar superfícies.
Não aplicar orientações restritas a monikers ASP.NET Core 11/12 ao runtime 10.

**Alternatives considered**: frontend e API em origens distintas, CORS irrestrito,
cookie sem CSRF ou proteção baseada só no formato JSON. Rejeitadas por risco e
complexidade não necessária.

**Sources**:
[antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0),
[cookie API behavior](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0).

## D05 — Catálogo e banco por tenant

**Decision**: um banco `platform_catalog` por ambiente para dados de controle;
um banco exclusivo por tenant para sua persistência. O catálogo contém somente
Tenant, binding e identidade técnica de produto/ambiente; não contém contas,
credenciais, concessões ou sessões dos usuários. Contas e permissões são I1;
sessões tenant-locais são I2.

Cada banco de tenant contém marcador de `TenantId` e `EnvironmentId`, além do histórico
de migrations da fundação. A conexão só é liberada após correspondência do marcador
e compatibilidade da versão. Dados sintéticos de negócio existem somente em testes.

**Rationale**: o catálogo resolve a empresa e mantém referências seguras. Banco por
tenant é exigência constitucional. Conferir o marcador também evita que uma referência
operacional equivocada conecte o tenant A ao banco B.
Email/CPF igual em tenants diferentes não causa deduplicação ou concessão automática.

**Alternatives considered**: coluna de discriminação em banco compartilhado, schema
por tenant ou confiar somente no nome do secret. Rejeitadas pela Constituição ou
por não detectar conexão dirigida ao banco errado.

**Source**:
[EF Core multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy).

## D06 — Header, contexto imutável e recursos scoped

**Decision**: I1 tooling exige TenantId/EnvironmentId explícitos e target confiável.
Em I2, exigir um único `X-Tenant-Id` UUID no login e nos endpoints autenticados.
No login, resolver catálogo, estado e identidade do banco antes da verificação
limitada às credenciais desse tenant. Em operação autenticada, o tenant da referência
de sessão protegida prevalece; header divergente é negado sem lookup em outro banco.
O contexto autorizado só existe após validar usuário, sessão e permissão.

DbContexts e fábricas com configuração variável são scoped; sem DbContext pooling.
Npgsql pode reutilizar conexões apenas dentro do pool da mesma configuração exata.
Nenhum singleton armazena conexão ou tenant atual. Troca de tenant exige novo login
da conta daquele banco, com cancelamento de requisições e descarte do contexto anterior.
Não existe listagem global de tenants por conta nem busca de email em múltiplos bancos.

**Rationale**: o header é uma indicação, não autorização. Lifetime incorreto pode
reutilizar configuração de outro tenant. A fundação prioriza isolamento verificável
antes de otimizações de pooling de contextos.

**Alternatives considered**: subdomínio, conexão enviada pelo frontend, tenant padrão,
tenant guardado globalmente ou factory singleton com conexão variável. Subdomínio
foi preterido pelo usuário; as demais opções violam isolamento ou confiança do servidor.

**Source**:
[EF Core lifetimes for multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy).

## D07 — Papéis operacionais e migrations fora da API

**Decision**: I1 separa leitura runtime do catálogo, runtime de cada tenant e owner
de migrations. EF CLI usa factories que validam alvo, role efetivo e configuração
privada; API não recebe os secrets elevados e não aplica migrations no startup.
Criação inicial de bancos/papéis é procedimento restrito do operador.
Provisionamento amplo e ferramentas de gestão são posteriores, não parte de I1.

Nomes de bancos são gerados a partir de UUIDs e identificadores são tratados com
quoting seguro; nenhum SQL recebe nome arbitrário do consumidor. Revogar `CONNECT`
de `PUBLIC` nos bancos da aplicação e conceder acesso apenas aos papéis correspondentes.
Runtime não tem DDL, CREATEDB, CREATEROLE ou superuser.

Aplicar migrations somente no processo de tooling, com lock EF, preflight e
diagnóstico de falha explícito. Criação de banco não é transação junto ao catálogo;
não adotar banco desconhecido nem excluir alvo automaticamente ao falhar.
Atualização incompatível exige janela de manutenção sem tráfego/writers.

**Rationale**: ter nomes de conexão diferentes não basta se papéis podem acessar
outros bancos. DDL e bootstrap exigem privilégios que o servidor web não deve possuir.
A coordenação é por etapas registradas, não uma transação distribuída.

**Alternatives considered**: `Database.Migrate` no startup, usar superuser na API,
pool com uma credencial para todos os bancos ou confiar em `--actor` para autorização.
Rejeitadas por privilégio excessivo e ausência de autenticação real do operador.

**Source**:
[EF Core applying migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

## D08 — Contratos REST e tratamento de falhas

**Decision**: OpenAPI 3.0.3 em JSON, endpoints futuros versionados `/api/v1`, Problem Details
com `code` e `traceId`, respostas sem redirecionar a páginas HTML.
Sucesso documentado significa resultado real; indisponibilidade não retorna lista vazia.

`openapi.json` descreve somente health checks I1. `future-openapi.json` documenta
I2, não autoriza expor essas rotas em I1. Retirar `/me/tenants`; `/me` consulta
somente a conta do tenant autenticado.

Em login futuro, ausência/formato de tenant inválido: 400. Tenant desconhecido/inativo,
usuário inválido/bloqueado ou senha incorreta: 401 AUTH_INVALID genérico.
Em operação autenticada, sessão inválida: 401; header divergente ou falta de concessão:
403; tenant de origem inativo: 409. Dependência indisponível: 503 com mensagem segura,
sem enumerar contas/empresas ou revelar referência de banco. Limitação de login: 429.

**Rationale**: o consumidor independente precisa distinguir falhas sem conhecer a
interface web. O formato 3.0.3 tem suporte amplo e pode ser emitido pelo gerador nativo
configurado; não exige biblioteca Swagger UI ou servidor adicional.

**Alternatives considered**: documentação manual sem contrato verificável,
erros ad hoc, detalhes de exceção públicos e fallback bem-sucedido. Rejeitadas
por drift, exposição e impossibilidade de validar comportamento.

## D09 — Extensibilidade mínima e frontend

**Decision**: usar composição DI explícita e grafo de módulos habilitados. Módulos
futuros expõem contratos, dados próprios e dependências; nenhuma extensão depende de
acesso às tabelas ou DbContexts internos de outro módulo.

I1 não implementa frontend ou registro dinâmico de módulos. Frontend posterior
tem login no tenant indicado e contexto, `fetch` nativo,
estado de autenticação/tenant em memória e React Router para URLs e histórico.
React Router é justificado pelas rotas, deep links e navegação back/forward,
não apenas pelo número de telas. Não adicionar Redux,
cache de negócio, framework de formulários, mediator ou mensageria.

**Rationale**: o primeiro aceite é backend e migration. As demais jornadas permanecem
objetivos posteriores; nenhum domínio comercial foi aprovado.
A extensão se comprova por fixtures posteriores, não por módulos prematuros.

**Alternatives considered**: carregar todos os módulos por padrão, plugin remoto,
CRUD comercial demonstrativo ou infraestrutura de eventos. Rejeitadas por escopo
e custo sem benefício presente.

## D10 — Persistência de chaves, containers e Dokploy

**Decision**: I1 usa Compose API/PostgreSQL com perfil pontual EF migrations.
Key ring e certificados de sessão passam a ser necessários somente ao implementar I2.
Nessa fase, key ring persistente, protegido por permissões e certificado externo
em produção. `SetApplicationName` distingue produto e ambiente; nunca compartilhar
as chaves entre produtos derivados. Rotação preserva certificados necessários
à leitura de chaves ainda válidas.

Compose local e Dokploy em VPS usam volumes nomeados. PostgreSQL 18 requer mount
persistente em `/var/lib/postgresql`, com PGDATA versionado; não copiar o mount
`/var/lib/postgresql/data` dos exemplos de versões anteriores.

Secrets são arquivos privados montados ou variáveis de runtime fornecidas pelo
operador. No Dokploy, variáveis definidas na UI não entram automaticamente nos
containers: mapear somente as necessárias, sem `env_file` indiscriminado em todos
os serviços. Não usar bind mounts absolutos sujeitos à limpeza de deploy.

**Rationale**: reiniciar aplicação não deve perder persistência ou invalidar sessões
por perda acidental das chaves. Isolar secrets por serviço preserva menor privilégio.

**Alternatives considered**: key ring temporário, secrets em build arguments,
mounts copiados de outra versão e compartilhar credenciais operacionais com a API.
Rejeitadas por perda de estado e exposição.

**Sources**:
[Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0),
[PostgreSQL image persistence](https://github.com/docker-library/docs/blob/master/postgres/README.md),
[Dokploy Compose](https://docs.dokploy.com/docs/core/docker-compose).

## D11 — Recuperação e políticas por produto

**Decision**: requisito de I4/pré-produção: backup lógico por catálogo e tenant,
cópia externa criptografada,
manifesto de tenant/ambiente/versão/ponto de recuperação e restauração em destino
de validação antes de substituir dados. Backup de tenant inclui suas contas,
permissões e sessões; restaurá-lo revoga sessões, renova epoch e revisa concessões
antes de reabrir acesso. Não depende de restaurar contas de outros tenants.
Catálogo restaurado contém apenas metadados técnicos: reconciliar bindings/estados
antes de reabrir uso, sem recuperar identidade global inexistente.

Cada produto define frequência, retenção, RPO, RTO e responsáveis antes de produção.
Downgrade de aplicação não executa downgrade de dados automaticamente.

**Rationale**: restaurar um banco errado pode violar isolamento; restaurar catálogo
antigo pode reconectar targets/estados obsoletos, e restaurar tenant pode reativar
sessões ou concessões antigas. O boilerplate não conhece carga
e necessidades de retenção de todos os produtos derivados.

**Alternatives considered**: cópia somente na VPS, snapshot sem identidade do alvo,
restaurar diretamente em produção sem validação e rollback apenas da imagem.
Rejeitadas por risco operacional e integridade.

## D12 — Testes, CI e controle de dependências

**Decision**: I1 inclui unitários, migrations/persistência em dois bancos reais,
arquitetura e contratos de saúde. SC-009 verifica estrutura, contas independentes
e reaplicação sem duplicação/perda. Testes de autenticação e frontend chegam com I2/I3.
Dados e módulos sintéticos pertencem ao host de testes,
não à API distribuída. SC-003 exige 200 operações e pelo menos 20 simultâneas,
incluindo efeitos válidos corretos e rollback quando aplicável.

GitHub Actions usa permissões mínimas, ações por SHA, lockfiles, checks obrigatórios,
Gitleaks CLI 8.30.1 para histórico e artefatos distribuídos, e auditorias nativas
npm/.NET para vulnerabilidades. Gitleaks é MIT, com release estável de 2026-03-21;
verificar checksums do binário e não usar a action que exige licença de organizações.
Resultado indeterminado ou scan indisponível nunca significa aprovação.
PR não recebe secrets de produção.
Interpretar adiamento de CI na spec como pipeline full-stack/deploy posterior;
checks mínimos de backend continuam obrigatórios antes da integração, conforme RC-002.

**Rationale**: reuso amplifica regressões; cobertura percentual não substitui
isolamento, revogação, banco-alvo correto ou recuperação.

**Alternatives considered**: persistência só com mocks, testes no banco de produção,
CI informativa sem bloqueio e dependências flutuantes. Rejeitadas por evidência insuficiente.

**Source**:
[GitHub Actions secure use](https://docs.github.com/en/actions/reference/security/secure-use),
[Gitleaks release](https://api.github.com/repos/gitleaks/gitleaks/releases/tags/v8.30.1),
[Gitleaks CLI](https://github.com/gitleaks/gitleaks/blob/v8.30.1/README.md),
[Gitleaks license](https://github.com/gitleaks/gitleaks/blob/v8.30.1/LICENSE).

## D13 — Factories e inicialização técnica sem seed de usuários

**Decision**: I1 usa factories `IDesignTimeDbContextFactory` com argumentos após
`--` e `UseSeeding`/`UseAsyncSeeding` para inicialização idempotente do marcador
TenantId/EnvironmentId. Factory verifica marcador e target antes de migrations;
callback só inicializa banco novo previamente aprovado e vazio. Marcador existente
correto é no-op; divergência ou ausência em estrutura existente interrompe.

Tooling EF chama o callback síncrono, inclusive em update sem migrations pendentes;
implementar também o assíncrono com a mesma regra. Não interpretar o Boolean do
callback como prova de primeira instalação. Usar lock nativo EF e preflight em
janela sem DDL/writers concorrentes.

Migration cria tabelas de acesso, não contas. Não usar HasData por tenant,
EnsureCreated, SQL aberto a consumidores ou seeding da API no startup.
Scripts SQL executados externamente não executam esses callbacks; não substituem
o roteiro de aplicação validado em I1 sem inicialização/verificação equivalentes.

**Rationale**: preservar EF model único, segurança do alvo e reaplicação SC-009
sem ferramenta administrativa ampla. Seeding ocorre depois de migration; por isso
callback sozinho não impede DDL em banco errado e não substitui preflight.

**Alternatives considered**: Identity central, migrations com HasData variável,
DbContext pooling com tenant mutável, bootstrap no startup e suíte Tools completa.
Rejeitadas por isolamento, drift do modelo, privilégio ou escopo.

**Sources**:
[design-time factories](https://learn.microsoft.com/en-us/ef/core/cli/dbcontext-creation),
[EF seeding/locking](https://learn.microsoft.com/en-us/ef/core/modeling/data-seeding),
[migration deployment](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

## Fechamento da pesquisa

Matriz consolidada com suporte e compatibilidade verificados; escolhas de segurança
confirmadas, propriedade tenant-local e limites de I1 definidos, alternativas registradas.
Não há esclarecimentos pendentes para iniciar
a fase 1. Pins auxiliares, digests e lockfiles são entregas de implementação, não
decisões arquiteturais adiadas. Não há ampliação para módulos de negócio ou serviços externos.
