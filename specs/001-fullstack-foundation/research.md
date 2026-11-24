# Research: Fundação full-stack reutilizável

**Date**: 2026-11-05 (esclarecimentos L2; decisões iniciais de 2026-10-13 preservadas)
**Revisões**: 2026-11-08 (D21); 2026-11-23 (L3, D22–D30); 2026-11-24 (L2.1, D31–D33;
delta L3, D34–D36); 2026-11-24, clarificações finais (D37 na L2.1, D38 na L3)
**Project**: `boilerplate-react-dotnet-sdd`
**Context**: [spec.md](spec.md), [plan.md](plan.md) e
[Constituição](../../.specify/memory/constitution.md).

Esta pesquisa registra decisões do desenho, justificativas e alternativas.
Não implementa bibliotecas, serviços ou infraestrutura. Autenticação por cookie
e indicação de tenant por header foram confirmadas pelo usuário.
L1/I1 está entregue; a revisão atual planeja L2/I2, sem implementar autenticação.
D01–D13 preservam a pesquisa inicial, não um retrato atualizado da implementação.
D14–D18 prevalecem nas decisões refinadas de L2 e usam os nomes efetivos do código.
A matriz anterior é histórica; esta revisão não verifica nem atualiza versões
frontend. Para backend, manifests/lockfiles atuais são a fonte dos pins.

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
referência no cookie e a desprotege antes de RetrieveAsync. O adapter valida a
referência contra a instalação fixa; não resolve conexões por tenant/header.
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

## D05 — Implantação dedicada por cliente (sem banco de catálogo central)

**Decision**: adotar o modelo **single-tenant por deploy (1 backend + 1 banco PostgreSQL
exclusivo da própria aplicação por cliente)**. Não existe banco apartado `platform_catalog`
nem `CatalogDbContext`. A identidade do cliente (`TenantId`), o ambiente (`EnvironmentId`),
o estado operacional e a conexão/secret do banco são fornecidos à instalação por variáveis
de ambiente/secrets (na aplicação correspondente do Dokploy na VPS ou em cofre de segredos).
Contas e permissões são I1; sessões persistidas no banco da instalação são I2.

O banco de cada instalação contém o marcador imutável `TenantDatabaseIdentity` (`TenantId`,
`EnvironmentId`, `AccessEpoch`), além do histórico de migrations da fundação. A conexão só é
liberada após correspondência do marcador com a configuração da instalação e compatibilidade
da versão. Dados sintéticos de negócio existem somente em testes.

**Rationale**: eliminar o catálogo central remove um ponto único de falha/configuração e
garante isolamento de processo e de banco entre clientes. Conferir o marcador `TenantDatabaseIdentity`
impede que um erro operacional de variáveis de ambiente conecte a instalação do cliente A ao banco B.
Email/CPF igual em clientes diferentes não causa deduplicação ou concessão automática.

**Alternatives considered**: backend compartilhado com banco `platform_catalog`, coluna de
discriminação em banco compartilhado, schema por tenant ou confiar somente na string de conexão
sem marcador no banco. Rejeitadas por preferência explícita por isolamento completo por deploy
e proteção contra configuração cruzada.

**Source**:
[EF Core multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy).

## D06 — Identidade da instalação, validação de header e recursos scoped

**Decision**: I1 tooling exige `TenantId`/`EnvironmentId` explícitos e conexão da instalação.
Em I2, a instância conhece seu próprio `IdTenant` pela configuração segura do ambiente; o header
`X-Tenant-Id` obrigatório nas cinco rotas de acesso deve coincidir com o `IdTenant` da instalação e com o
marcador do banco antes de qualquer verificação de credenciais ou sessão. Qualquer divergência é
negada imediatamente (403/401 conforme a operação), sem tentar selecionar outro banco.
O contexto autorizado só existe após validar usuário, sessão e permissão no banco da instalação.

DbContexts são scoped. Nenhum componente aceita conexão enviada pelo frontend ou tenta resolver
múltiplos bancos em tempo de execução. Acesso ao cliente B ocorre na instalação publicada de B,
autenticando a conta de B. Não existe listagem global de tenants por conta.

**Rationale**: com 1 backend + 1 banco por cliente, o processo não precisa nem deve conhecer
bancos de outros clientes; validar o `TenantId` da instalação e o marcador do banco garante
que requisições ou configurações destinadas a outro cliente falhem imediatamente.

**Alternatives considered**: roteamento dinâmico para vários bancos no mesmo processo, conexão
enviada pelo frontend ou ausência de validação do `TenantId` da instalação. Rejeitadas por
aumentar superfície de risco de acesso cruzado.

