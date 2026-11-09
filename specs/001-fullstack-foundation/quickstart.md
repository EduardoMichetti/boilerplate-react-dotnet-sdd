# Quickstart: validar a L1 e, após implementação, a L2

**Date**: 2026-11-05
**References**: [plano](plan.md), [modelo](data-model.md),
[operações](contracts/operations.md), [API L2](contracts/openapi.json).

**Estado atual**: L1 entregue. Login, sessões, host `Platform.Operations` e perfil
`operations` abaixo são **planejados**. Não executar a seção L2 como se já existisse.
Este comando de planejamento não executa testes, migrations ou containers.

## 1. Pré-requisitos e regressão L1 disponível

- PowerShell na raiz do repositório, Git e Docker Linux/Compose v2 responsivo.
- SDK conforme `global.json`: base 10.0.401, roll-forward `latestFeature`, sem preview.
  Runtime/ASP.NET e pacotes permanecem na linha fixada do repositório.
- Ferramentas locais/lockfiles existentes; integração cria PostgreSQL real descartável,
  não usa configuração/dados de produção. Nenhuma senha padrão é necessária.

Executar cada comando separadamente e interromper no primeiro erro:

```powershell
dotnet --version
docker info
dotnet tool restore
dotnet restore .\backend\Boilerplate.slnx --locked-mode
dotnet build .\backend\Boilerplate.slnx --no-restore
dotnet format .\backend\Boilerplate.slnx --verify-no-changes --no-restore
dotnet test --solution .\backend\Boilerplate.slnx --no-restore --minimum-expected-tests 1
```

Esperado na base L1: saúde/OpenAPI, erros seguros e persistência/migrations A/B.
As cinco rotas L2 ainda não existem. Histórico e escopo das provas estão em
[tasks.md](tasks.md); a execução acima, não este documento, gera nova evidência.

## 2. Preparação L2 — somente depois de implementar seus pré-requisitos

1. Aplicar P0 de [plan.md](plan.md): roles runtime/operador/owner distintas com
   grants reais, exemplos sem secrets utilizáveis e PostgreSQL sem exposição pública.
   Se precisar de depuração, publicar somente no loopback; não usar owner na API.
2. Copiar o exemplo atualizado para configurações privadas A/B. Os valores de
   `IdTenant` e `IdAmbiente`, bancos/roles, portas API/PostgreSQL e volumes/redes
   devem ser distintos. Não basta trocar o header/argumento de tenant.
3. Manter key rings separados/persistentes e nomes Data Protection derivados
   de aplicação/ambiente/tenant. Montar certificados/senhas por arquivos privados
   em produção; sem secrets no Git, imagem ou build arguments.
4. Para HTTP local, usar modo **Development + loopback** explicitamente e cookie
   `bps-session`; fora disso, HTTPS e `__Host-bps-session`. O Compose L1 em
   Production/HTTP não serve como validação dos cookies L2 sem esse ajuste.
   Publicação da API restrita ao loopback do host; listener interno do container
   pode usar sua interface de rede. Gateway Docker não precisa ser loopback.
   Nenhum Host/header encaminhado livremente substitui esse isolamento.
5. Configurar somente o perfil `operations` com segredo do operador de A ou B;
   cada operador usa login PostgreSQL individual e segredo não compartilhado;
   API não recebe esse segredo. Roles/grants são validados em volumes criados do
   zero (§3); não há volumes L1 a atualizar nem upgrade L1→L2 a provar.

Depois de preparar arquivos privados, validar sem imprimir secrets:

```powershell
$compose = '.\infra\compose.yaml'
$localA = '.\infra\.env.local'
$localB = '.\infra\.env.b.local'
$tenantA = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa'
$tenantB = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb'
$ambienteA = '11111111-1111-4111-8111-111111111111'
$ambienteB = '22222222-2222-4222-8222-222222222222'
docker compose --project-name boilerplate-a -f $compose --env-file $localA config --quiet
docker compose --project-name boilerplate-b -f $compose --env-file $localB config --quiet
docker compose --project-name boilerplate-a -f $compose --env-file $localA build api migrations operations
docker compose --project-name boilerplate-b -f $compose --env-file $localB build api migrations operations
docker compose --project-name boilerplate-a -f $compose --env-file $localA up -d postgres
docker compose --project-name boilerplate-b -f $compose --env-file $localB up -d postgres
```

