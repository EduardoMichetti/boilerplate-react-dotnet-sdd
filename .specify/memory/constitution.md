# boilerplate-react-dotnet-sdd Constitution

## Core Principles

Esta Constituição rege o `boilerplate-react-dotnet-sdd`, um boilerplate full-stack reutilizável
para aplicações profissionais. **DEVE**, **DEVEM**, **NÃO DEVE** e **NÃO DEVEM** expressam
obrigações verificáveis. Seu conteúdo consolida o estudo em [Resumo.md](../../Resumo.md).
Estes princípios orientam especificações e implementações futuras; sua ratificação
não autoriza nem exige implementar funcionalidades nesta etapa.

### I. Arquitetura modular

- O backend DEVE começar como monólito modular, com módulos executados na mesma
  aplicação e limites explícitos de responsabilidade.
- As responsabilidades de Domain, Application, Infrastructure e API DEVEM estar
  separadas. A organização em projetos ou pastas DEVE ser definida no plano técnico
  e permitir verificar as dependências entre essas camadas.
- Domain DEVE conter regras de negócio sem depender de persistência, HTTP ou
  infraestrutura. Application DEVE coordenar casos de uso usando Domain e abstrações;
  Infrastructure DEVE implementar integrações e persistência; API DEVE tratar
  transporte, autenticação, validação de entrada e composição da aplicação.
- Dependências DEVEM apontar para o domínio e as abstrações da aplicação. Referências
  à infraestrutura na API DEVEM ficar restritas à composição e integração técnica;
  regras de negócio NÃO DEVEM residir em controllers ou componentes do frontend.
- Microsserviços NÃO DEVEM ser introduzidos por antecipação. A extração de um módulo
  exige necessidade comprovada, ADR e avaliação dos custos operacionais.

**Verificação:** o plano e a revisão DEVEM identificar as camadas, seus limites e as
referências permitidas; o grafo de dependências NÃO DEVE conter ciclos.

### II. Reutilização e contratos explícitos

- Cada módulo de negócio DEVE declarar seu propósito, dados sob sua responsabilidade,
  contratos públicos e dependências.
- Comunicação entre módulos DEVE ocorrer por interfaces, mensagens ou contratos
  explícitos. Chamadas em processo são suficientes no monólito; REST entre aplicações
  independentes DEVE ter contrato OpenAPI.
- Um módulo NÃO DEVE consultar ou alterar diretamente tabelas, entidades internas,
  repositórios ou implementações de outro módulo. Dados de outro módulo DEVEM ser
  obtidos por seus contratos, inclusive para consultas e relatórios.
- Componentes compartilhados DEVEM atender a uma necessidade real de reutilização.
  Regras específicas de negócio NÃO DEVEM ser deslocadas para componentes genéricos
  apenas para contornar limites entre módulos.
- Produtos derivados DEVEM poder selecionar módulos sem dependências ocultas.
  Compatibilidade ou migração dos consumidores DEVE acompanhar alterações de contratos.

**Verificação:** a revisão DEVE conferir consumidores, contratos e ausência de acesso
às implementações internas. Contratos alterados DEVEM ter testes dos usos afetados.

### III. Stack tecnológica

- O frontend DEVE utilizar React, TypeScript, Vite e Tailwind CSS.
- O backend DEVE utilizar uma versão .NET LTS com suporte ativo, ASP.NET Core,
  Entity Framework Core e PostgreSQL, com provedor compatível com as versões escolhidas.
- A versão concreta da stack DEVE ser registrada no plano técnico e nos mecanismos
  de controle de versões do repositório, incluindo SDK, dependências e lockfiles
  aplicáveis. Instalações e builds NÃO DEVEM depender de versões flutuantes.
- TypeScript DEVE manter verificação estrita de tipos. Supressões de diagnósticos ou
  perda deliberada de tipagem DEVEM ter justificativa localizada e revisão.
- Atualizações DEVEM preservar compatibilidade e passar pelas validações afetadas.
  A migração para uma versão suportada DEVE ocorrer antes do fim do suporte utilizado.

**Verificação:** manifests, configuração de SDK e lockfiles DEVEM corresponder à stack
declarada; builds e verificação de tipos DEVEM passar sem erros.

### IV. Isolamento por cliente com implantação dedicada (single-tenant por deploy)

