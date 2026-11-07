# ADR 0003: Tratamento de erros, validação de entrada e códigos de erro

**Data**: 2026-11-07
**Status**: proposto; implementação e aceite pendentes.
**Contexto**: [Constituição V e X](../../.specify/memory/constitution.md),
[research D08](../../specs/001-fullstack-foundation/research.md),
[operations.md §4](../../specs/001-fullstack-foundation/contracts/operations.md),
[future-openapi.json](../../specs/001-fullstack-foundation/contracts/future-openapi.json),
[ConfiguracaoProblemDetails.cs](../../backend/src/App.Api/Diagnostics/ConfiguracaoProblemDetails.cs).

## Contexto

A I1 já trata exceções não tratadas com `IExceptionHandler` (`ManipuladorExcecaoSeguro`),
respondendo Problem Details 500/503 com `code` e `traceId`, sem expor mensagem, stack ou
conexão. Faltava decidir como validar a entrada HTTP, como o serviço comunica falhas de
negócio esperadas, o formato dos códigos de erro para os módulos de negócio, os status
HTTP correspondentes, quem define o texto exibido e a rigidez da desserialização JSON.

A referência anterior do time (Portal360, `ResultError`/`ResultSchema`) usa códigos
numéricos `E + Operação + Módulo + Id` num catálogo único e mistura falhas internas
(`ErroInternoCriarPlanoAcao`) com regras de negócio. O catálogo apresenta prefixos
incoerentes, códigos reaproveitados para erros distintos e mensagens que interpolam
dados de entrada, e o `ResultError` não carrega informação para escolher o status HTTP.

## Decisão

### D1 — Três categorias de falha, um mecanismo para cada

| Categoria | Mecanismo | Resposta |
|---|---|---|
| Entrada malformada | Desserialização rígida (D6) + FluentValidation (D2) | 400 `INPUT_INVALID` + `errors` |
| Regra de negócio esperada | `Resultado<T>` (D3) | 403, 404, 409 ou 422 com código do módulo |
| Falha inesperada (bug, infraestrutura, invariante) | Exceção `Excecao*` → `ManipuladorExcecaoSeguro` | 500 `INTERNAL_ERROR` / 503 `DEPENDENCY_UNAVAILABLE` |

Critério: se o usuário pode provocar com uso normal, é `Resultado`; se só ocorre por bug
ou infraestrutura, é exceção. Falha interna nunca é catalogada como erro de negócio.

### D2 — FluentValidation para o formato da entrada

- Um `AbstractValidator<T>` por DTO de entrada, em `Platform.Application`. DTO de
  entrada é todo tipo com prefixo `Requisicao` (ex.: `RequisicaoCriarPlanoAcao`); seu
  validador se chama `Validador<NomeDoDto>`.
- Escopo restrito ao que se verifica sem banco e sem relógio: obrigatoriedade, tamanho,
  formato, coerência entre campos do próprio payload e duplicidade em coleções.
  Existência, permissão, datas relativas a "hoje" e demais regras de domínio ficam no
  serviço (Constituição V; relógio via `TimeProvider`).
- Um endpoint filter aplicado ao grupo `/api/v1` resolve automaticamente o
  `IValidator<T>` de cada corpo de requisição; nenhum endpoint precisa registrá-lo.
- Teste de arquitetura: todo tipo `Requisicao*` tem `IValidator<T>` registrado; validadores não usam
  `MustAsync`/`CustomAsync` nem dependem de repositório, `DbContext` ou `TimeProvider`.
- Dependência nova justificada pela Constituição X: regras condicionais, entre campos e
  em coleções aninhadas são frequentes nos módulos previstos; mensagens pt-BR nativas;
  testes com `TestValidate`; time já familiarizado. Versão `12.2.0` (NuGet, publicada em
  2026-10-10), licença Apache-2.0, alvo `net8.0`+, compatível com `net10.0`.
  Alternativa nativa `AddValidation()` (.NET 10) avaliada: sem dependência, porém
  mensagens padrão em inglês e regras condicionais/entre campos verbosas via
  `IValidatableObject`.

### D3 — `Resultado<T>` próprio para falhas de negócio

- Tipos em `Platform.Domain/Resultados`, sem dependências:
  `Erro(Codigo, Mensagem, TipoErro)`, `TipoErro { NaoEncontrado, Conflito, RegraNegocio,
  AcessoNegado }`, `Resultado<T>` com conversões implícitas a partir de `T` e de `Erro`,
  e `Resultado` não genérico, com a mesma API, para operações sem valor de retorno
  (`Resultado.Ok()`; sucesso costuma virar 204).
- O acesso principal é `Corresponder(sucesso, falha)`; ler `Valor` numa falha lança
  `InvalidOperationException`.
- Cada módulo declara seus erros num catálogo estático `Erros<Modulo>`
  (ex.: `ErrosPlanoAcao.JaCancelado`). O `TipoErro` é definido no catálogo, uma vez.
- `App.Api` traduz `Erro` em Problem Details num único ponto (`ConfiguracaoProblemDetails.DeErro`).
- Invariantes de entidade continuam lançando `Excecao*`. Conflitos de concorrência
  (`DbUpdateConcurrencyException`) são capturados na persistência e viram `Erro` do tipo
  `Conflito`.
- Rejeitadas: exceções de negócio (falha invisível na assinatura, risco de `catch`
  genérico converter falha em sucesso) e bibliotecas como ErrorOr/FluentResults
  (dependência adicional e nomes fora do Português Técnico, sem ganho relevante).