Esses comandos pressupõem perfil/imagem `operations` implementados na L2.
Não imprimir `compose config` completo, usar credencial de demonstração como secret
ou apagar volumes para contornar falha. Interromper diante de erro.

## 3. Criar os bancos locais do zero e aplicar a migration inicial

Ambiente **local de desenvolvimento**, sem dados: começar com volumes novos para que
os scripts de banco/grants rodem (o `init-db.sql` só executa na criação do volume).
Este comando só é aceitável aqui, em ambiente local descartável:

```powershell
docker compose --project-name boilerplate-a -f $compose --env-file $localA down --volumes
docker compose --project-name boilerplate-b -f $compose --env-file $localB down --volumes
```

Parar ambas as APIs/writers, definir `EstadoTenant=Inativo` nos arquivos privados.
Banco novo vazio aprovado usa `Migracoes__PermitirInicializarBancoVazio=true` uma
vez; reaplicações com marcador correto usam false.

```powershell
docker compose --project-name boilerplate-a -f $compose --env-file $localA stop api
docker compose --project-name boilerplate-b -f $compose --env-file $localB stop api
docker compose --project-name boilerplate-a -f $compose --env-file $localA --profile migrations run --rm migrations dotnet ef database update --project Platform.Infrastructure --startup-project App.Api --context AcessoTenantDbContext -- --tenant-id $tenantA --environment-id $ambienteA
docker compose --project-name boilerplate-b -f $compose --env-file $localB --profile migrations run --rm migrations dotnet ef database update --project Platform.Infrastructure --startup-project App.Api --context AcessoTenantDbContext -- --tenant-id $tenantB --environment-id $ambienteB
```

Esperado: marcador criado e tabelas de contas, permissões, sessões e auditoria
vazias, com colunas `Id*`/`FkId*` e constraints `pk_/fk_/ix_/uq_/ck_` no padrão.
Reaplicar em manutenção não duplica nem perde dados. Histórico com migration fora
da cadeia conhecida é rejeitado como versão desconhecida.
Runtime tentando DDL e alvo A apontando a B falham.
Não gerar migration por cliente nem chamar migration pelo endpoint HTTP.

## 4. Preparar contas/permissões sem painel

Com perfil e host L2 implementados, criar conta sintética por instalação.
Entrar senhas **diferentes** e privadas no prompt sem eco; não incluí-las no comando.
Guardar os `idUsuario` retornados, não inferi-los de um exemplo.

```powershell
docker compose --project-name boilerplate-a -f $compose --env-file $localA --profile operations run --rm operations conta criar --id-tenant $tenantA --id-ambiente $ambienteA --email acesso@example.invalid --nome-exibicao "Teste A"
docker compose --project-name boilerplate-b -f $compose --env-file $localB --profile operations run --rm operations conta criar --id-tenant $tenantB --id-ambiente $ambienteB --email acesso@example.invalid --nome-exibicao "Teste B"
$idUsuarioA = Read-Host 'Id de usuario retornado na instalacao A'
$idUsuarioB = Read-Host 'Id de usuario retornado na instalacao B'
docker compose --project-name boilerplate-a -f $compose --env-file $localA --profile operations run --rm operations permissao conceder --id-tenant $tenantA --id-ambiente $ambienteA --id-usuario $idUsuarioA --codigo foundation.context.read
docker compose --project-name boilerplate-b -f $compose --env-file $localB --profile operations run --rm operations permissao conceder --id-tenant $tenantB --id-ambiente $ambienteB --id-usuario $idUsuarioB --codigo foundation.context.read
```