- Cada cliente/tenant DEVE possuir implantação lógica e banco PostgreSQL exclusivos
  (1 instância de aplicação + 1 banco da própria aplicação por instalação). O código
  e as imagens são reutilizados, mas processos, configurações, segredos e dados NÃO
  DEVEM ser compartilhados entre clientes.
- NÃO DEVE existir banco apartado de catálogo central compartilhado entre clientes;
  cada instância do backend DEVE conhecer exclusivamente o banco e os segredos da
  sua própria instalação, fornecidos por variáveis de ambiente/secrets do ambiente
  (como Dokploy na VPS ou cofre de segredos).
- O backend DEVE validar a identidade da própria instalação e do banco configurado
  antes de operar, impedindo que uma configuração incorreta aponte a instância do
  cliente A para o banco do cliente B. O cliente HTTP NÃO DEVE fornecer credenciais,
  strings de conexão ou escolher bancos de dados.
- Em operações autenticadas, o backend DEVE validar identidade, estado da conta,
  sessão vinculada à instalação e permissões antes de executar a operação. Operações
  anônimas necessárias, como login, DEVEM ter escopo e controles documentados, sem
  liberar acesso a dados protegidos.
- Configuração obrigatória ausente, banco de outra instalação, instância inativa ou
  conexão indisponível DEVE causar erro explícito, sem fallback para outro banco.
- Jobs, exportações, arquivos, caches e outras operações fora de requisições HTTP
  DEVEM permanecer restritos à própria instalação e ao seu banco configurado.
- Provisionamento, migrations, backup e restauração DEVEM identificar explicitamente
  a instalação e o banco-alvo, registrando resultados por cliente sem afetar outras
  instalações.

**Verificação:** testes automatizados DEVEM usar pelo menos dois bancos PostgreSQL
distintos (simulando duas instalações de clientes) e comprovar isolamento de dados,
contas e sessões, bloqueio quando uma instância é apontada para o banco de outra,
acesso negado e execução concorrente segura. Isolamento é requisito de liberação,
não melhoria futura.

### V. API REST e contratos documentados

- A API DEVE utilizar REST, semântica consistente de métodos e códigos HTTP, e
  contratos OpenAPI que reflitam os endpoints, payloads, autenticação e respostas.
- Toda entrada externa DEVE ser validada no backend antes de uso. Validação no
  frontend complementa a experiência, mas NÃO DEVE substituir controles do servidor.
- Regras de domínio DEVEM ser verificadas na camada apropriada, independentemente do
  consumidor ou meio de transporte.
- Erros HTTP DEVEM seguir formato consistente baseado em Problem Details, com código
  ou tipo estável e identificador de correlação quando aplicável.
- Erros e logs NÃO DEVEM expor secrets, strings de conexão, stack traces em produção
  ou dados de outros tenants. Falhas NÃO DEVEM ser convertidas em sucesso aparente.
- Falhas DEVEM ser tratadas por categoria: entrada malformada é rejeitada antes de uso
  com erro por campo; regra de negócio esperada é comunicada por resultado explícito na
  assinatura do caso de uso; falha inesperada ou de infraestrutura é exceção tratada de
  forma centralizada. Falha interna NÃO DEVE ser catalogada como erro de negócio.
- Códigos de erro publicados DEVEM ser estáveis: NÃO DEVEM mudar de significado nem ser
  reaproveitados, e DEVEM ser únicos e declarados uma única vez no código-fonte.
- A desserialização de entrada DEVE ser estrita: campos desconhecidos, tipos
  incompatíveis e nulos em campos obrigatórios DEVEM ser rejeitados. Exceção somente
  para payloads de terceiros não controlados, isolada e verificável.
- Mensagens de erro NÃO DEVEM interpolar valores de entrada sensíveis; o campo
  afetado identifica a origem.
- Mudanças incompatíveis DEVEM declarar consumidores afetados e estratégia de
  versionamento ou migração antes da publicação.

**Verificação:** testes de contrato ou integração DEVEM conferir respostas válidas,
entradas inválidas, autenticação, autorização e formato de erro, incluindo rejeição
de campos desconhecidos e ausência de valores de entrada ecoados; testes de
arquitetura DEVEM verificar unicidade e padrão dos códigos de erro; OpenAPI DEVE ser
atualizado no mesmo conjunto de alterações do contrato.