### D4 — Códigos semânticos com prefixo de módulo

- `code` é string estável em `SCREAMING_SNAKE_CASE`, padrão `^[A-Z][A-Z0-9_]+$`.
- Códigos de plataforma permanecem em inglês e sem prefixo (`INPUT_INVALID`,
  `NOT_FOUND`, `INTERNAL_ERROR`...). Códigos de domínio usam `<MODULO>_<REGRA>` em
  português (`PLANO_ACAO_JA_CANCELADO`, `TAREFA_NAO_ENCONTRADA`).
- Código publicado nunca muda de significado nem é reaproveitado; regra extinta tem seu
  código removido.
- Cada endpoint declara no OpenAPI os códigos que pode devolver. Teste garante unicidade
  e padrão de todos os códigos dos catálogos.
- Rejeitado o esquema numérico: tabela de alocação sujeita a erro, opacidade, e
  convivência de dois estilos no contrato. A necessidade de "código para o suporte" é
  atendida pelo `traceId`.

### D5 — Status HTTP

| Status | Quando |
|---|---|
| 400 `INPUT_INVALID` | Payload malformado (desserialização ou FluentValidation). |
| 403 | `TipoErro.AcessoNegado`. |
| 404 | `TipoErro.NaoEncontrado`. |
| 409 | `TipoErro.Conflito`: conflita com o estado atual do recurso. |
| 422 | `TipoErro.RegraNegocio`: viola regra independentemente do estado. |

Critério 409 × 422: "Se o recurso estivesse em outro estado, essa mesma requisição seria
aceita?" Sim → 409; não → 422. Separar 400 de 409/422 permite alertar sobre 400 como
indício de defeito no cliente.

### D6 — Mensagens

- O backend é dono do texto padrão em pt-BR (`title` e `errors`). O frontend pode
  sobrescrever por `code` num mapa opcional, com fallback para o `title` recebido.
- `errors` permanece `campo → string[]`, como em `future-openapi.json`. A chave é o
  caminho do campo com os nomes JSON em camelCase, sem prefixo `$.`
  (ex.: `tarefas[0].emailResponsavel`), tanto para erros de desserialização quanto do
  FluentValidation (configurado com resolvedor de nomes camelCase). Assim o frontend
  associa a chave diretamente ao campo do formulário.
- Mensagens não interpolam valores de entrada sensíveis (e-mail, documento, nome); o
  campo em `errors` identifica a origem.

### D7 — Desserialização JSON rígida

- `ConfigureHttpJsonOptions` com `UnmappedMemberHandling = Disallow`,
  `RespectNullableAnnotations = true` e `RespectRequiredConstructorParameters = true`.
- `RouteHandlerOptions.ThrowOnBadRequest = true` em todos os ambientes. Por padrão o
  ASP.NET Core só lança em Development; fora dele, falha de binding responde 400 sem
  corpo e nunca chega ao `IExceptionHandler`, violando o formato Problem Details.
- `ManipuladorExcecaoSeguro` converte `BadHttpRequestException` (com `JsonException`
  interna) em 400 `INPUT_INVALID`, com `errors` indexado pelo caminho do campo
  no formato da D6 (derivado de `JsonException.Path`, sem o prefixo `$.`) e mensagem genérica, sem ecoar o valor recebido nem a
  mensagem original da exceção em nenhum ambiente. Esse 400 não é registrado com
  `LogError`.
- Exceção explícita por DTO via `[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Skip)]`,
  restrita a payloads de terceiros que não controlamos. O atributo só é permitido em
  tipos de namespaces `*.Integracoes.*`; teste de arquitetura falha se aparecer em
  outro lugar.
- Remoção de campo de entrada passa por um ciclo com a propriedade marcada `[Obsolete]`.
- Rejeitado o modo permissivo: campo opcional com nome errado é descartado com sucesso
  aparente; `null` em tipo não anulável vira 500 em vez de 400; e o contrato já declara
  `additionalProperties: false`.

## Consequências

- `ConfiguracaoProblemDetails` ganha fábricas para 400 (validação e desserialização),
  403, 404, 409 e 422; hoje `Criar` aceita apenas 404, 500 e 503.
- `future-openapi.json` passa a tratar o enum de `code` como catálogo da plataforma,
  estendido por módulo, e a documentar 422 onde aplicável.
- Nova dependência `FluentValidation` (e `FluentValidation.DependencyInjectionExtensions`)
  `12.2.0` em `Platform.Application`/`App.Api`, sujeita à aprovação prevista na
  Constituição X; fixada em `Directory.Packages.props` com lockfiles atualizados.
- Implementação planejada no bloco P1c do [plano L2](../../specs/001-fullstack-foundation/plan.md).
- Novos testes: contrato (formato de 400/409/422, ausência de valor ecoado), arquitetura
  (validador por `Requisicao*`, validador sem I/O, padrão e unicidade de códigos,
  `JsonUnmappedMemberHandling.Skip` só em `*.Integracoes.*`) e unitários dos
  validadores e de `Resultado<T>`.
- Constituição 2.3.0 (princípio V) incorpora as obrigações tecnológicas neutras desta
  ADR: categorias de falha, códigos estáveis, entrada estrita e mensagens sem valores
  sensíveis. Esta ADR fixa os mecanismos concretos.