Esperado: resultado JSON/exit code conforme [operations.md](contracts/operations.md)
e auditoria persistida da operação, sem senha/hash/conexão. Operador sem papel
autorizado não muda nada; API/runtime não pode executar esse procedimento.
Ativar A/B (`EstadoTenant=Ativo`) e iniciar:

```powershell
docker compose --project-name boilerplate-a -f $compose --env-file $localA up -d api
docker compose --project-name boilerplate-b -f $compose --env-file $localB up -d api
$origemA = 'http://localhost:8080'
$origemB = 'http://localhost:8081'
Invoke-RestMethod "$origemA/health/live"
Invoke-RestMethod "$origemA/health/ready"
Invoke-RestMethod "$origemB/health/ready"
```

Origens HTTP acima só para o modo local aprovado. HTTPS usa origem/certificado
confiável configurados, sem desabilitar validação TLS.

## 5. Consumidor independente — SC-005

Usar cliente HTTP com cookie jar e os contratos, sem frontend nem código interno.
Exemplo PowerShell para A, em sessão local privada. Não habilitar transcript/log
de requisições com corpo/cookie e não salvar o corpo de login em arquivo:

```powershell
$cabecalhosA = @{ 'X-Tenant-Id' = $tenantA }
$csrf = Invoke-RestMethod "$origemA/api/v1/antiforgery-token" -Headers $cabecalhosA -SessionVariable sessaoA
$credencial = Get-Credential -UserName 'acesso@example.invalid' -Message 'Senha sintetica da conta A'
$corpo = @{
    email = $credencial.UserName
    password = $credencial.GetNetworkCredential().Password
} | ConvertTo-Json -Compress
$cabecalhosA['X-CSRF-TOKEN'] = $csrf.requestToken
try {
    $login = Invoke-RestMethod "$origemA/api/v1/sessions" -Method Post -Headers $cabecalhosA -WebSession $sessaoA -ContentType 'application/json' -Body $corpo
} finally {
    Remove-Variable corpo, credencial
}
$csrf = Invoke-RestMethod "$origemA/api/v1/antiforgery-token" -Headers @{ 'X-Tenant-Id' = $tenantA } -WebSession $sessaoA
$cabecalhosA['X-CSRF-TOKEN'] = $csrf.requestToken
Invoke-RestMethod "$origemA/api/v1/me" -Headers $cabecalhosA -WebSession $sessaoA
Invoke-RestMethod "$origemA/api/v1/context" -Headers $cabecalhosA -WebSession $sessaoA
Invoke-WebRequest "$origemA/api/v1/sessions/current" -Method Delete -Headers $cabecalhosA -WebSession $sessaoA -UseBasicParsing
```

Não prosseguir se uma chamada falhar. Esperado: token CSRF, login 201 com
`tenantId/expiresAt`, identidade/contexto somente A e logout 204.
O replay do cookie capturado **somente na fixture de teste** antes do logout
retorna 401 depois do commit. Não copiar cookies reais para arquivos de evidência.
Remover objetos de credenciais limita sua retenção local, não promete zerar memória.

Repetir com B em outro jar. Portas no mesmo hostname não isolam cookies:
usar jars separados mesmo em localhost; o teste cruzado reenvia deliberadamente
cookie de A para B e deve falhar. Headers/respostas não contêm conexão ou senha.
O documento servido em `/openapi/v1.json` deve corresponder ao contrato promovido
L2, sem `x-planned-only=true` e sem operações ausentes da implementação.

## 6. Matriz de aceite L2