### VI. Segurança e menor privilégio

- Cada produto DEVE definir autenticação e autorização conforme seus riscos, atores
  e operações. A escolha de mecanismos DEVE ser documentada, sem inventar protocolos
  criptográficos ou depender apenas da interface para proteger operações.
- Endpoints protegidos DEVEM negar acesso por padrão e verificar permissões no
  backend, incluindo recursos e vínculo com o tenant. Estar autenticado NÃO DEVE
  ser tratado como autorização suficiente.
- Secrets NÃO DEVEM ser armazenados em código, histórico Git, imagens, logs ou
  frontend. Configurações e bundles do frontend, inclusive variáveis `VITE_*`,
  DEVEM ser tratados como públicos.
- Secrets DEVEM ser fornecidos por configuração segura em tempo de execução ou
  mecanismo apropriado ao ambiente. Arquivos de exemplo DEVEM conter apenas valores
  fictícios; credenciais expostas DEVEM ser revogadas e substituídas.
- Identidades da aplicação, banco, CI e deploy DEVEM ter somente os privilégios
  necessários. Credenciais administrativas NÃO DEVEM ser usadas pela aplicação
  para operações rotineiras.
- Publicação DEVE utilizar HTTPS e proteger serviços internos contra exposição
  desnecessária. Logs DEVEM permitir investigação sem registrar dados sensíveis
  desnecessários; dados pessoais DEVEM ter tratamento documentado conforme o produto.

**Verificação:** revisão e CI DEVEM verificar exposição de secrets e dependências
vulneráveis; testes negativos DEVEM cobrir permissões e limites de acesso. Achados
DEVEM ser triados; riscos críticos ou altos não mitigados que afetem o produto
DEVEM bloquear a liberação.

### VII. Qualidade proporcional ao risco

- Cada alteração DEVE explicitar riscos e evidências de validação. Risco maior exige
  maior profundidade de testes; percentual global de cobertura NÃO DEVE substituir
  cenários críticos comprovados.
- Regras de negócio DEVEM ter testes automatizados de caminhos válidos, limites e
  violações relevantes. Defeitos corrigidos DEVEM receber teste de regressão quando
  a reprodução automatizada for viável; inviabilidade DEVE ser justificada na revisão.
- Persistência e isolamento DEVEM ter testes de integração com PostgreSQL real,
  descartável ou dedicado a testes, sem depender somente de mocks ou banco em memória.
- Operações de persistência DEVEM testar efeitos gravados, restrições e comportamento
  de transações e falhas quando aplicável. Migrations DEVEM ser validadas na criação
  de banco e na atualização de uma versão anterior suportada.
- Fluxos críticos de autenticação, autorização e jornada do usuário DEVEM ter testes
  de integração ou ponta a ponta conforme a superfície afetada.
- Testes DEVEM ser reproduzíveis, independentes de dados de produção e executáveis
  pela automação do repositório.

**Verificação:** a revisão DEVE relacionar riscos aos testes; CI DEVE executar builds,
verificação de tipos, lint e testes pertinentes, bloqueando integração em caso de falha.

### VIII. Infraestrutura reproduzível

- Desenvolvimento DEVE utilizar Docker e Docker Compose, com versões de imagens
  controladas, configuração por ambiente e instruções executáveis a partir de clone limpo.
- A solução DEVE ser preparada para publicação em VPS Linux com Dokploy. Configuração
  de deploy, variáveis exigidas, volumes, health checks e procedimento de atualização
  DEVEM ser documentados e versionados sem secrets.
- Dados PostgreSQL DEVEM persistir em volumes apropriados. Publicação DEVE separar
  exposição pública de serviços internos e terminar HTTPS de forma documentada;
  Traefik é a referência do estudo, não motivo para adicionar proxies redundantes.
- Backups DEVEM ser planejados por banco, com cópia externa à VPS. Frequência,
  retenção, metas de recuperação e responsáveis DEVEM ser definidos antes da produção.
- Restauração DEVE ser testada antes da primeira liberação e em periodicidade
  documentada. Deploys e migrations DEVEM ter plano de recuperação compatível com
  alterações de dados; reverter somente a imagem NÃO DEVE ser presumido suficiente.

