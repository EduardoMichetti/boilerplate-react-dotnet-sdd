<!--
Template de PR (FR-028; Constituição, "Fluxo de desenvolvimento e resolução de conflitos").
Preencha todas as seções. Não cole segredos, cookies, strings de conexão, senhas ou
dados pessoais reais em nenhuma parte deste PR, inclusive nas evidências.
-->

## Objetivo

<!-- O que muda e por quê. Referencie spec/plan/tasks (ex.: specs/001-.../tasks.md T123). -->

## Riscos

<!-- Riscos de segurança, isolamento entre tenants, integridade de dados, compatibilidade
e operação, com a mitigação de cada um. Escreva "nenhum identificado" só após avaliar. -->

## Testes executados e evidências

<!-- Comandos executados e resultado (SDK de `dotnet --version`, quantidade por suíte,
gates de CI). Evidência é resultado de execução, não a existência de arquivos de teste. -->

- [ ] `dotnet restore backend/Boilerplate.slnx --locked-mode`
- [ ] `dotnet build backend/Boilerplate.slnx --no-restore`
- [ ] `dotnet format backend/Boilerplate.slnx --verify-no-changes --no-restore`
- [ ] `dotnet test --solution backend/Boilerplate.slnx --no-restore --minimum-expected-tests 1`
- [ ] Outros (Compose, A/B locais, cenários da matriz do quickstart):

## Documentação afetada

<!-- README, quickstart, contracts/operations.md, ADRs, data-model, plan. Liste os arquivos
atualizados ou justifique por que nenhuma documentação muda. -->

## Impacto em contratos/OpenAPI

<!-- Operações, esquemas, códigos de erro ou comandos do host operacional alterados.
Alteração incompatível: consumidores afetados e estratégia de transição (Constituição V).
O contrato promovido (contracts/openapi.json) e /openapi/v1.json entram no mesmo PR dos endpoints. -->

## Impacto em tenants

<!-- Efeito sobre isolamento por instalação, marcador do banco, migrations, roles/grants,
sessões e dados existentes. Migration nova: como foi validada em A e B. -->

## Dependências novas

<!-- Pacote, versão, motivo, alternativa considerada e ADR/aprovação do mantenedor
(Constituição X). Escreva "nenhuma" quando não houver. -->

| Pacote | Versão | Projeto | Justificativa | ADR/aprovação |
|---|---|---|---|---|
|  |  |  |  |  |

## Critérios de qualidade (Constituição, "Restrições e critérios de qualidade")

<!-- Marque os atendidos com a evidência. Item não aplicável DEVE ter justificativa.
Isolamento entre tenants, proteção de secrets, autorização e integridade de dados não
admitem exceção por prazo. -->

| Critério | Evidência exigida | Status | Evidência ou justificativa de não aplicável |
|---|---|---|---|
| Arquitetura e módulos | Limites documentados, dependências sem ciclos e contratos explícitos. | ☐ atende ☐ N/A |  |
| Stack e tipos | Versões suportadas, builds e verificação de tipos sem erros. | ☐ atende ☐ N/A |  |
| Tenant e autorização | Testes de isolamento e acesso negado para operações afetadas. | ☐ atende ☐ N/A |  |
| Persistência | Integração com PostgreSQL e validação de migrations quando alteradas. | ☐ atende ☐ N/A |  |
| API | OpenAPI atualizado e testes de entradas, respostas e erros afetados. | ☐ atende ☐ N/A |  |
| Segurança | Verificação de secrets, permissões e vulnerabilidades com triagem registrada. | ☐ atende ☐ N/A |  |
| Regressão | Testes proporcionais ao risco e comportamento anterior preservado ou migrado. | ☐ atende ☐ N/A |  |
| Operação | Compose validado quando afetado; evidências de deploy e recuperação antes da produção. | ☐ atende ☐ N/A |  |
| Documentação | Instruções, decisões e contratos afetados atualizados. | ☐ atende ☐ N/A |  |

<!-- Mudança exclusivamente documental dispensa build/testes de aplicação, mas exige revisão
de consistência, links, regras e ausência de placeholders indevidos. -->

## Exceções temporárias

<!-- Somente para regras não críticas: justificativa, riscos, mitigação, responsável,
prazo de validade e tarefa de correção, com aprovação do mantenedor e do revisor.
Escreva "nenhuma" quando não houver. -->