| Cenário | Resultado exigido |
|---|---|
| Login correto A/B, mesmo email e senhas distintas | Sessões independentes; senha A não autentica B; nova chave a cada login. |
| Header ausente/inválido/duplicado | 400 Problem Details, antes de lookup indevido. |
| Login divergente/inativo, email desconhecido/senha errada/conta bloqueada | 401 AUTH_INVALID genérico; sem enumeração nem sessão emitida. |
| Cookie A em B e header divergente em A | 401 para cookie não autenticável; 403 para header divergente válido fora do login; zero dados cruzados. |
| CSRF ausente/inválido/de identidade anterior | 400 CSRF_INVALID em login/logout; token novo após mudança de identidade. |
| Falta/revogação da concessão | `/context` 403 já na próxima operação; `/me` próprio continua sujeito à sessão válida. |
| Logout e replay / Renew concorrente | Revogação persistida antes de 204; replay 401; sem ressurreição ou extensão de prazo. |
| Novo login com sessão anterior e falha injetada no commit | 503; nenhuma sessão nova; a anterior continua válida com o mesmo `ExpiraEm`. Revogação/lockout confirmados por outra operação durante a tentativa permanecem. |
| Dois logins concorrentes com a mesma sessão anterior (barreira) | Exatamente uma troca conclui (200); a outra recebe 409 SESSION_CONFLICT sem `Set-Cookie`; uma única sessão nova no banco; sessões de outros jars intactas. Repetição com novo GET antiforgery funciona. |
| Concorrência por entidade | Conta usa `ConcurrencyStamp` (sem `Versao`); concessão simultânea/retirada simultânea gera conflito explícito no host; falhas de senha concorrentes não perdem contagem. |
| Duas sessões da mesma conta em jars distintos | Novo login/logout em uma não invalida a outra. Reset/desativação/lockout/revogação operacional invalidam ambas, sem efeito em B. |
| Expiração absoluta | Sem configuração, aos 30 minutos da criação, 401 (29:59 ainda autoriza); `TimeProvider` evita espera real no teste. |
| Prazo configurável (`Sessao__PrazoAbsolutoMinutos`) | `5` e `480` iniciam a API e expiram exatamente nesse prazo; `4`, `481`, `0`, `30.5` e texto impedem a inicialização com diagnóstico que cita a chave/faixa, sem secrets. Sessão emitida com 30, API reiniciada com 5: a sessão mantém o `ExpiraEm` original. |
| `sessao limpar`, retenção padrão | Fixture com sessões ativa, expirada e revogada em 30d+1µs, exatamente 30d e menos de 30d antes do agora: removidas somente as encerradas há mais de 30 dias; ativa e as demais intactas, inclusive com login/logout/Renew concorrentes. Um registro `Sucesso` com retenção, corte, quantidade, operador e correlação, sem ids de sessões/contas. |
| `sessao limpar`, sem elegíveis / falha | Sem elegíveis: `sucesso`, `QuantidadeRemovida=0` e um registro. Falha injetada: rollback (nenhuma removida), um registro `Falha`, saída não zero. Dois operadores distintos aparecem com seus logins. |
| `sessao limpar`, retenção inválida | `OperacaoAcesso__RetencaoSessoesDias` `0`, `366` ou não inteiro: não zero antes de conectar, `RETENCAO_INVALIDA`, nada removido; `1` e `365` aceitos. Runtime não consegue DELETE em sessões e a API não tem job de limpeza. |
| Reset/desativação/revogação em A | Próxima operação A nega; conta/sessões/concessões B permanecem idênticas. |
| Lockout | Quinta falha bloqueia por 15 minutos e revoga todas as sessões da conta atomicamente, inclusive sob concorrência. Após o bloqueio, cookie anterior continua 401; novo login funciona; conta B independente. |
| Rate limit | Sexto pedido na janela de um minuto: 429 + Retry-After; IP forjado não contorna. Testar separadamente do lockout. |
| Instalação inativa após autenticar | `/me` e `/context` retornam 409 com sessão válida; login 401 genérico. Obter CSRF e encerrar sessão própria ainda válida continua permitido, com 204 somente após commit. |
| Reativação da instalação | Sessão dentro do prazo original e não revogada retoma. Sessão expirada/revogada não retoma; tempo inativo conta no prazo fixado na emissão. |
| Banco/marker/key ring indisponível | 503 seguro; nunca 200, identidade vazia ou 401 silencioso por erro de banco. |
| Falha no commit do logout | Não confirmar 204 nem apagar cookie como se a revogação estivesse concluída. |
| Health com cookies válidos/inválidos | Nenhum lookup de sessão; liveness sem banco/chaves e readiness sem dados de conta. |
| Restart da API | Sessão ainda dentro do prazo funciona com mesmo key ring/banco; sem estender expiração. |
| Operação sem autorização/auditoria indisponível | Não zero, sem mutação; sucesso somente com auditoria e commit. |
| Dois operadores individuais autorizados | Auditoria distingue seus logins autenticados e correlações. Nome/role informado em argumento livre não pode substituir a identidade verificada. |
| Base auditável de contas/permissões/sessões | Novos registros têm autor/data/correlação de criação. Alterações preservam criação e registram última alteração atomicamente; histórico operacional permanece imutável. |
| Autores não autenticados | Tentativa inválida é anônima; nenhum fluxo L2 grava autoria Sistema (limpeza é do operador); email/Id enviado não forja autor. |
| Migration/roles | Banco novo e reaplicação sem perda; histórico desconhecido rejeitado; runtime sem DDL e operador sem acesso a outro banco. |
| Convenções de modelo | PK `Id*` UUID v7 com timestamp igual ao `InstanteOperacao` do relógio de teste, sem geração pelo provider; FK `FkId*`; constraints minúsculas ≤63 caracteres; instantes UTC em microssegundos; exceções (marcador, concessão, tabelas auxiliares Identity vazias) em lista fechada; nenhum relógio estático, `Guid.NewGuid()`/`CreateVersion7()` sem argumento nem `ExecuteUpdate` em entidade auditável. |
| Histórico de versão incompatível | ID de migration desconhecida bloqueia runtime/readiness e tooling, mesmo sem pendentes. Não apagar histórico para obter aprovação. |
| Development HTTP em Docker | Requisição pelo loopback do host chega normalmente via gateway Docker; modo não fica publicado para acesso remoto. HTTPS continua obrigatório fora do recorte local. |