**Verificação:** o roteiro de ambiente limpo DEVE iniciar os serviços e confirmar sua
saúde; antes da produção, DEVEM existir evidências de publicação e restauração.

### IX. Manutenibilidade e Padrão de Nomenclatura (Português Técnico)

- Implementações DEVEM adotar a solução mais simples que satisfaça os requisitos
  aprovados, com nomes claros, convenções consistentes e responsabilidades coesas.
- O código-fonte (classes, interfaces, métodos, propriedades, variáveis, mensagens e
  testes) DEVE utilizar **Português Técnico**, mantendo apenas os nomes de pastas físicas
  em inglês e preservando jargões técnicos universais de arquitetura (como `Tenant`,
  `DbContext`, `Snapshot`, `Token`, `Claim`, `Hash`, `Preflight`, `Liveness`, `Readiness`).
  Termos artificiais como `Inquilino` (no lugar de `Tenant`), `Instantaneo` (no lugar de
  `Snapshot`) ou `ContextoBanco` (no lugar de `DbContext`) NÃO DEVEM ser utilizados.
- Identificadores DEVEM sempre iniciar pelo prefixo **`Id`** (ex.: `IdTenant`, `IdAmbiente`,
  `IdUsuario`, `IdSessao`). Para registros singleton, DEVE-SE utilizar `IdUnico` e
  `IdUnicoPadrao`.
- Na persistência, a PK de cada tabela DEVE se chamar `Id<Entidade>` e a coluna de
  chave estrangeira DEVE se chamar `FkId<Entidade>[Papel]` (ex.: `FkIdUsuario`,
  `FkIdUsuarioResponsavel`). Referência sem constraint de FK mantém `Id*`. O prefixo
  `Fk` NÃO DEVE vazar para Domain, Application ou DTOs. Propriedades herdadas de
  frameworks que não podem ser renomeadas (ex.: Identity `Id`/`UserId`) DEVEM ter a
  coluna mapeada para o padrão.
- Tabelas usam snake_case; colunas usam o nome PascalCase da propriedade. Constraints
  e índices DEVEM usar minúsculas com prefixo do tipo (`pk_`, `fk_`, `ix_`, `uq_`,
  `ck_`), aplicados por convenção central e com até 63 caracteres.
- **Id é chave técnica, nunca código de negócio.** Campos `Id*` persistidos DEVEM ser
  substitutos opacos, gerados pelo sistema, imutáveis e sem significado de negócio;
  servem para chave primária, chave estrangeira e correlação técnica. NÃO DEVEM ser
  digitados ou escolhidos pelo usuário, derivados de dados de negócio, reutilizados,
  nem apresentados na interface como código, número ou referência de negócio.
- O tipo padrão de `Id*` persistido é **UUID v7** (`Guid` gerado no servidor, ordenável
  no tempo, verificado por teste). Inteiro somente para `IdUnico` de singleton ou para
  tabela interna justificada no plano que nunca seja exposta fora do backend.
- Códigos e números visíveis ao usuário (ex.: código do produto, número do pedido)
  DEVEM ser colunas próprias `Codigo*`/`Numero*`, com unicidade e validação explícitas
  no banco da instalação; chaves estrangeiras DEVEM referenciar `Id*`, nunca
  `Codigo*`/`Numero*`. A API PODE transportar `Id*` como referência opaca entre
  sistemas, mas busca e exibição de negócio usam o código próprio.
- Exceção única: código **declarado no código-fonte** em catálogo fechado (ex.:
  código de permissão), sem tabela de catálogo, PODE compor a chave de tabela de
  associação. Esse código DEVE ser único no catálogo, ter formato validado no banco,
  ser rejeitado quando desconhecido e NUNCA ser reutilizado ou renomeado sem migration
  de dados. Toda exceção DEVE ser registrada no plano.
- **Data e hora seguem um padrão único.** Instantes persistidos ou trafegados DEVEM ser
  `DateTimeOffset` em UTC (offset zero), gravados em `timestamptz` e normalizados para
  precisão de microssegundos. O instante atual DEVE vir exclusivamente de `TimeProvider`
  injetado; `DateTime.Now`, `DateTime.UtcNow`, `DateTimeOffset.Now`,
  `DateTimeOffset.UtcNow` e `DateTime.Today` NÃO DEVEM ser usados em código de produção.
  Colunas NÃO DEVEM obter o instante por `now()`/default do banco, nem consultas
  comparar com o relógio do banco; o instante da aplicação é passado como parâmetro.
  `DateOnly` só representa data de calendário sem hora. Conversão para fuso de
  exibição pertence à interface, não ao backend ou à persistência.