**Source**:
[EF Core lifetimes for multi-tenancy](https://learn.microsoft.com/en-us/ef/core/miscellaneous/multitenancy).

## D07 — Papéis operacionais e migrations fora da API

**Decision**: I1 separa a credencial runtime da instalação (usada pela API, sem DDL) e o
owner de migrations (usado apenas no processo EF CLI). As factories validam a identidade
da instalação (`TenantId`/`EnvironmentId`), o role efetivo e o marcador do banco; a API
não recebe os secrets elevados e não aplica migrations no startup. Criação inicial de
banco e papéis é procedimento restrito do operador por instalação.

Identificadores são tratados com quoting seguro; nenhum SQL recebe nome arbitrário do
consumidor. Revogar `CONNECT` de `PUBLIC` no banco da aplicação e conceder acesso apenas
aos papéis da própria instalação. Runtime não tem DDL, CREATEDB, CREATEROLE ou superuser.

Aplicar migrations somente no processo de tooling, com lock EF, preflight e diagnóstico
de falha explícito. Atualização incompatível exige janela de manutenção sem tráfego/writers.

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
Categorias de falha, validação de entrada (FluentValidation), `Resultado<T>`, códigos
`<MODULO>_<REGRA>`, status 400/409/422 e JSON estrito estão detalhados em
[ADR 0003](../../docs/adr/0003-tratamento-de-erros-e-validacao.md).

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
em produção. `SetApplicationName` distingue produto, ambiente e tenant; nunca compartilhar
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

**Decision**: requisito de I4/pré-produção: backup lógico por banco de cada instalação
de cliente, cópia externa criptografada, manifesto de `TenantId`/`EnvironmentId`/versão/ponto
de recuperação e restauração em destino de validação antes de substituir dados. Backup do
cliente inclui suas contas, permissões, sessões e dados de negócio; restaurá-lo valida o
marcador do banco contra a identidade da instalação, revoga sessões, renova `AccessEpoch`
e revisa concessões antes de reabrir acesso. Não depende de restaurar outros clientes nem
existe banco de catálogo central.

Cada produto define frequência, retenção, RPO, RTO e responsáveis antes de produção.
Downgrade de aplicação não executa downgrade de dados automaticamente.

**Rationale**: restaurar um banco errado pode violar isolamento; restaurar tenant pode reativar
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

## Revisão L2 — 2026-11-04

### D14 — Base entregue e fronteira incremental

**Decision**: usar L1/I1 como base entregue, com `AcessoTenantDbContext`,
`UsuarioAplicacao`, `PermissaoUsuario`, `IdentidadeBancoTenant`,
`IdAplicacao/IdAmbiente/IdTenant`, `EstadoTenant` e `StringsConexao:BancoApp`
(`IdAplicacao` renomeado para `CodigoAplicacao` em D19).
Não traduzir tipos herdados Identity nem renomear propriedades JSON já previstas.
SDK-base 10.0.401 com `rollForward=latestFeature`, pacotes backend fixados no
repositório e migration L1 de 2026-11-02 são a referência efetiva.
L2 adiciona migration; não recria estrutura, tarefas L1 ou contas.

**Rationale**: os documentos iniciais descreviam antes da implementação.
Planejar sobre nomes antigos ou prometer SDK sem roll-forward produziria comandos
e tarefas incompatíveis com a base atual.
Leitura do Compose/exemplo não permite presumir runtime distinto do owner;
P0 do plano exige separação real e modo local/HTTPS antes de expor login.

**Alternatives considered**: reimplementar L1, renomear seu código para inglês,
considerar todo comentário de exemplo como controle comprovado ou atualizar a stack.
Rejeitadas por escopo, regressão ou ausência de evidência.

**Revisão 2026-11-07 (substitui a premissa de preservação acima)**: vale a decisão
de construção do zero de D19. O código L1 é ponto de partida alterável, não base
preservada: a migration L1 de 2026-11-02 é descartada e regenerada como migration
inicial única; não há contas, hashes, concessões, volumes ou senhas L1 a manter nem
upgrade L1→L2 a testar. Continuam válidos: SDK/pacotes fixados como referência,
nomes Identity herdados, nomes JSON já contratados e o histórico de [tasks](tasks.md).

**Sources**: [tasks L1](tasks.md),
[SDK](../../global.json), [pacotes](../../Directory.Packages.props),
[API](../../backend/src/App.Api/Program.cs),
[Compose](../../infra/compose.yaml).
Isto é inspeção documental/de código, não reexecução das provas L1.

### D15 — Cookie nativo, store local e corridas de revogação

**Decision**: usar Cookies + `ITicketStore` nativos. Adapter HTTP na API com
escopo por chamada e porta própria de persistência em Application/Infrastructure.
Sessão L2 persiste ticket protegido, epoch, prazos, revogação e versão no banco
local. Data Protection protege tanto o cookie-referência quanto os bytes do ticket.
Não adicionar cache de sessão ou capturar DbContext scoped nas options.

Retrieve confere sessão, conta ativa/lockout/stamp, epoch e expiração em cada
operação. Concessões são verificadas no banco na autorização. Desabilitar o sliding
expiration **do handler de cookie**; sem refresh. *(Revisado por [D31](#d31--sessão-por-inatividade-com-teto-absoluto-revisa-d21-2026-11-24),
2026-11-24: a expiração passa a ser inatividade + teto absoluto, controlada no servidor
pelo registro de uso; o cookie e o ticket continuam sem renovação.)* Renew não faz upsert, não estende
o teto e só atualiza linha ainda ativa/versão esperada. Logout persiste revogação
antes de confirmar; erro de banco não é retornado como `null` ou logout concluído.

No login válido com sessão anterior, revogá-la antes de SignIn para gerar chave
nova. O handler pode reutilizar chave existente ao chamar SignIn; não presumir
rotação automática. Corridas de login/desativação e Renew/logout exigem transação,
updates condicionais e testes determinísticos, inclusive zero linhas atualizadas.
Esclarecimento de 2026-11-05: sessões simultâneas por conta são permitidas.
Rotação por login/logout limita-se ao cookie recebido; outras sessões permanecem
válidas. Reset/desativação/lockout/revogação operacional revogam todas as da conta.
Callbacks de rejeição/renew não podem reativar referência revogada.
Esclarecimento de 2026-11-05: instalação inativa não bloqueia GET antiforgery nem
logout de sessão própria ainda válida; o token permite somente sua revogação,
não consultar dados. `/me` e `/context` continuam bloqueados.
Inativação da instalação suspende uso, não revoga sessões nem muda epoch/prazos.
Após reativar, somente sessões ainda válidas e não revogadas podem retomar,
mantendo a contagem da expiração durante o período inativo.

**Rationale**: usar criptografia/protocolo do framework evita implementação própria;
store persistente elimina a validade de replay após revogação. Revogação sem intervalo
atende à próxima operação após commit. Escopos e referências validadas impedem
dependência circular da autenticação ou seleção de outro banco.

**Alternatives considered**: JWT sem estado, cookie só assinado, validação periódica
de stamp, sliding expiration, upsert no Renew e store em Redis.
Rejeitadas por atraso de revogação, risco de ressurreição ou serviço desnecessário.

**Sources**:
[Cookie authentication .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0),
[ITicketStore .NET 10](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.cookies.iticketstore?view=aspnetcore-10.0),
[handler — source browser](https://source.dot.net/Microsoft.AspNetCore.Authentication.Cookies/CookieAuthenticationHandler.cs.html).
Source browser é evidência auxiliar, não fonte fixada ao binário 10.0.12.
Comportamento exato de Store/Retrieve/Renew/Remove e sign-in/sign-out será comprovado
nos testes com os pacotes efetivamente utilizados; não há execução nesta pesquisa.

### D16 — CSRF JSON, limitação de login e saúde isolada

**Decision**: `IAntiforgery.GetAndStoreTokens` no GET de token e
`ValidateRequestAsync` explícito nos POSTs JSON de login/logout.
Após mudança de identidade, consumidor faz outro GET para obter novo token.
Não depender de validação implícita de formulários/minimal APIs, SameSite ou CORS.
Usar headers/cookies existentes no contrato, sem frontend nesta entrega.

Rate limiter nativo por IP efetivo/instalação: janela fixa de um minuto, cinco
pedidos, fila zero e 429/Retry-After. Identity: cinco falhas de senha por conta,
15 minutos de lockout, habilitado em toda conta desde a criação (revisão
2026-11-07: não há contas L1 preservadas); testar atualização concorrente.
Esclarecimento de 2026-11-05: confirmar lockout revoga todas as sessões da conta
e muda stamp atomicamente. Fim do bloqueio exige novo login, sem retomada de
cookies antigos; nenhuma conta/sessão da outra instalação é alterada.
Forwarded headers somente de proxies explicitamente confiáveis.
Uma réplica por instalação; escala horizontal exigiria outra decisão de limite.

Pipeline de saúde e OpenAPI fica fora do middleware que autentica cookies.
Somente `AllowAnonymous` não prova ausência de Retrieve.
Liveness não acessa banco/chaves; readiness confere identidade/versão e
round-trip de proteção de chaves, sem consultar contas/sessões.

**Rationale**: login também exige CSRF; token de identidade anterior não serve para
logout autenticado. Rate limiting e lockout tratam riscos distintos e devem ser
testados separadamente. Health não pode disparar acesso a sessão arbitrária.

**Alternatives considered**: JSON considerado imune a CSRF, token único antes/depois
de login, confiar em X-Forwarded-For livre, limiter global entre clientes ou
`AllowAnonymous` como única barreira de health. Rejeitadas por contrato e isolamento.

**Sources**:
[Antiforgery .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0),
[Rate limiting .NET 10](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0),
[Forwarded headers .NET 10](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer?view=aspnetcore-10.0),
[Health checks .NET 10](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0).
Não extrapolar mecanismos automáticos documentados apenas para versões posteriores.

### D17 — Key ring por instalação e upgrade aditivo

**Decision**: volume persistente privado por instalação, nome lógico Data Protection
derivado de aplicação/ambiente/tenant e certificado X.509 externo para proteção em
repouso em produção. Não adicionar contexto EF/banco de chaves.
Cookie `__Host-bps-session` só em HTTPS; exceção Development HTTP localhost usa
`bps-session`, falhando fora desse escopo. Preservar key ring em restart; rotação
mantém certificados necessários às chaves ainda válidas.

Migration L2 aditiva cria sessões e auditoria vazias e prepara lockout para contas
existentes; manter hash/conta/concessão/marcador/epoch L1.
Atualização exige preflight e manutenção com owner. Testar banco vazio, L1→L2 e
reaplicação; não presumir que alterar init scripts atualiza um volume já existente.
Revisão de consistência de 2026-11-05: runtime exige cadeia de migrations conhecida
completa; tooling aceita seu prefixo válido. Histórico com IDs desconhecidos
bloqueia, mesmo sem pendentes. Ticket expirado pode permanecer armazenado até
limpeza, mas nunca autoriza uso; revogação remove o ticket.
Development HTTP em Docker é delimitado pela publicação no loopback do host,
não por presumir que o endereço remoto interno do gateway é loopback.

**Rationale**: nome lógico sozinho não substitui chaves/arquivos isolados.
Persistir key ring em diretório explícito exige definir proteção em repouso,
não presumir criptografia automática. Upgrade real passa a ser obrigação agora
que existe um estado anterior entregue.

**Alternatives considered**: key ring efêmero/compartilhado, novo serviço/banco de
chaves, cookies inseguros em Production para facilitar Compose e recriar a migration
inicial. Rejeitadas por perda de sessão, isolamento ou perda de dados.

**Revisão 2026-11-06 (substitui a parte de migration acima)**: o projeto está em
construção do zero; nada foi publicado e não há dados. O esquema é criado por uma
migration inicial única com o modelo completo e as convenções de D19, e os bancos
locais são criados do zero. A parte de key ring/cookies desta decisão permanece válida.

**Sources**:
[Data Protection configuration .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0),
[Key encryption at rest](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest?view=aspnetcore-10.0),
[EF migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying).

### D18 — Procedimento de contas restrito e auditável

**Decision**: host console mínimo `Platform.Operations` com comandos fechados,
perfil Compose pontual e casos de uso reutilizados de Application/Infrastructure.
Não é servidor administrativo. Usa `UserManager`, senha lida sem eco,
configuração privada e argumentos de identidade para confirmar o alvo.
Role operacional local distinta de runtime/owner, autenticada pelo PostgreSQL
e permitida pela configuração protegida do processo; ACL do ambiente autoriza
o operador a usar seu segredo. `--actor`/flag não autentica ninguém.
Esclarecimento de 2026-11-05: login de operação é individual e não compartilhado,
atribuído a um único operador. A auditoria grava esse login autenticado com a
correlação; uma credencial técnica compartilhada não comprova autoria pessoal.
O recorte auditável comum abrange contas, permissões e sessões, com criação e
última alteração atribuídas e correlacionadas. Base pura `EntidadeAuditavel` e
contrato `IEntidadeAuditavel` em Domain; permissões/sessões herdam a base, usuário
Identity implementa o contrato sem trocar sua classe-base.
Infrastructure compartilha mapeamento/preenchimento (mecanismo em D19) e obtém
autoria de contexto confiável. Ações anônimas/automáticas são distintas; todo
registro nasce com autoria e data reais. Histórico operacional é imutável e não herda base mutável.
Não ampliar a base para marcador/tabelas auxiliares Identity nem armazenar diff
de campos secretos. Alternativa de dupla herança no usuário é impossível em C#;
refatorar Identity ou criar framework genérico de histórico foi rejeitado por escopo.

Registrar resultado em `RegistroOperacaoAcesso`; alterações bem-sucedidas e auditoria
na mesma transação. Revogar sessões/stamp ao desativar/redefinir/revogar acesso.
Recuperação L2 é reset pelo operador, sem email/self-service.
Senha nova/reset: 12–128 caracteres com validadores Identity definidos no contrato;
não elevar retroativamente o requisito de senha existente no login.
Sem bootstrap privilegiado por HTTP, seed padrão, CRUD de papéis ou catálogo.

**Rationale**: FR-033 entra no recorte L2 e agora tem consumidor concreto.
Um host pontual é menor que painel/API administrativa e mais verificável que
SQL manual para hash/stamp/lockout. A auditoria participa do resultado, não é
stdout descartável apresentado como prova durável.

**Alternatives considered**: adiar FR-033 à produção, editar tabelas/senhas por SQL,
criar conta padrão, usar owner na operação rotineira ou expor endpoints de gestão.
Rejeitadas por recorte, controles incompletos e privilégio excessivo.

**Sources**: FR-033/SC-002 de [spec.md](spec.md),
[Identity configuration .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-10.0),
[ADR 0002](../../docs/adr/0002-sessoes-e-operacao-acesso-local.md).

### D19 — Preenchimento de auditoria, data/hora e identificadores (2026-11-06)

**Decision**:
- **Auditoria**: porta `IContextoOperacao` (Application) com `IdOperacao`, tipo/autor
  e `InstanteOperacao`, uma implementação por host; `InterceptadorAuditoria`
  (`SaveChangesInterceptor`, Infrastructure) preenche criação/alteração e incrementa
  `Versao` onde ela existe (token por entidade em D20). Entidades auditáveis só são alteradas rastreadas; `ExecuteUpdate`/
  `ExecuteDelete` proibidos nelas, com teste de arquitetura. Corridas em Renew/logout
  e revogações são resolvidas por `Versao` como token de concorrência.
- **Sem campos duplicados**: permissão usa `CriadoEm` da base como instante da
  concessão (sem `ConcedidaEm`); sessão usa `CriadoEm` como emissão, sem `EmitidaEm`.
  `RevogadaEm` permanece como estado da sessão.
- **Construção do zero**: nada publicado nem dados; o código existente é ponto de
  partida alterável. O esquema nasce de uma migration inicial única gerada do modelo
  completo (a migration de rascunho atual é descartada). `CriadoEm` é sempre
  obrigatório; não há exceção de relógio.
- **Sem exclusão lógica na base**: estados próprios (`EstaAtivo`, `RevogadaEm`) e
  exclusão física auditada da concessão atendem a L2.
- **Data/hora**: `DateTimeOffset` UTC em `timestamptz`, normalizado para
  microssegundos por `InstanteUtc` (Domain, puro); instante atual somente de
  `TimeProvider`, lido uma vez por operação; sem `now()`/default do banco, sem
  exceções. APIs de relógio estáticas
  barradas por teste de arquitetura. Fuso de exibição fica na interface.
- **Identificadores**: Constituição 2.2.0. `Id*` é chave técnica opaca, UUID v7
  gerado no servidor; código de negócio é coluna `Codigo*`/`Numero*` própria.
  `CodigoPermissao` na PK da concessão é exceção registrada (catálogo fechado no
  código-fonte, não de env/Key Vault). Campos que guardam login/identidade textual
  deixam de usar o prefixo `Id` (`LoginOperador`, `AutorCriacao`, `AutorAlteracao`);
  configuração `IdAplicacao` passa a `CodigoAplicacao` (mesmo valor).
- **Nomes no banco**: PK `Id<Entidade>`; FK `FkId<Entidade>[Papel]` só na
  persistência (Domain/Application/DTO usam `IdUsuario`); colunas PascalCase;
  tabelas snake_case; constraints/índices `pk_/fk_/ix_/uq_/ck_` em minúsculas, até
  63 caracteres, por convenção central e teste. Propriedades Identity `Id`/`UserId`
  não são renomeáveis (declaradas nas classes-base e usadas pelo `UserStore`); suas
  colunas são mapeadas para `IdUsuario`/`FkIdUsuario`.

**Rationale**: o plano dizia “preenchimento compartilhado” sem mecanismo, e as
transições de sessão tendiam a `ExecuteUpdate`, que ignora interceptores e deixaria
a auditoria desatualizada sem erro. O código L1 chamava `DateTimeOffset.UtcNow`
diretamente (inicializador de `PermissaoUsuario` e preflight), o que impede controlar
o relógio em teste e faz a validação de `default` nunca disparar. .NET guarda
100 ns e o PostgreSQL microssegundos, o que quebra comparações após gravar e ler.
Misturar relógio do banco e da aplicação torna `agora >= ExpiraEm` inconsistente.
UUID v7 mantém os Ids atuais (`Guid`), não é enumerável e é ordenável para índice.

**Alternatives considered**:
- `ExecuteUpdate` via extensão que inclui colunas de auditoria: rejeitada, dois
  caminhos de auditoria para manter; o SELECT extra é irrelevante no volume de sessões.
- Ter `ConcedidaEm` e `CriadoEm` lado a lado: rejeitada pelo mantenedor (mesmo
  significado em dois campos).
- `BaseEntity` com `Id int`, `CreatedBy` livre e `Deleted bool` (modelo de outro
  projeto): rejeitada; PK composta/Guid, autoria tipada e conflito do soft delete
  com Identity/PK.
- `DateHelper` estático com `UtcNow` e conversão para America/Sao_Paulo: rejeitado;
  não testável, fallback silencioso para UTC sem `tzdata`, e conversão de exibição
  não pertence ao backend. `Microsoft.Extensions.TimeProvider.Testing` dispensado
  para não adicionar pacote; uma implementação de teste mínima basta.
- Catálogo `access.permissoes` com `IdPermissao`: rejeitado por ora, pois duplicaria
  a fonte da verdade (código) sem consumidor; revisitar se permissões virarem dado
  cadastrável.
- `bigint` identity como Id padrão: rejeitado; enumerável se exposto e divergente
  dos UUIDs já usados.
- Migrations incrementais sobre o rascunho atual: rejeitada pelo mantenedor; sem
  dados nem publicação, só acrescentaria renames e exceções sem benefício.
- FK com o mesmo nome da PK referenciada (`IdUsuario`): preterida pelo mantenedor em
  favor de `FkIdUsuario`, que torna a FK explícita na coluna.
- Colunas em snake_case: preterida; o mantenedor prefere PascalCase igual à
  propriedade C#, aceitando aspas em SQL manual (o host operacional evita SQL manual).
- Nome de configuração indicando a origem (ex.: prefixo de Key Vault): rejeitado;
  a mesma chave vem de fontes diferentes por ambiente, e segredo é definido pela
  lista documentada, não pelo nome.

**Sources**: [Constituição IX](../../.specify/memory/constitution.md),
[TimeProvider](https://learn.microsoft.com/en-us/dotnet/standard/datetime/timeprovider-overview),
[EF Core interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors),
[ExecuteUpdate e ChangeTracker](https://learn.microsoft.com/en-us/ef/core/saving/execute-insert-update-delete),
[Npgsql date/time](https://www.npgsql.org/doc/types/datetime.html).

### D20 — Tabelas auxiliares Identity, concorrência por entidade, relógio dos UUIDs e troca de sessão (2026-11-07)

**Decision**:
- **Tabelas auxiliares Identity** (`reivindicacoes_usuario`, `logins_usuario`,
  `tokens_usuario`): permanecem no modelo como **exceção registrada** às regras de PK.
  `IdentityUserClaim<Guid>.Id` é `int` declarado na classe-base; logins e tokens têm
  PK composta textual definida pelo framework. Renomear colunas não muda esse tipo.
  Mapeamento: `Id` da reivindicação → coluna `IdReivindicacaoUsuario` (int identity,
  sem UUID v7), `UserId` → `FkIdUsuario`, constraints na convenção central. Nenhum
  fluxo L2 lê ou grava essas tabelas: login usa verificação de senha sem fábrica de
  claims, reset usa `UserManager` sem token persistido e não há login externo/2FA.
  Runtime e operador não recebem INSERT/UPDATE/DELETE nelas; teste prova que
  continuam vazias após a suíte e o teste de modelo as trata como exceção listada.
  Não recebem base auditável. Usá-las exige novo plano.
- **Concorrência por entidade** (o token é escolhido pelo dono da escrita):

  | Entidade | Token | Escrita | Interceptor |
  |---|---|---|---|
  | `UsuarioAplicacao` | `ConcurrencyStamp` nativo | somente `UserManager` | Preenche metadados; não toca em `ConcurrencyStamp` nem cria `Versao`. Mudança apenas de `ConcurrencyStamp` não é alteração efetiva. |
  | `PermissaoUsuario` | nenhum (imutável) | INSERT/DELETE | Rejeita `Modified`. Alteração não existe; retirada é DELETE com evento em `RegistroOperacaoAcesso`. |
  | `SessaoAutenticacao` | `Versao` int64 | store e casos de uso | Preenche metadados e incrementa `Versao` (contrato `IEntidadeVersionada`). |
  | `RegistroOperacaoAcesso` | nenhum | INSERT | Rejeita `Modified`/`Deleted`. |

  `IdentityResult` com `ConcurrencyFailure` e `DbUpdateConcurrencyException` são
  falha segura. Exceção única: na contagem de falhas de senha, que não pode perder
  tentativas, o caso de uso recarrega a conta e reavalia senha/lockout até três vezes;
  esgotado, 503 sem sessão. Concessão concorrente duplicada (violação de PK) ou
  retirada concorrente (zero linhas no DELETE) é conflito explícito, não no-op.
- **Relógio dos UUIDs**: `Guid.CreateVersion7()` sem argumento lê o relógio do
  sistema e é proibido. Todo `Id*` gerado usa `IdentificadorV7.Gerar(instante)`
  (helper puro em Domain sobre `Guid.CreateVersion7(DateTimeOffset)`, exige offset
  zero) com o `InstanteOperacao` da operação; o timestamp do UUID coincide com
  `CriadoEm` em milissegundos. PKs `Guid` são `ValueGeneratedNever()`: o gerador do
  provider (que também usaria o relógio do sistema) fica desligado. `UsuarioAplicacao.Id`
  é atribuído pelo caso de uso antes de `CreateAsync`. `SecurityStamp`/
  `ConcurrencyStamp` continuam gerados pelo Identity: são carimbos aleatórios, não `Id*`.
  Teste de arquitetura barra `Guid.NewGuid()` e `Guid.CreateVersion7()` sem argumento
  no código de produção.
- **Troca de sessão no login** (clarificação de 2026-11-07): uma unidade de trabalho
  por requisição revoga a sessão anterior com `Versao` esperada e insere a nova;
  commit antes de emitir `Set-Cookie`. `StoreAsync` participa da mesma unidade pelo
  escopo da requisição; o login nunca chama `RenewAsync` com a chave anterior.
  Conflito de `Versao` na sessão anterior (outro login, logout, Renew ou revogação
  concorrente) faz rollback e retorna **409 SESSION_CONFLICT**, sem cookie novo e sem
  alterar a anterior; repetir exige novo GET antiforgery. Falha de persistência faz
  rollback e retorna 503 DEPENDENCY_UNAVAILABLE. Login sem sessão anterior válida não
  tem disputa a perder.

**Rationale**: o modelo afirmava UUID v7 em todas as PKs, mas o Identity impõe tipos
nas tabelas auxiliares; registrar a exceção é mais seguro que remover entidades das
quais o `UserOnlyStore` depende. Um `Versao` paralelo ao `ConcurrencyStamp` criaria dois
tokens com semânticas diferentes na mesma linha, e `Versao` em concessão imutável não
protege nada. Gerar UUID com relógio próprio contradiz a fonte única de D19. 409 separa
a disputa perdida de credencial inválida (401) e de dependência indisponível (503).

**Alternatives considered**:
- Ignorar as entidades auxiliares no `OnModelCreating`: rejeitada; métodos do store e
  `UserClaimsPrincipalFactory` falhariam em tempo de execução sem contrato documentado.
- Store Identity próprio só com usuário: rejeitada por escopo (abstração nova sem consumidor).
- `Versao` também em usuário/permissão por uniformidade: rejeitada (token duplicado ou inútil).
- Repetição automática da troca de sessão: rejeitada; a clarificação exige contexto atual.
- 401 para a disputa perdida: rejeitada; o consumidor não distinguiria credencial inválida.

**Sources**: clarificações 2026-11-07 de [spec.md](spec.md),
[Identity model customization](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0),
[Guid.CreateVersion7(DateTimeOffset)](https://learn.microsoft.com/en-us/dotnet/api/system.guid.createversion7),
[EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

### D21 — Prazo de sessão configurável e limpeza auditada de sessões (2026-11-08)

> **Revisada por [D31](#d31--sessão-por-inatividade-com-teto-absoluto-revisa-d21-2026-11-24)
> (2026-11-24)**: a chave `Sessao:PrazoAbsolutoMinutos`, a faixa 5–480, o `ExpiraEm` fixo
> como única expiração e o CHECK de prazo foram substituídos. Continuam vigentes desta
> decisão: regra estrita de parse, valores fixados na emissão, comando `sessao limpar`,
> registro único por execução, privilégios e invocação rejeitada antes do alvo. O critério
> de remoção passa a usar a expiração efetiva (D31). O texto abaixo é histórico.

**Decision**:
- **Prazo absoluto configurável**: chave `Sessao:PrazoAbsolutoMinutos` (env
  `Sessao__PrazoAbsolutoMinutos`), lida somente pela API. Ausente = 30. Aceita apenas
  inteiro decimal sem sinal, separador ou espaço (`NumberStyles.None`, cultura
  invariante) na faixa **5–480 inclusive**; `4`, `481`, `30.5`, `0`, `-5` ou texto
  impedem a inicialização (`ValidateOnStart`) com diagnóstico que cita a chave e a
  faixa, nunca o valor de outras configurações. Não há fallback silencioso para 30.
  O valor é capturado na emissão: `ExpiraEm = CriadoEm + prazo`, ambos do mesmo
  `InstanteOperacao`; `ExpiresUtc` do ticket e o cookie usam esse `ExpiraEm`.
  Retrieve/Renew comparam `agora >= ExpiraEm` gravado, nunca recalculam com a
  configuração atual; mudar o prazo e reiniciar não altera sessões emitidas.
  CHECK do banco garante `ExpiraEm - CriadoEm` entre 5 e 480 minutos.
- **Comando de limpeza**: `sessao limpar` no host `Platform.Operations`, mesmo
  preflight/autorização dos demais comandos. Retenção em
  `OperacaoAcesso:RetencaoSessoesDias` (ausente = 30; inteiro 1–365, mesma regra de
  parse). Não há argumento de retenção nem modo “forçar”: a política é configuração
  protegida do host, não escolha ad hoc. Sem job, `IHostedService` ou timer na API;
  agendamento externo é L4.
- **Critério de remoção**: `corte = InstanteOperacao - RetencaoDias`. Remove somente
  sessões com `RevogadaEm < corte` ou (`RevogadaEm` nulo e `ExpiraEm < corte`).
  Como `corte < agora`, uma sessão ativa nunca satisfaz o critério; encerrada
  exatamente no corte é mantida (“há mais de N dias”). Revogação só ocorre em
  sessão ativa, então `RevogadaEm` é sempre o instante de encerramento da revogada.
- **Execução**: uma transação. Projeta `IdSessao`/`Versao` das elegíveis (sem
  carregar tickets), anexa stubs e marca `Deleted` (alteração rastreada, com `Versao`
  como token; `ExecuteDelete` continua proibido), insere o registro de sucesso e faz
  commit. Conflito ou erro: rollback total (zero removidas) e, em transação separada,
  registro `Falha`. Sessões elegíveis não têm transição concorrente legítima
  (Renew/logout/revogação exigem sessão ativa), então conflito é anomalia, não disputa.
- **Registro único por execução**: `RegistroOperacaoAcesso` com
  `TipoOperacao=LimparSessoes`, operador individual autenticado (`LoginOperador`),
  `RetencaoDias`, `InstanteCorte`, `QuantidadeRemovida` (0 em falha ou nada elegível),
  `Resultado`, `CodigoResultado` e `IdOperacao`; `IdUsuarioAlvo`/`CodigoPermissao`
  nulos e nenhuma lista de sessões ou contas. Os três campos novos são obrigatórios
  apenas nesse tipo (CHECK). Não existe autoria `Sistema` para limpeza.
- **Invocação rejeitada antes do alvo validado** (argumentos, retenção fora da faixa,
  configuração ou preflight) não chega a ser uma execução sobre o banco: segue a regra
  existente — saída não zero, diagnóstico seguro em stderr e journal protegido, sem
  gravar em banco não validado. A partir do alvo validado e papel autorizado, toda
  execução gera exatamente um registro.
- **Privilégios**: DELETE em `sessoes_autenticacao` somente para o operador; runtime
  mantém SELECT/INSERT/UPDATE em sessões, sem DELETE.

**Rationale**: fixar o prazo na emissão torna o comportamento independente de
reinícios e mantém “sem extensão”. Configuração protegida para retenção evita que um
argumento apague histórico recente por engano. Uma única transação dá contagem exata
e “zero ou tudo”, compatível com o volume de sessões de 5–480 minutos por instalação.
O registro sem ids cumpre a clarificação e evita transformar auditoria em inventário.

**Alternatives considered**:
- Recalcular expiração com o prazo atual: rejeitada; violaria “fixado na criação”.
- `ExecuteDelete` em lote: rejeitada; contorna interceptor/token e a regra D19.
- Lotes com commits parciais: rejeitada; falha deixaria contagem ambígua.
- `--retencao-dias` por argumento: rejeitada; política por produto, não por execução.
- Job em background na API: rejeitado pela clarificação de 2026-11-08.
- Autoria `Sistema` para limpeza: rejeitada; a limpeza é sempre iniciada por operador.

**Sources**: clarificações 2026-11-08 de [spec.md](spec.md) (FR-006, FR-033, SC-002),
[Options validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/options#options-validation),
[EF Core concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency).

## Fechamento da pesquisa L2

Não há esclarecimentos técnicos pendentes; D21 incorpora as clarificações de
2026-11-08 sem alterar D14–D20. Decisões acima permitem a Phase 1 sem
atualizar a stack ou ampliar para frontend/negócio/deploy.
Valores privados, caminhos de certificado e proxies reais são configuração do
ambiente de validação/produção, não escolhas arquiteturais silenciosamente assumidas.
P0 do plano permanece gate obrigatório de implementação; pesquisa/desenho não
equivalem a testes, scans, migrations ou liberação executados.

## Revisão L2.1 — 2026-11-24 (reabertura da L2: modelo de sessão)

Base: L2 entregue e validada (`c33acbe`). Incorpora as clarificações de 2026-11-23
sobre FR-006/SC-002 (inatividade + teto absoluto). A L2.1 DEVE ser implementada e
validada antes de iniciar qualquer bloco da L3. Numeração D31–D33 continua após a
revisão L3 (D22–D30), já registrada; D34–D36 são o delta L3 correspondente. D37
(motivo da negação só no log) pertence à L2.1; D38 (409 no diálogo) é delta L3.

### D31 — Sessão por inatividade com teto absoluto (revisa D21, 2026-11-24)

**Decision**:
- **Regra**: a sessão é válida enquanto `agora < ExpiracaoEfetiva`, com
  `ExpiracaoEfetiva = min(UltimoUsoEm + JanelaInatividadeMinutos, TetoAbsolutoEm)`,
  além das condições L2 já existentes (registro ativo e não revogado, ticket presente,
  conta ativa e fora do bloqueio, stamp e epoch conferindo). Comparação sempre com
  valores gravados e o `agora` do `TimeProvider`; nunca com a configuração atual.
- **Configuração** (lida somente pela API; o host operacional não a usa):

  | Chave (env) | Padrão | Faixa inclusiva |
  |---|---|---|
  | `Sessao:InatividadeMinutos` (`Sessao__InatividadeMinutos`) | 30 | 5–240 |
  | `Sessao:TetoAbsolutoMinutos` (`Sessao__TetoAbsolutoMinutos`) | 600 | 30–720 |

  Parse estrito herdado de D21/G10: inteiro decimal sem sinal, separador ou espaço
  (`NumberStyles.None`, cultura invariante); ausente ou vazio (`""`) = padrão.
  Fora da faixa, não inteiro, ou `InatividadeMinutos > TetoAbsolutoMinutos` impede a
  inicialização (`ValidateOnStart`) com diagnóstico que cita somente as chaves e as
  faixas, nunca valores de outras configurações. Inatividade igual ao teto é aceita.
  A chave antiga `Sessao:PrazoAbsolutoMinutos`, se presente com valor não vazio, também
  impede a inicialização, com diagnóstico indicando as duas chaves novas: evita que um
  produto derivado que configurou um prazo curto passe silenciosamente a 30/600.
- **Persistência** (`access.sessoes_autenticacao`): `ExpiraEm` é renomeado para
  `TetoAbsolutoEm` (= `CriadoEm` + teto, imutável); novas colunas
  `JanelaInatividadeMinutos` (inteiro, imutável) e `UltimoUsoEm` (UTC, inicia igual a
  `CriadoEm`, só cresce). Teto e janela são fixados na emissão a partir da configuração
  vigente; mudar a configuração e reiniciar não altera sessões emitidas. A expiração
  efetiva é derivada, não persistida. CHECKs substituem `ck_sessoes_autenticacao_prazo`:
  `TetoAbsolutoEm − CriadoEm` entre 30 e 720 minutos; `JanelaInatividadeMinutos` entre 5
  e 240 e não maior que `TetoAbsolutoEm − CriadoEm`; `CriadoEm ≤ UltimoUsoEm <
  TetoAbsolutoEm`. Detalhes em [data-model §7](data-model.md#sessaoautenticacao--accesssessoes_autenticacao).
- **Cookie e ticket**: o handler de cookie continua com `SlidingExpiration = false` e
  nunca reemite o cookie por uso. `ExpiresUtc` do ticket e `Expires` do cookie = 
  `TetoAbsolutoEm`. A inatividade é aplicada **somente no servidor** (Retrieve compara
  `ExpiracaoEfetiva`), de modo que o cookie em si não carrega nem estende prazo e
  requisições normais não emitem `Set-Cookie`. Sessão expirada por inatividade é
  tratada como ausente (401 `AUTH_REQUIRED`), sem revogar a linha.
- **Renovação**: somente pelo registro de uso de [D33](#d33--registro-de-uso-da-sessão-limitação-de-gravação-e-concorrência);
  nunca ultrapassa o teto (a fórmula usa `min`). Sem remember-me, refresh ou
  renovação por login silencioso.
- **Instalação inativa**: não pausa teto nem inatividade. Requisições negadas com 409
  não renovam; ao reativar, a sessão só retoma se `agora < ExpiracaoEfetiva`.
- **Limpeza (`sessao limpar`)**: retenção contada da expiração efetiva ou da
  revogação: elegível se `RevogadaEm < corte`, ou se `RevogadaEm` nulo e
  `min(UltimoUsoEm + janela, TetoAbsolutoEm) < corte`. Como `corte < agora`, sessão
  ativa continua inelegível. A consulta usa `UltimoUsoEm < corte` como pré-filtro
  indexado (condição necessária, pois a janela é positiva) e calcula o mínimo no SQL com
  `corte` como parâmetro (sem relógio do banco). Demais regras de D21 inalteradas.
- **API**: `expiresAt` (login) e `sessionExpiresAt` (`/me`, e portanto `/context.user`)
  passam a significar explicitamente o **teto absoluto**; campos aditivos
  `idleTimeoutMinutes` (login) e `sessionIdleTimeoutMinutes` (`/me`) informam a janela de
  inatividade fixada na sessão. O instante de expiração por inatividade **não** é exposto:
  muda a cada uso e incentivaria temporizador no cliente (vedado). Contrato em
  [operations §4](contracts/operations.md#expiração-e-uso-da-sessão-l21) e
  [openapi.json](contracts/openapi.json) 2.1.0.
- **Mantidos sem alteração**: troca atômica de sessão no login, 409 `SESSION_CONFLICT`
  na disputa de logins, bloqueio revogando todas as sessões, sessões independentes por
  dispositivo, logout revogando só a corrente, revogação operacional de todas.

**Rationale**: atende FR-006/SC-002 com a menor mudança sobre o store nativo de D15:
o protocolo do cookie não muda, a decisão de validade permanece no banco local e
continua revogável na próxima operação. Persistir janela e teto na emissão cumpre
"fixados na criação" sem depender da configuração atual. Manter o cookie com `Expires`
no teto evita reemitir `Set-Cookie` a cada requisição.

**Alternatives considered**:
- Sliding expiration nativo do handler: reemite cookie/ticket e chama `RenewAsync` com
  regras próprias (metade da janela), não respeita teto nem "negada não renova" e
  reabriria os riscos de ressurreição tratados em D15. Rejeitado.
- Persistir `ExpiraInatividadeEm` em vez de `UltimoUsoEm` + janela: redundante e perde
  a janela original (exigida pelo CHECK e pela exibição). Rejeitado.
- Cookie de sessão sem `Expires`: mudaria o comportamento L2 ao fechar o navegador
  sem pedido da spec. Rejeitado.
- Ignorar a chave antiga: mudança silenciosa de política de segurança. Rejeitado.

**Sources**: clarificações 2026-11-23 de [spec.md](spec.md) (FR-006, SC-002),
[Cookie authentication .NET 10](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0),
[Options validation](https://learn.microsoft.com/en-us/dotnet/core/extensions/options#options-validation).

### D32 — Migration inicial única regenerada em vez de migration incremental

**Decision**: a L2.1 **regenera** a migration inicial única `EsquemaInicialAcessoTenant`
(novo timestamp, Designer e snapshot) a partir do modelo completo, em vez de criar uma
migration incremental. A L3 faz o mesmo para o CHECK de `CodigoPermissao` com hífen
(revisa D28). A regra vale enquanto não houver publicação com dados (até a primeira
publicação, L4); a partir dela, toda mudança de esquema é incremental, com upgrade
validado. Bancos locais A/B e de CI são recriados do zero pelo roteiro existente
(quickstart §3); um banco local antigo é rejeitado pela readiness/tooling por histórico
desconhecido, comportamento L2 que permanece e é a evidência de que não há upgrade
silencioso. GRANTs por tabela continuam emitidos pela própria migration (G6).

**Rationale**: a clarificação de 2026-11-06 estabelece que nada foi publicado, não há
dados e o esquema é criado por **uma migration inicial única gerada do modelo
completo**; SC-009 vale para ela. Uma migration incremental criaria um caminho de upgrade
permanente para um estado que nunca existiu fora de máquinas de desenvolvimento e
exigiria regra de preenchimento de `UltimoUsoEm`/`JanelaInatividadeMinutos` para linhas
inexistentes. FR-015 exige atualização de estado anterior "quando existente"; não existe.
A lógica de tooling que aceita prefixo da cadeia continua coberta pelos testes atuais
com cadeias sintéticas.

**Alternatives considered**:
- Migration incremental `SessaoInatividadeTeto` (e `PermitirHifenCodigoPermissao` na L3):
  contraria a clarificação de 2026-11-06 e adiciona backfill sem dado real. Rejeitada.
- Antecipar o CHECK com hífen para a regeneração da L2.1: mistura escopo da L3 (id de
  módulo) num recorte de regressão; regenerar de novo na L3 é barato. Rejeitada.

**Risco**: perder a "primeira prova de upgrade" prevista em D28. Mitigação: a prova de
upgrade real passa a ser critério da primeira mudança de esquema após a publicação (L4).

### D33 — Registro de uso da sessão, limitação de gravação e concorrência

**Decision**:
- **Requisição autenticada aceita**: requisição a operação protegida por sessão
  (`/me`, `/context` e, na L3, rotas de módulos) que passou por tenant, marcador,
  sessão válida, instalação ativa, CSRF (métodos não seguros) e autorização e chegou ao
  handler. Respostas do próprio handler (inclusive 400/422 de regra) contam como uso.
  As revalidações da interface por carga, foco/visibilidade ou navegação (`GET /me`,
  D36) são requisições aceitas comuns e renovam a inatividade (clarificação 2026-11-24);
  o servidor não as distingue, e a interface só as dispara nesses eventos, nunca por
  temporizador. Não renovam: qualquer negação antes do handler (400/401/403/409/429/503), login (cria a
  sessão com `UltimoUsoEm = CriadoEm`), logout (revoga), GET antiforgery,
  `GET /api/v1/tenant`, health, OpenAPI e arquivos da interface.
- **Onde**: filtro de endpoint `FiltroRegistroUsoSessao` (App.Api) aplicado ao grupo de
  rotas protegidas, executado depois da autorização e antes do handler, chamando
  `CasoUsoRegistrarUsoSessao` (Application) sobre a porta de sessões. A validação de
  sessão no Retrieve (que roda antes da autorização) não grava nada.
- **Limitação de frequência**: grava `UltimoUsoEm = InstanteOperacao` somente se
  `InstanteOperacao − UltimoUsoEm ≥ 60 s` (constante `IntervaloMinimoRegistroUso`, não
  configurável) — no máximo uma escrita por minuto por sessão. Escrita por entidade
  rastreada com `Versao` esperada (sem `ExecuteUpdate`); o `InterceptadorAuditoria`
  incrementa `Versao` e preenche `AlteradoEm`/`AlteradoPor` com a conta da sessão (D20).
- **Concorrência**: conflito de `Versao` no registro de uso é descartado sem repetição —
  outra requisição já registrou uso, ou a sessão foi revogada, e a próxima requisição
  reavalia; o handler da requisição atual prossegue, pois ela já foi aceita com sessão
  válida. Falha de dependência ao gravar → 503 `DEPENDENCY_UNAVAILABLE` sem executar o
  handler. Revogações (logout, revogação de todas as sessões da conta por reset,
  desativação, bloqueio ou procedimento operacional, e a troca no login) que encontrarem
  conflito de `Versao` recarregam a sessão: se ela continua ativa e só `UltimoUsoEm`
  mudou, repetem até 3 vezes; se foi revogada, vale a regra L2 de disputa real (login →
  409 `SESSION_CONFLICT`; demais → comportamento atual). Esgotadas as tentativas → 503
  sem confirmar. Assim um registro de uso nunca derruba nem confunde uma revogação, e
  nunca reativa sessão revogada (a escrita exige a `Versao` lida com a sessão ativa).
- **Tolerância introduzida**: o uso real pode ocorrer até 60 s depois do último uso
  gravado. Logo, a sessão é negada **no máximo** `janela` após a última requisição
  aceita (nunca depois — direção segura) e **nunca antes** de `janela − 60 s`.
  Consequências para os testes (relógio controlado):
  - testes de fronteira usam intervalos ≥ 60 s entre usos (todo uso grava), o que torna
    os limites exatos: último uso + 29:59 aceita; + 30:00 nega;
  - teste dedicado da limitação: uso a +30 s não grava (`UltimoUsoEm` e `Versao`
    inalterados) e a expiração conta do último uso gravado; uso a +60 s grava;
  - o cenário SC-002 "uso a intervalos menores que 30 min" é exercitado com intervalos
    de 29 min e de 1 min até o teto (600 min: 599:59 aceita, 600:00 nega mesmo em uso).
  O caso residual — usos intermediários não gravados seguidos de intervalo entre 29 e
  30 min — pode negar até 60 s antes; aceito pela spec (FR-006/SC-002, clarificação 2026-11-24).

**Rationale**: gravar em toda requisição dobraria as escritas, faria requisições
paralelas da mesma aba (`/me` + `/context`) disputarem `Versao` e aumentaria conflitos
com revogações e com a troca de sessão no login. Uma escrita por minuto mantém o custo
desprezível e a imprecisão sempre a favor da segurança.

**Alternatives considered**: gravar sempre (conflitos e ruído de auditoria);
`ExecuteUpdate` condicional (proibido por D19/D20 em entidade auditável); intervalo
configurável (sem necessidade e amplia a matriz de testes); conceder 60 s extras na
verificação (violaria "30 min sem uso nega"); registrar uso no Retrieve (renovaria
requisições negadas por autorização ou instalação inativa); revalidação da interface por
verificação que não registra uso (endpoint ou cabeçalho próprio: mais contrato e testes
para eventos que já são ação humana — rejeitada na clarificação de 2026-11-24).

### D37 — Motivo da negação de sessão só no log (2026-11-24)

**Decision**:
- **Resposta inalterada**: toda negação por sessão inválida continua 401
  `AUTH_REQUIRED` genérico, sem campo, cabeçalho ou texto que indique o motivo. Contrato
  e `openapi.json` não mudam.
- **Motivo categorizado** (enum `MotivoNegacaoSessao` em Application): `Inatividade`
  (`agora ≥ UltimoUsoEm + janela`, antes do teto), `TetoAbsoluto` (`agora ≥ TetoAbsolutoEm`;
  prevalece se ambos), `Revogada` (`RevogadaEm` preenchida), `Invalida` (cookie presente
  mas ilegível, ticket inexistente, conta bloqueada/desativada, stamp ou epoch divergente,
  instalação de origem diferente, ou sessão alterada entre a validação e o caso de uso) e
  `Ausente` (operação protegida sem cookie de sessão).
- **Onde**: `ValidacaoSessaoAtual` devolve o motivo junto do resultado; o
  `ArmazenamentoTicketsSessao` (Retrieve) o guarda num serviço scoped da requisição
  (`IContextoNegacaoSessao`); casos de uso que devolvem `AUTH_REQUIRED` por mudança
  posterior registram `Invalida`. O ponto único que escreve o 401 de sessão
  (`OnRedirectToLogin` e o mapeamento de `AUTH_REQUIRED` dos casos de uso) emite o log.
  Login (`AUTH_INVALID`) e o pedido de sair seguem os registros atuais; logout com 401
  também registra o motivo.
- **Registro**: `EventId(1002, "SessaoNegada")`, nível `Information`, mensagem com
  `Motivo`, `TraceId` (o mesmo da resposta) e `IdOperacao`. **Nunca** inclui id da
  sessão, ticket, cookie, id ou email da conta, IP ou caminho com dados.
- **Testes**: integração com `CapturaLogs` (já existente) para os cinco motivos com relógio
  controlado; cada um verifica o `EventId`, o motivo, que o `TraceId` do log é igual ao da
  resposta e que a resposta (status, cabeçalhos e corpo, exceto `traceId`) é idêntica nos
  cinco casos;
  verificação negativa de que nenhum valor de cookie, id de sessão ou de conta aparece no
  log capturado.

**Rationale**: o suporte recebe o código de referência mostrado pela interface e precisa
explicar por que a sessão caiu (NFR-006); expor o motivo ao cliente não muda o
comportamento da interface (mesmo diálogo) e revelaria estado da conta, como revogação pelo
operador.

**Alternatives considered**: motivo na resposta (`reason`) com mensagem específica na
interface (informação de conta a quem tem só o cookie; mais contrato e estados na UI);
nenhum registro (investigação impossível); nível `Warning` (expiração é evento normal e
geraria ruído); incluir id da conta (não necessário para correlacionar, já há `TraceId`, e
amplia dados pessoais em log).

## Revisão L3 — 2026-11-23

Base: L2 entregue e validada. Decisões abaixo incorporam as clarificações de
2026-11-22 e 2026-11-23 da [spec](spec.md) e prevalecem sobre D04/D09/D12 no que
divergirem. D01 permanece como registro histórico; os pins do frontend são os de D24.

### D22 — Interface servida pela API

**Decision**: o build do Vite é copiado para `wwwroot/` da imagem da API por um estágio
Node no `Dockerfile.api`; a API serve arquivos estáticos e um fallback restrito
`{*path:nonfile}` para `index.html`, depois de todas as rotas. `/api/*`, `/openapi/*` e
`/health*` nunca caem no fallback (404 Problem Details). Arquivo com extensão inexistente
→ 404, para que falha de chunk seja detectável. Em Production, ausência de `index.html`
ou do `buildId` impede a inicialização; em Development/Testes a API inicia e navegações
recebem 404 JSON. Em desenvolvimento, `vite dev` encaminha `/api`, `/openapi` e
`/health` ao backend (mesma origem vista pelo navegador, cookies `bps-*` em localhost).

**Rationale**: mesma origem real sem CORS, uma stack por tenant (Constituição IV),
cookie `__Host-` e CSRF já desenhados na L2. Arquivos com ponto (`.vite/`) são excluídos
pelo provedor físico padrão.

**Alternatives considered**: Nginx/serviço estático por instalação (serviço extra e
configuração de proxy por tenant); CDN/origem separada (CORS e cookies cross-site);
fallback para qualquer caminho (mascararia 404 de API e de chunks).

### D23 — Identidade pública do tenant e `buildId`

**Decision**: `GET /api/v1/tenant`, anônima, sem `X-Tenant-Id`, sem lookup de sessão,
devolve `{ id, displayName, buildId }` quando configuração e marcador conferem
(instalação ativa **ou** inativa); senão 503 genérico. `buildId` = 32 hex iniciais do
SHA-256 sobre o conteúdo das fontes do frontend (`src/**`, `index.html`, lockfile e
configuração do Vite), calculado por um plugin local antes do bundle, injetado como
constante e gravado em `.vite/id-build.txt`; a API lê esse arquivo na inicialização.
Sem build: `sem-interface`, e a interface em `import.meta.env.DEV` não compara.
A interface consulta somente em eventos (carga, foco/visibilidade, navegação).

**Rationale**: o frontend precisa do tenant antes de chamar rotas que exigem
`X-Tenant-Id`, sem variável de ambiente nem bundle por tenant. Hash de conteúdo é
determinístico e opaco (não é versão, commit, data ou ambiente). Não revelar
ativa/inativa a anônimos evita sondagem de estado comercial.

**Alternatives considered**: tenant em `VITE_*` (bundle por instalação, viola a
clarificação); `<meta>` gerada por instalação (HTML dinâmico); `applicationVersion`
de `/context` (exige sessão e não identifica o build da interface); UUID aleatório por
build (não reprodutível); limitador próprio na rota (risco baixo, NAT corporativo
poderia bloquear usuários legítimos; borda na L4).

### D24 — Stack do frontend e pins reverificados (2026-11-23)

**Decision**: pins exatos (registro npm consultado em 2026-11-23): React/DOM 19.3.1,
React Router 8.5.2 (pacote `react-router`, modo declarativo), Vite 8.3.8,
`@vitejs/plugin-react` 6.3.0, Tailwind/`@tailwindcss/vite` 4.3.5, **TypeScript 6.0.3**,
ESLint 10.15.0 + `typescript-eslint` 8.74.0 + `eslint-plugin-react-hooks` 7.1.1,
Prettier 3.10.1, Vitest 5.1.1 + jsdom 31.0.1, Testing Library React 16.3.3/DOM 10.4.2/
user-event 14.6.13, Playwright 1.65.1, `@axe-core/playwright` 4.14.0. Node 24.22.3 LTS
(`.nvmrc`, `engines`, imagem Node com digest registrado).

**Rationale**:
- TypeScript 7.0.2 (D01) não é aceito por `typescript-eslint` 8.74.0 (peer
  `typescript <6.1.0`); lint com informação de tipos é exigido (FR-027). 6.0.3 é a
  última estável compatível.
- `eslint-plugin-jsx-a11y` 6.10.2 não declara suporte a ESLint 10; acessibilidade é
  garantida por axe nos E2E (bloqueante, NFR-011), consultas por papel/rótulo no
  Testing Library e revisão. Reavaliar quando houver versão compatível.
- React Router 8 é a linha corrente (exige React ≥19.2.7 e Node ≥22.22); 7.x recebe
  só correções. Justificado por URLs, deep links, back/forward e página não encontrada (D09).
- jsdom 31 exige Node ≥24.15; Vitest 5.1 e Vite 8 aceitam Node 24.
- Prettier aplica a convenção de formatação por ferramenta (Constituição IX), como
  `dotnet format` no backend.

**Alternatives considered**: TypeScript 7 sem lint com tipos (perde regras estritas);
duas versões de TypeScript (complexidade); ESLint 9.39.5 para manter jsx-a11y (linha
anterior); happy-dom (menor fidelidade); Cypress (sem vantagem sobre Playwright, que
já fornece Chromium e integração axe).

**Sources**: `registry.npmjs.org/<pacote>` (`peerDependencies`, `engines`, `time`),
[Node.js releases](https://nodejs.org/dist/index.json).

### D25 — Estado no cliente, multi-aba, revalidação e atualização

> **Revisada por [D35](#d35--nova-entrada-no-próprio-local-e-estado-preservado-revisa-d25)
> e [D36](#d36--várias-abas-e-recarga-única-sem-armazenamento-revisa-d25) (2026-11-24)**:
> 401 deixa de descartar o estado (nova entrada no próprio local), o aviso entre abas
> passa a ser só de logout explícito e o marcador em `sessionStorage` é removido.
> Permanecem: sem biblioteca de estado, `ClienteApi` sobre `fetch`, sem retentativa
> automática, revalidação por eventos e comparação de `buildId`.

**Decision**: sem biblioteca de estado/consultas. `ClienteApi` sobre `fetch`
(`credentials: 'same-origin'`, `X-Tenant-Id`, CSRF, parse de Problem Details para
categorias) e um provedor React com máquina de estados explícita
(`carregando`, `indisponivel`, `anonimo`, `autenticado`, `semPermissaoContexto`,
`empresaIndisponivel`, `sessaoEncerrada`). Cache somente em memória da aba; descartado
no logout, em 401 e no aviso de outra aba. Sem retentativa automática. Multi-aba por
`BroadcastChannel('bps-sessao')` com mensagem só de tipo (`sessao-encerrada`); fallback
pela revalidação de `/me` no foco/visibilidade e pela próxima chamada negada. Nova
versão por comparação de `buildId`; falha de chunk (`vite:preloadError`) → uma recarga,
guardada por `sessionStorage['bps-recarga'] = buildId` (removido após carga
bem-sucedida). Esse marcador não contém dado de conta, contexto ou empresa (NFR-012).

**Rationale**: quatro leituras e duas escritas, sem dados de negócio; requisitos
exigem justamente desligar recursos típicos das bibliotecas (retry, persistência, cache
entre telas). Menos dependências e bundle menor (NFR-013).

**Alternatives considered**: TanStack Query (revalidação no foco pronta, mas exige
desligar retry/cache e adiciona dependência sem necessidade presente; reavaliar no
primeiro módulo de negócio com telas de dados); evento `storage` via `localStorage`
(grava no armazenamento, vedado); service worker (vedado); timer de expiração (vedado).

### D26 — CSP, estilos, cabeçalhos, cache, acessibilidade e idioma

**Decision**: CSP `default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self';
font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self';
frame-ancestors 'none'` em todas as respostas; `nosniff`; `Referrer-Policy: no-referrer`.
Tailwind v4 via plugin Vite gera CSS em arquivo; nenhuma biblioteca que injete estilos
em runtime; prop `style` proibida por regra de lint (`no-restricted-syntax`).
`/assets/*` imutável por um ano; `index.html` e toda resposta da API `no-store`.
pt-BR fixo (`<html lang="pt-BR">`), WCAG 2.1 AA: rótulos, foco visível, ordem de
tabulação, erros e carregamento anunciados, contraste verificado por axe. E2E falha com
violação séria/crítica do axe ou com mensagem de CSP no console.

**Rationale**: atende NFR-012 sem nonce (SPA estática servida igual a todos). HSTS
depende de HTTPS publicado (L4).

**Alternatives considered**: nonce por requisição (exigiria HTML dinâmico);
`style-src 'unsafe-inline'` (rejeitado na clarificação); UI kits com CSS-in-JS.

### D27 — Composição de módulos em compilação e catálogo efetivo

**Decision**: projeto `Platform.Modulos` (abstração `IModulo`, `DeclaracaoModulo`,
`ContratoModulo`, `DependenciaContrato`, `ValidadorComposicaoModulos`,
`CatalogoPermissoesEfetivo : ICatalogoPermissoes`, `ExcecaoComposicaoModulos`,
contrato `foundation.autorizacao` v1) e projeto `App.Composicao` com a lista única de
módulos (vazia na base). `App.Api` e `Platform.Operations` registram os módulos por
`App.Composicao`; a validação roda após o `Build()` e falha a inicialização. Cada módulo
= `<Modulo>.Contratos` (declaração estática e contratos) + `<Modulo>` (implementação).
Endpoints em `/api/v1/modules/{idModulo}/…`, fora da descrição OpenAPI. Imagem
`operations` passa à base `aspnet:10.0.12` porque `Platform.Modulos` referencia o
framework ASP.NET Core; o host continua sem Kestrel (teste de arquitetura).

**Rationale**: uma única lista garante o mesmo catálogo de permissões na API e no
operador (concessão rejeita código fora do catálogo efetivo). Prefixo `/modules/` evita
colisão de id de módulo (ex.: `me`) com rotas da fundação. Validação após o `Build()`
permite que o host de testes acrescente o módulo sintético.

**Alternatives considered**: descoberta por reflexão/plugins em diretório (seleção em
runtime, vedada); feature flags por configuração (vedado na L3); declaração sem
ASP.NET + segunda lista de implementações na API (dois pontos de edição);
`/api/v1/{idModulo}` (colisão com rotas da fundação).

### D28 — Código de permissão com hífen e concessões inertes

> **Revisada por [D32](#d32--migration-inicial-única-regenerada-em-vez-de-migration-incremental)
> (2026-11-24)**: o novo CHECK entra na **migration inicial única regenerada** da L3, não em
> uma migration aditiva `PermitirHifenCodigoPermissao`. Formato, catálogo efetivo e
> concessões inertes permanecem como abaixo.

**Decision**: formato `^[a-z][a-z0-9-]*(\.[a-z][a-z0-9]*)+$`, ≤128, em
`CodigosPermissao.PadraoFormato` e no CHECK, por migration aditiva
`PermitirHifenCodigoPermissao`. Validada em banco novo e em upgrade de banco L2
(cadeia de duas migrations, tooling com prefixo válido). Conceder exige código no
catálogo efetivo; revogar aceita código de formato válido existente na conta, mesmo
fora do catálogo, com auditoria normal. Concessões inertes não autorizam, não aparecem
em `/context` e não são apagadas automaticamente.

**Rationale**: id de módulo admite hífen (clarificação 2026-11-23) e o código de
permissão começa pelo id. O CHECK L2 (G7) rejeitaria `meu-modulo.x.ler`. Sem revogação
de inertes o operador não conseguiria limpá-las.

**Alternatives considered**: proibir hífen no id (contraria a spec); reescrever a
migration inicial (quebra bancos L2 validados e perde a primeira prova de upgrade);
apagar concessões ao omitir módulo (vedado pela spec).

### D29 — OpenAPI só da fundação, contratos de módulos e variantes SC-004

**Decision**: o grupo de módulos usa `ExcludeFromDescription`; teste compara o
`/openapi/v1.json` servido nas variantes sem/com módulo (igualdade após normalização).
Contratos de módulo ficam em `contracts/openapi.json` dentro da pasta do módulo.
Módulo sintético em `backend/tests/Modules/Teste.ModuloSintetico` e
`Teste.ModuloSintetico.Contratos`: id `teste-sintetico`, permissão
`teste-sintetico.eco.ler`, dependência de `foundation.autorizacao` v1, operação
`GET /api/v1/modules/teste-sintetico/eco`. Registrado somente pela fábrica de testes.
“Solução e imagem distribuídas” = projetos de `backend/src/**`, saída de
`dotnet publish` e imagens `api`/`operations` (verificados no CI por grafo de
referências e listagem da imagem); o projeto sintético pode constar da `.slnx` sob a
pasta de testes (gate Q0 item 4).

**Rationale**: a instância não anuncia superfície de módulos a anônimos; o OpenAPI
idêntico prova que a fundação não muda com módulos.

**Alternatives considered**: documento por módulo servido pela instância (revela
módulos anonimamente); filtrar por tag (frágil).

### D30 — Testes/CI do frontend, orçamento e SC-001 automatizado

**Decision**: workflow `frontend-ci.yml` com permissões mínimas e ações por SHA:
`npm ci`, `prettier --check`, ESLint, `tsc --noEmit`, Vitest, `vite build`, orçamento
(script local sem dependência: soma gzip da entrada e imports estáticos pelo
`.vite/manifest.json`; limite em `frontend/orcamento.json`, 200 KiB = 204 800 bytes),
`npm audit --audit-level=high` e Gitleaks também sobre `frontend/` e o `dist`. Job E2E:
executa o roteiro de `infra/scripts/` (mesmos comandos do quickstart: `.env.local`
sintético, bancos do zero, migrations, contas via `operations`, Compose com a API
servindo a interface) e `npx playwright test` em Chromium; registra duração e falha
acima de 45 min (SC-001). Estados difíceis de produzir na stack real (500 inesperado,
falha de rede, chunk ausente) usam `page.route`; os demais usam o backend real
(revogação via operador, instalação inativa por reinício com `EstadoTenant=Inativo`,
429 por seis logins). Execução manual SC-001: uma pessoa que não escreveu o guia,
registro de tempo, revisão e dúvidas em `tasks.md`.

**Rationale**: FR-027 torna todos bloqueantes; SC-001 exige roteiro idêntico ao guia.

**Alternatives considered**: E2E contra `vite dev` (não testa CSP/cache/fallback da
API); bundlesize/size-limit (dependência para uma soma simples).

### D34 — Descrição pt-BR das permissões declarada em código

**Decision**: toda permissão (fundação e módulos) é uma `DefinicaoPermissao(Codigo,
Descricao)` declarada em código. Fundação: `CodigosPermissao.Definicoes`
(`foundation.context.read` → "Consultar o próprio contexto de acesso"); módulos:
`DeclaracaoModulo.Permissoes` passa de lista de códigos a lista de definições.
`Descricao`: texto pt-BR de 1 a 200 caracteres, sem espaços nas pontas e sem caracteres
de controle. `ValidadorComposicaoModulos` rejeita descrição ausente/inválida com o código
`PERMISSAO_DESCRICAO_INVALIDA`, **inclusive para a fundação**, na inicialização dos dois
hosts e nos testes de arquitetura. `GET /api/v1/context` ganha o campo aditivo
`permissionDetails: [{ code, description }]`, ordenado por `code`, com exatamente as
permissões efetivas de `permissions` (inertes omitidas); `permissions` (lista de códigos)
é mantido para compatibilidade. A interface mostra descrição e código a partir de
`permissionDetails` e não tem tabela própria. Descrições não são persistidas; a
qualidade do texto pt-BR é verificada em revisão (não automatizável).

**Rationale**: a descrição nasce junto do código que a define, fica igual na API e no
operador e some com o módulo omitido. Campo novo preserva o consumidor L2 (NFR-010).

**Alternatives considered**: transformar `permissions` em objetos (quebra o consumidor
L2); operação de catálogo completo (revelaria permissões de módulos a quem não as tem);
tabela de descrições na interface (vedado pela spec); tabela no banco (dado de código,
ficaria órfão ao omitir módulo).

### D35 — Nova entrada no próprio local e estado preservado (revisa D25)

**Decision**: continua **sem biblioteca de estado/consultas**. A preservação usa o
próprio React:
- **Estado novo `reautenticando`** no provedor de sessão. Entra quando uma chamada de
  aba autenticada (`autenticado`, `semPermissaoContexto`) recebe 401 `AUTH_REQUIRED`
  (sessão expirada por inatividade ou teto, revogada ou ausente), inclusive na
  revalidação de foco. **Exceção** (clarificação 2026-11-24): 401 no pedido de sair
  conclui a saída localmente (descarta, `/entrar` com aviso, avisa as outras abas) e não
  abre o diálogo. Guarda em memória o `id` da conta e o tenant.
- **Conteúdo escondido, estado mantido**: a árvore protegida **permanece montada**
  (estado local dos componentes intacto) dentro de um contêiner com `hidden` e `inert`;
  nada protegido fica visível nem acessível por leitor de tela. O diálogo é um
  `<dialog>` nativo aberto com `showModal()` (camada superior, fundo inerte, foco preso,
  sem estilo inline — compatível com a CSP). O evento `cancel` (Esc) é bloqueado: o
  diálogo só fecha por nova entrada ou por "Sair". Ao abrir, foco no primeiro campo; ao
  fechar com restauração, foco volta ao elemento que o tinha (ou ao título da tela). O
  email não é pré-preenchido (o diálogo não exibe dado da conta encerrada).
- **Fila no `ClienteApi`**: leituras (`GET`) negadas por 401 ficam pendentes (promessa
  não resolvida) e são repetidas automaticamente após restauração pela mesma conta;
  gravações (métodos não seguros, exceto o pedido de sair) negadas por 401
  são rejeitadas com `ErroGravacaoNaoEnviada` e **nunca** reenviadas — o chamador mostra
  "A ação não foi concluída. Repita após entrar." Vários 401 simultâneos abrem um único
  diálogo.
- **Depois do login no diálogo** (`GET antiforgery` → `POST /sessions` → `GET /me`):
  mesmo `id` de conta → fecha o diálogo, remove `hidden`/`inert`, repete as leituras
  pendentes e revalida `/context` (permissões podem ter mudado); outra conta → descarta
  todo o estado (remonta a árvore com nova chave, rejeita a fila) e vai a `/contexto` da
  nova conta. "Sair" no diálogo → descarta todo o estado e vai a `/entrar`, sem pedido
  ao servidor (a sessão já é inválida) e sem aviso a outras abas (D36). Erros do login no
  diálogo seguem as categorias da tela de entrada; empresa inativa após a nova entrada
  (`/me` 409) → estado "empresa indisponível", somente sair.
- **Proibições mantidas**: nada em `localStorage`/`sessionStorage`/IndexedDB/cookies
  legíveis por script; sem temporizador de expiração, sem requisição periódica, sem
  login silencioso. O estado se perde ao fechar a aba.

**Rationale**: manter a árvore montada é a forma mais simples de preservar **todo** o
estado da aba (inclusive formulários de módulos futuros) sem serializar nada; `<dialog>`
nativo entrega foco preso e fundo inerte exigidos por WCAG sem dependência. A fila separa
leitura (idempotente, pode repetir) de gravação (nunca reenviada), como exige FR-004.

**Alternatives considered**: TanStack Query (guarda dados de consulta, mas não o estado
local de componentes, exigiria desligar retry/refetch e acrescenta ~13 KB gzip;
mutações já não seriam repetidas sem ela; reavaliar no primeiro módulo de negócio);
desmontar a árvore e guardar só um store global (perde estado local); redirecionar a
`/entrar` com retorno (vedado: destino vindo da URL e perda de estado); biblioteca de
modal/foco (`focus-trap`, Radix) — desnecessária com `<dialog>` nos navegadores suportados.

### D36 — Várias abas e recarga única sem armazenamento (revisa D25)

**Decision**:
- **Aviso entre abas só no logout explícito**: `BroadcastChannel('bps-sessao')` com
  `{ "tipo": "logout" }` (sem dados), enviado somente no logout explícito:
  `DELETE /sessions/current` 204 ou 401 (sessão já inválida, saída concluída localmente,
  clarificação 2026-11-24). Abas que recebem descartam o estado e vão a `/entrar`.
  Expiração, revogação e "Sair" do diálogo de nova entrada **não** avisam: cada aba trata
  a própria sessão.
- **Revalidação no retorno de foco/visibilidade** (e na navegação), uma por vez, sem
  polling: `GET /api/v1/tenant` (`buildId`) e, em estado autenticado ou
  `reautenticando`, `GET /api/v1/me`. Resultado: 200 mesma conta → em `reautenticando`
  restaura sem diálogo (nova entrada feita em outra aba; o cookie é compartilhado) e
  repete leituras pendentes; 200 outra conta → descarta e vai a `/contexto` da nova
  conta; 401 → `reautenticando` (diálogo). A revalidação de foco que obtém 200 conta como
  uso (D33): é disparada por ação do usuário, não por temporizador.
- **Recarga única sem armazenamento**: em `vite:preloadError`, recarrega somente se a
  navegação atual **não** for ela própria uma recarga
  (`performance.getEntriesByType('navigation')[0].type !== 'reload'`); caso contrário,
  tela de indisponibilidade com "Recarregar" manual. Em `reautenticando`, a recarga
  automática é suspensa (perderia o estado preservado) e aparece o aviso de nova versão.
  Remove o marcador `sessionStorage['bps-recarga']`: a interface não grava **nada** no
  armazenamento do navegador, e o E2E verifica `localStorage`, `sessionStorage` e
  IndexedDB vazios ao fim das jornadas.

**Rationale**: a spec passou a proteger o trabalho das outras abas na expiração; só a
saída explícita é decisão do usuário que vale para todas. O tipo de navegação do
navegador impede laço sem gravar marcador.

**Alternatives considered**: evento `storage` (grava armazenamento); marcador na URL
(`?recarga=1`, polui histórico e mistura URL com estado); manter `sessionStorage` com o
`buildId` (permitido pela NFR-012, mas desnecessário e contraria a orientação de não
gravar nada no navegador).

### D38 — Conflito de sessão no diálogo de nova entrada (2026-11-24)

**Decision**: no estado `reautenticando`, se `POST /sessions` responder 409
`SESSION_CONFLICT` (duas abas fizeram a nova entrada ao mesmo tempo sobre a mesma sessão
anterior), o provedor faz **uma única** `GET /api/v1/me`, sem novo token antiforgery e sem
reenviar o login:
- 200 mesma conta → restaura sem diálogo, como a revalidação de foco (D36): fecha o
  diálogo, devolve o foco, repete leituras pendentes e revalida `/context`;
- 200 outra conta → descarta todo o estado e vai a `/contexto` da nova conta;
- 401 → o diálogo permanece com a mensagem de conflito ("Não foi possível concluir a
  entrada. Tente novamente."), campos de senha limpos, nova tentativa só manual;
- 409 `TENANT_UNAVAILABLE` → "empresa indisponível", só sair;
- falha de rede/503 → o diálogo permanece com a mensagem de indisponibilidade e
  referência, sem nova tentativa automática.
A revalidação é a mesma função usada no retorno de foco (no máximo uma em andamento). Na
tela de entrada (sem estado preservado), o 409 continua só com a mensagem e nova
tentativa manual.

**Rationale**: o caso comum é a outra aba já ter criado sessão válida da mesma conta
(cookie compartilhado); uma leitura resolve sem repetir credenciais nem substituir de novo a
sessão recém-criada.

**Alternatives considered**: manter só a mensagem e esperar o próximo foco (o usuário
digitaria a senha de novo e substituiria a sessão da outra aba); repetir o login com as
credenciais digitadas (credencial mantida em memória além do envio e reenvio automático
vedado por FR-004).

## Fechamento da pesquisa L3

Não há `NEEDS CLARIFICATION`. Pendências de implementação, não de design: aprovação
das dependências e do pin TypeScript (gate Q0), ADRs 0004/0005 e confirmação da
interpretação de “solução distribuída” em SC-004 (D29). A L3 só começa após o aceite da
L2.1 (D31–D33 e D37); D34–D36 e D38 prevalecem sobre D25/D28 no que divergirem.