### SC-003 — prova quantitativa, exclusivamente no host de teste

Executar **200 operações sintéticas**, 100 por instalação, com leituras e gravações
intercaladas e IDs locais iguais. Usar os mesmos controles de sessão/permissão e
persistência da L2; endpoint/entidade sintética não é distribuído na API.
Uma barreira e contadores medem **pico efetivo >=20**, com A e B sobrepostos.
Conferir commits válidos, consultas, rollback/falha provocada em A e ausência de
alteração em B. Negar todas ou apenas lançar 200 tarefas não satisfaz o critério.
Não aplicar o limiter de login às operações do cenário: autenticar antes da carga.

Registrar quantitativos/pico/resultados e correlações sem dados secretos.
SC-002 exige todos os casos da matriz aplicável; SC-005 usa só contratos publicados.

## 7. Gates e evidências

Reexecutar os comandos da seção 1 após implementar L2. Além deles:

```powershell
dotnet package list .\backend\Boilerplate.slnx --vulnerable --include-transitive --format json
gitleaks git --redact --exit-code 1 .
```

Usar o scanner fixado/checksum e os critérios de bloqueio do workflow existente;
listar vulnerabilidades sem interpretar severidade não prova aprovação.
Zero testes, scan ausente/inconclusivo ou falha de gate nunca aprova.
Exercícios negativos controlados de compilação/formatação/acesso/persistência/
isolamento/scans devem falhar sem adicionar secrets reais ou dependência vulnerável.
Registrar bloqueio real dos checks obrigatórios; configuração de proteção exige
permissão do mantenedor e não é presumida por existir YAML.

Evidências: revisão testada/SDK selecionado, suites/quantidades, migration inicial,
matriz SC-002, 200/100/100/pico SC-003, consumidor SC-005, gates SC-006 e docs SC-008.
L3/L4 (frontend, módulos opcionais, publicação e recuperação real) não são
executadas nem declaradas concluídas neste roteiro.