- Classes de exceção DEVEM utilizar o prefixo **`Excecao`** em português (ex.:
  `ExcecaoIsolamentoTenant`, `ExcecaoConfiguracaoInstalacao`), sem o sufixo `Exception`.
- A configuração de conexões de banco DEVE utilizar de forma padronizada e exclusiva a
  nomenclatura **`StringsConexao`** (ex.: `StringsConexao:BancoApp` / `StringsConexao__BancoApp`),
  evitando misturar `ConnectionStrings` e `StringsConexao` no projeto.
- Todas as entidades e modelos persistidos DEVEM possuir descrições documentadas em
  cada campo (via comentários XML `/// <summary>` no código e `.HasComment(...)` no
  mapeamento do EF Core / migrations).
- Convenções de estrutura, formatação e tratamento de erros DEVEM ser documentadas
  e aplicadas por ferramentas existentes quando disponíveis.
- Abstrações DEVEM representar necessidades presentes, não possibilidades futuras.
  Comentários DEVEM explicar decisões não evidentes, sem apenas repetir o código.
- Decisões arquiteturais relevantes DEVEM ser registradas em ADRs com contexto,
  alternativas, decisão e consequências, incluindo efeitos sobre segurança e operação.
- Alterações DEVEM atualizar contratos e documentação diretamente afetados, no mesmo
  conjunto de trabalho. Refatorações alheias ao objetivo NÃO DEVEM ampliar seu escopo.

**Verificação:** a revisão DEVE conferir legibilidade, aderência ao Português Técnico e
às regras de nomenclatura (`Id*`, `Excecao*`, `StringsConexao`, descrições de campos),
testes de modelo que provem PK em `Id*`, FK em `FkId*` (ou exceção registrada),
UUID v7 e nomes de constraints/índices no padrão,
teste de arquitetura que barre APIs de relógio proibidas em código de produção,
limites de responsabilidade e ADRs necessários, sem aceitar complexidade sem justificativa.

### X. Evolução controlada

- Nova tecnologia ou dependência DEVE responder a necessidade concreta e documentada,
  com análise de alternativas existentes, compatibilidade, licença, manutenção,
  segurança e custo operacional.
- Dependências DEVEM ser adicionadas somente após aprovação dessa justificativa na
  revisão. Impactos arquiteturais exigem ADR; mudanças na stack obrigatória exigem
  emenda desta Constituição.
- Mensageria, microsserviços, aplicativos mobile e outras extensões NÃO DEVEM ser
  implementados apenas por constarem como possibilidades no estudo.
- Mudanças DEVEM ser incrementais, preservar comportamento contratado ou explicitar
  migração, e permitir validar o benefício que motivou sua adoção.

**Verificação:** a revisão DEVE localizar a necessidade, a justificativa e as evidências
de compatibilidade para cada adição; itens sem motivação concreta DEVEM ser rejeitados.

## Restrições e critérios de qualidade

O estudo define a direção do projeto, não um catálogo obrigatório de funcionalidades.
Nomes de módulos, diretórios e produtos derivados são propostas a detalhar no plano.
A Constituição NÃO DEVE ser interpretada como autorização para criar antecipadamente
módulos de negócio, aplicação mobile, mensageria ou serviços independentes.

Uma alteração de implementação somente pode ser aceita quando atender aos critérios
aplicáveis abaixo. Itens não aplicáveis DEVEM ter justificativa registrada na revisão.

| Critério              | Evidência exigida                                                                      |
| --------------------- | -------------------------------------------------------------------------------------- |
| Arquitetura e módulos | Limites documentados, dependências sem ciclos e contratos explícitos.                  |
| Stack e tipos         | Versões suportadas, builds e verificação de tipos sem erros.                           |
| Tenant e autorização  | Testes de isolamento e acesso negado para operações afetadas.                          |
| Persistência          | Integração com PostgreSQL e validação de migrations quando alteradas.                  |
| API                   | OpenAPI atualizado e testes de entradas, respostas e erros afetados.                   |
| Segurança             | Verificação de secrets, permissões e vulnerabilidades com triagem registrada.          |
| Regressão             | Testes proporcionais ao risco e comportamento anterior preservado ou migrado.          |
| Operação              | Compose validado quando afetado; evidências de deploy e recuperação antes da produção. |
| Documentação          | Instruções, decisões e contratos afetados atualizados.                                 |

Mudanças exclusivamente documentais dispensam builds e testes de aplicação, mas DEVEM
ser revisadas quanto à consistência, links, regras e ausência de placeholders indevidos.
Critérios de produção NÃO DEVEM bloquear etapas que ainda não publicam a aplicação;
DEVEM permanecer como requisitos explícitos para a liberação correspondente.

## Fluxo de desenvolvimento e resolução de conflitos

- Funcionalidades DEVEM partir de especificação com escopo, riscos e critérios de
  aceitação. O plano DEVE verificar conformidade com esta Constituição antes de gerar
  tarefas; implementação e revisão DEVEM repetir essa verificação nas superfícies afetadas.
- Pull requests DEVEM informar objetivo, riscos, testes executados, documentação e
  impacto sobre contratos e tenants. O responsável pela revisão DEVE conferir as
  evidências, não apenas a existência de arquivos de teste.
- Automação de CI, inicialmente com GitHub Actions conforme o estudo, DEVE executar
  os critérios automatizáveis pertinentes. Falhas NÃO DEVEM ser ignoradas para
  acelerar a integração.
- Conflitos DEVEM seguir esta prioridade: **segurança, isolamento e integridade de
  dados; conformidade contratual e manutenibilidade; velocidade de entrega**.
- Sob pressão de prazo, a equipe DEVE reduzir escopo, dividir a entrega ou adiar itens
  opcionais. NÃO DEVE remover validações, autorização, isolamento ou testes críticos
  para obter uma entrega aparentemente concluída.
- Entre alternativas igualmente seguras e conformes, DEVE prevalecer a opção mais
  simples e fácil de manter. Otimizações e abstrações adicionais exigem necessidade
  mensurável ou requisito concreto.
- Exceções temporárias a regras não críticas exigem registro com justificativa,
  riscos, mitigação, responsável, prazo de validade e tarefa de correção, aprovado
  pelo mantenedor e pelo revisor. Prazo vencido DEVE bloquear entregas dependentes.
- Isolamento entre tenants, proteção de secrets, autorização de operações protegidas
  e integridade de dados NÃO admitem exceção por prazo ou conveniência.

## Governance

- Esta Constituição prevalece sobre planos, tarefas, convenções e o estudo quando
  houver conflito. Divergências DEVEM ser resolvidas antes de implementar ou integrar
  a decisão conflitante; não por descumprimento silencioso.
- Emendas DEVEM ser propostas em pull request com motivação, texto alterado, impacto,
  alternativas e plano de transição para artefatos ou implementações existentes.
  A aprovação explícita do mantenedor responsável DEVE ocorrer antes da vigência.
- A versão DEVE seguir SemVer: **MAJOR** para remoções ou redefinições incompatíveis
  de princípios; **MINOR** para novos princípios, seções normativas ou ampliação
  material de obrigações; **PATCH** para esclarecimentos sem mudança de obrigação.
  A primeira ratificação é **1.0.0**, pois não havia Constituição adotada anteriormente.
- A data de ratificação original DEVE ser preservada. Cada emenda DEVE atualizar a
  data da última alteração em formato ISO `YYYY-MM-DD` e registrar o impacto da versão.
- Toda especificação, plano e revisão de implementação DEVE conferir as regras
  aplicáveis. Antes de cada liberação, o mantenedor DEVE confirmar critérios de
  produção, exceções vigentes e evidências de segurança e isolamento.
- Descumprimentos DEVEM ser registrados e corrigidos antes da integração ou liberação
  afetada. Exceções válidas seguem exclusivamente o procedimento definido acima.
- Templates e comandos do Spec Kit consultam esta Constituição em tempo de execução.
  Sua atualização NÃO DEVE modificar esses artefatos nem executar intenções de
  implementação ou deploy sem solicitação própria.

**Version**: 2.3.0 | **Ratified**: 2026-10-12 | **Last Amended**: 2026-11-07
