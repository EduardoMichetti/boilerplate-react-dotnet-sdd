# Feature Specification: Fundação full-stack reutilizável

**Feature Branch**: `main` (nenhuma branch criada por esta especificação)

**Created**: 2026-10-12

**Status**: Draft

**Input**: User description: "Definir a fundação padronizada, segura, testável e
reutilizável de aplicações profissionais, com interface web, acesso por empresa,
persistência isolada, integração documentada, execução reproduzível, validação
automatizada e preparação para publicação. Nome confirmado:
boilerplate-react-dotnet-sdd. Escopo confirmado: somente fundação técnica,
sem funcionalidades de módulos de negócio. Não implementar código nesta etapa."

**Governance**: [Constituição do projeto](../../.specify/memory/constitution.md).

**Primeiro incremento confirmado**: backend iniciado e executável, com persistência
e migration para o cadastro de usuários/acesso, sem telas ou painel administrativo.
As demais capacidades da fundação descritas abaixo pertencem a incrementos posteriores;
não são pré-condições para concluir esse primeiro recorte.
Essa separação não adia os controles constitucionais aplicáveis ao backend:
execução local por Docker Compose, testes de persistência/isolamento e checks mínimos
de integração continuam obrigatórios.

**Recorte vigente L2/I2**: planejamento sobre a L1/I1 entregue, sem reabrir seu
histórico. L2 cobre entrada/saída HTTP, sessões revogáveis locais, consulta da
própria identidade/contexto e manutenção restrita de contas/permissões com
auditoria. Não inclui frontend, painel, módulo de negócio ou publicação real.
SC-002/SC-003/SC-005 definem o aceite funcional da L2; SC-006/SC-008 acompanham
os controles aplicáveis, e SC-009 permanece regressão da base, aplicada à migration
inicial única do modelo completo (construção do zero, sem dados a preservar).
Interface/extensão modular continuam L3/I3; publicação/recuperação, L4/I4.

## Clarifications

### Session 2026-11-08

- Q: Qual deve ser o limite máximo configurável para o prazo absoluto da sessão em produtos derivados (o padrão continua 30 minutos)? → A: Opção B. Faixa permitida de 5 a 480 minutos (8 horas), limites inclusivos; padrão de 30 minutos quando ausente; valor fora da faixa ou não inteiro impede a inicialização com diagnóstico seguro.
- Q: Por quantos dias, por padrão, sessões vencidas ou revogadas devem ser mantidas antes que o comando de limpeza possa removê-las? → A: Opção B. Retenção padrão de 30 dias, configurável em dias inteiros de 1 a 365; contada a partir da expiração (sessão vencida) ou da revogação (sessão revogada); valor fora da faixa impede a execução do comando com diagnóstico seguro.
- Q: Como a execução da limpeza de sessões deve ficar registrada no histórico imutável de operações? → A: Opção A. Um registro por execução (inclusive sem remoções ou com falha), atribuído ao operador individual autenticado, com retenção aplicada, instante de corte, quantidade removida, resultado e correlação; sem identificadores de sessões ou contas removidas e sem autoria "Sistema".

### Session 2026-11-07

- Q: Se um novo login falhar ao salvar a nova sessão, a sessão anterior desse navegador deve continuar válida? → A: Sim. A troca é atômica: encerramento da sessão anterior e criação da nova são confirmados juntos. Falha preserva o estado e o prazo anteriores, sem desfazer bloqueios ou revogações concorrentes e sem confirmar novo acesso.
- Q: Quando dois logins simultâneos tentarem substituir a mesma sessão do navegador, apenas um deve conseguir concluir a troca? → A: Sim. Apenas uma substituição conclui; a tentativa concorrente retorna erro explícito sem criar sessão adicional. Nova tentativa exige o contexto atual, sem limitar sessões independentes em outros dispositivos.
- Q: Na L2, "alteração de senha" significa só a redefinição por operador autorizado ou também troca pelo próprio usuário via API? → A: Somente redefinição por operador (reset restrito que encerra todas as sessões da conta). Troca pelo próprio usuário fica para L3 ou posterior; recuperação por email/token não faz parte da feature sem novo plano.
- Q: A sessão deve durar no máximo 30 minutos desde o login, sem renovação por atividade? → A: Sim, como padrão da fundação: prazo absoluto de 30 minutos contado da criação, sem sliding, remember-me ou refresh. O valor é configurável por produto derivado dentro de um limite máximo definido no plano; valor ausente usa o padrão e valor fora do limite impede a inicialização.
- Q: Registros de sessões expiradas ou revogadas devem ser removidos automaticamente na L2? → A: Opção B. A revogação remove o ticket protegido; sessões vencidas ou revogadas há mais de N dias (retenção configurável, definida no plano) são removidas por comando operacional restrito e auditado, sem job em background na API. Agendamento externo fica para L4.

### Session 2026-11-06

- Q: É preciso preservar dados, migrations ou compatibilidade do que já foi executado? → A: Não. O projeto está em construção do zero: nada foi publicado e não há dados. O código existente é ponto de partida e pode ser alterado ou desfeito livremente; o esquema é criado por uma migration inicial única gerada do modelo completo. Todo registro nasce com autoria e data reais. SC-009 vale para essa migration inicial.

### Session 2026-11-05

- Q: Se a instalação da empresa estiver inativa, o usuário ainda deve conseguir encerrar a própria sessão? → A: Sim. Logout com sessão de origem identificada e proteção da requisição válida permanece permitido, sem liberar consultas ou dados da empresa.
- Q: Quando uma conta é bloqueada por tentativas de senha incorreta, suas sessões anteriores devem voltar a funcionar depois dos 15 minutos de bloqueio? → A: Não. O bloqueio invalida todas as sessões da conta; depois dele, é necessário novo login, sem reativar sessões antigas.
- Q: A mesma conta deve poder manter sessões simultâneas em dispositivos diferentes na instalação da empresa? → A: Sim. Sessões são independentes; logout encerra somente a sessão atual, enquanto reset, desativação, bloqueio ou revogação operacional encerram todas as sessões da conta.
- Q: Ao reativar a instalação de uma empresa, as sessões anteriores à inativação podem voltar a funcionar se ainda estiverem dentro do prazo e não tiverem sido revogadas? → A: Sim. Inativação suspende o uso sem estender a validade; reativação permite somente sessões ainda válidas e não revogadas, sem restaurar acesso já encerrado.
- Q: A auditoria das operações de contas e permissões deve identificar qual operador individual executou a ação, ou basta registrar um papel técnico compartilhado? → A: Registrar operador individual autenticado e correlação. Aplicar uma base auditável comum a contas, permissões e sessões, com autor/data de criação e última alteração e correlação; histórico de operações é imutável, sem ampliar para marcador ou tabelas auxiliares do Identity.

### Session 2026-10-15

- Q: Como deve funcionar o isolamento entre clientes/empresas e onde ficam os segredos de conexão?
  → A: Opção 1 — implantação dedicada por cliente (single-tenant por deploy: 1 backend + 1 banco PostgreSQL da própria aplicação por cliente), sem banco apartado de catálogo central (`platform_catalog`). Os segredos e conexões de cada cliente ficam nas variáveis de ambiente/secrets da respectiva aplicação no Dokploy (na VPS) ou cofre externo, nunca no Git ou no frontend.

- Q: Se uma pessoa tiver acesso ao sistema no tenant A e também no tenant B,
  as duas contas devem ter senha, bloqueio e recuperação independentes?
  → A: Sim. Cada tenant tem contas e credenciais próprias; usar o mesmo
  email não vincula identidades nem faz alterações de acesso em A afetarem B.
- Q: Na primeira implementação do boilerplate, você quer somente o acesso funcional
  e testado, ou também uma tela básica para cada tenant administrar seus usuários?
  → A: Acesso funcional e testado, com gestão de contas e permissões por procedimento
  operacional autorizado; sem tela administrativa nesta primeira versão.
- Q: O banco e o backup de cada tenant devem conter também suas contas e permissões,
  para restaurá-las sem restaurar as contas de outros tenants?
  → A: Sim. Contas, credenciais, permissões e sessões pertencem exclusivamente ao banco
  da própria instalação do cliente; não existe banco de catálogo central.
- Q: Você quer registrar esse recorte na especificação ou começar a implementar
  o backend agora?
  → A: Registrar o recorte: primeiro backend iniciado, persistência e migration de
  usuários/acesso, sem código nesta etapa. O exemplo de revenda expressa somente
  isolamento; não define gestão comercial. Esse recorte substitui a amplitude da
  primeira entrega de acesso anteriormente discutida.

## User Scenarios & Testing *(mandatory)*

Os atores são: desenvolvedor de um produto derivado, usuário vinculado a uma empresa,
operador da plataforma, mantenedor e responsável por uma integração externa.
Tenant/cliente significa uma instalação dedicada de empresa (1 instância da aplicação +
1 banco PostgreSQL exclusivo da própria aplicação), configurada por variáveis de ambiente
e secrets próprios; não existe banco apartado de catálogo central nem compartilhamento de
processo/banco entre clientes. Também não pressupõe venda, contratação ou gestão comercial
nem significa automaticamente matriz ou filial. Cada instalação de cliente possui cadastros
de negócio e contas de acesso próprios. Uma pessoa presente em dois clientes possui registros
independentes, mesmo com CPF, CNPJ ou email iguais.
Produtos distintos também não compartilham automaticamente cadastros ou autenticação.

As histórias descrevem a evolução da fundação completa. No primeiro incremento,
o cenário de persistência e migration de US2, FR-034 e SC-009 define o aceite.
Telas de login, gestão operacional ampliada, pipeline full-stack e publicação não
são entregas desse recorte; checks de CI do backend permanecem obrigatórios antes
da integração. Nenhuma operação protegida pode ser exposta antes de seus controles de acesso.

Operações de uso de uma empresa exigem empresa ativa e vínculo autorizado.
Administração técnica da própria instalação, preparação e recuperação são operações privilegiadas:
exigem autorização específica e alvo validado, podem tratar estados não ativos quando
necessário ao procedimento e não concedem acesso de usuário a dados de negócio.

### User Story 1 - Iniciar um novo produto com uma base reproduzível (Priority: P1)

Como desenvolvedor, quero criar um projeto a partir do boilerplate e executar sua
fundação localmente, sem reconstruir a configuração inicial ou copiar dados reais.

**Why this priority**: o valor inicial do boilerplate é reduzir o trabalho repetido
de preparação e permitir que a equipe comece a desenvolver com uma base confiável.

**Independent Test**: um desenvolvedor usa um ambiente limpo e o guia de instalação
para abrir a interface de acesso e verificar que os serviços estão prontos,
sem instalar módulos de negócio.

**Requisitos relacionados**: FR-001 a FR-004, FR-024, NFR-001, NFR-004 e NFR-005.

**Acceptance Scenarios**:

1. **Given** um clone limpo e os pré-requisitos documentados, **When** o desenvolvedor
   configura o ambiente e segue o roteiro, **Then** a interface de acesso e os
   serviços necessários ficam disponíveis, sem funcionalidades de negócio.
2. **Given** um novo repositório derivado, **When** a configuração é concluída,
   **Then** nomes e referências de ambiente podem ser próprios do produto e não
   existem credenciais, usuários reais ou dados de empresas da origem.
3. **Given** configuração obrigatória ausente ou inválida, **When** a execução é
   solicitada, **Then** o problema é identificado sem expor secrets e a operação
   dependente não é apresentada como pronta.
4. **Given** ambientes local e de publicação com configurações diferentes,
   **When** cada ambiente é iniciado, **Then** usa apenas sua configuração,
   sem depender de alterar código ou acessar recursos de outro ambiente.

---

### User Story 2 - Acessar somente a empresa e as operações autorizadas (Priority: P1)

Como usuário, quero me identificar, reconhecer a empresa em uso e consultar meu
contexto de acesso, sem poder acessar outra empresa ou executar ações não permitidas.

**Why this priority**: identidade, permissões e isolamento protegem todos os produtos
que serão criados a partir da fundação.

**Independent Test**: com contas e permissões próprias de duas empresas de teste,
inclusive contas com o mesmo email e senhas diferentes, verificar entrada, consulta
do próprio contexto e saída. Confirmar que alteração de senha, bloqueio, recuperação
ou revogação em uma empresa não afeta a outra, sem qualquer módulo de negócio.

**Requisitos relacionados**: FR-005 a FR-008, FR-022, FR-023, FR-033, FR-034, NFR-001 a NFR-003,
NFR-005 e NFR-006.

**Acceptance Scenarios**:

1. **Given** identidade válida e vínculo ativo com uma empresa, **When** o usuário
   entra e consulta seu contexto, **Then** reconhece sua identidade, a empresa
   selecionada e as permissões aplicáveis, sem receber credenciais de armazenamento.
2. **Given** usuário sem vínculo ou sem a permissão exigida, **When** tenta uma
   operação protegida, inclusive fora da interface web, **Then** o acesso é negado
   sem revelar dados protegidos ou produzir efeitos de persistência.
3. **Given** acesso expirado, revogado ou encerrado pelo usuário, **When** ocorre uma
   nova tentativa protegida, **Then** ela é negada e a interface não mantém o contexto
   anterior como utilizável.
4. **Given** pessoa com contas independentes em duas empresas, **When** tenta usar
   a sessão de A em uma operação de B, **Then** o acesso é negado, mesmo que o email
   seja igual; para acessar B, deve autenticar sua conta de B.
5. **Given** identificação inválida ou tentativa de assumir a identidade de outro
   usuário, **When** o acesso é solicitado, **Then** nenhuma identidade autenticada
   é estabelecida e a resposta não divulga informações protegidas.
6. **Given** contas com o mesmo email em A e B, **When** a senha é alterada ou o acesso
   é bloqueado, recuperado ou revogado em A, **Then** a conta e as permissões de B
   permanecem inalteradas; a senha de A não autentica a conta de B.
7. **Given** procedimento operacional e operador autorizado para A, **When** prepara,
   desativa ou redefine o acesso de uma conta e concede ou retira suas permissões,
   **Then** a alteração é verificável e afeta somente A; operador sem autorização
   não modifica contas ou concessões, e não há tela administrativa exigida nesta versão.
8. **Given** backend iniciado e dois bancos de tenants vazios, **When** a migration de
   usuários/acesso é aplicada e registros sintéticos são persistidos para validação,
   **Then** a estrutura permite cadastrar e consultar usuários e seus acessos em cada
   banco, sem frontend ou painel; email igual nos dois bancos não vincula os registros
   e aplicar novamente a versão vigente não duplica a estrutura nem perde dados.
9. **Given** empresa inativa e sessão própria ainda válida, **When** o usuário solicita
   encerramento com proteção da requisição válida, **Then** a sessão é revogada antes
   da confirmação, sem permitir consulta de identidade, contexto ou dados da empresa.
10. **Given** conta com sessões anteriores e bloqueio por tentativas inválidas,
    **When** o bloqueio termina, **Then** somente um novo login pode estabelecer
    acesso; nenhuma sessão anterior volta a funcionar, e as contas de B não mudam.
11. **Given** conta com sessões em dois dispositivos da própria instalação,
    **When** um deles efetua logout, **Then** somente essa sessão é encerrada;
    a outra permanece válida, mas reset, desativação, bloqueio ou revogação
    operacional invalidam ambas sem alterar contas de outro tenant.
12. **Given** alteração de conta, permissão ou sessão com autor verificado,
    **When** a alteração é persistida, **Then** registra quem/quando criou ou alterou
    e a correlação, na mesma confirmação dos dados. Autoria anônima ou automática
    não é atribuída a um usuário sem autenticação.
13. **Given** sessão anterior válida no navegador e credenciais válidas para novo login,
    **When** a troca de sessão falha, **Then** nenhum novo acesso é confirmado e a
    sessão anterior mantém seu estado e prazo, sem desfazer revogações ou bloqueios
    confirmados por outra operação.
14. **Given** dois logins válidos concorrentes com a mesma sessão anterior,
    **When** tentam substituí-la, **Then** somente uma troca conclui e a outra
    retorna erro explícito sem criar sessão adicional; sessões independentes de
    outros dispositivos não são afetadas.

---

### User Story 3 - Controlar empresas e preservar seus dados isolados (Priority: P1)

Como operador autorizado, quero registrar empresas, controlar sua disponibilidade e
acompanhar a preparação e evolução de seus dados sem afetar outra empresa.

**Why this priority**: a fundação deve tornar a operação multiempresa segura,
incluindo falhas de configuração e de atualização.

**Independent Test**: preparar duas empresas e dados sintéticos de teste, validar
seus estados, leitura, gravação, atualização e falhas. Não é necessário criar um
cadastro comercial, painel administrativo ou módulo de negócio.

**Requisitos relacionados**: FR-009 a FR-016, NFR-001 a NFR-003, NFR-006 e NFR-008.

**Acceptance Scenarios**:

1. **Given** operador autorizado e identificação ainda não cadastrada, **When** registra
   uma empresa pelo procedimento documentado, **Then** ela recebe identificação única,
   estado e referência segura ao seu armazenamento, separados de dados de negócio.
2. **Given** empresa registrada, **When** sua preparação é concluída com sucesso e
   ela é ativada, **Then** pode atender operações autorizadas; uma preparação incompleta
   ou falha não a torna disponível.
3. **Given** duas empresas ativas, **When** leituras e gravações sintéticas são
   intercaladas e concorrentes, **Then** operações válidas confirmam os efeitos
   esperados somente na empresa validada e falhas não são apresentadas como sucesso,
   mesmo com identificadores de registros iguais e reutilização de recursos.
4. **Given** empresa inexistente, ausente, inativa, não autorizada ou indisponível,
   **When** uma operação de uso dessa empresa é solicitada, **Then** ocorre erro explícito,
   sem usar o armazenamento de outra empresa.
5. **Given** atualização da estrutura de informações de uma empresa, **When** ocorre
   sucesso ou falha, **Then** o resultado e a versão alcançada são identificados para
   essa empresa, seus dados existentes são preservados em caso de sucesso e nenhuma
   outra empresa é alterada ou usada como alternativa.
6. **Given** empresa ativa com usuários vinculados, **When** o operador a inativa,
   **Then** novas operações de uso dessa empresa são negadas e as demais continuam com seus
   próprios controles de disponibilidade e autorização. A inativação não estende a
   validade das sessões nem as revoga automaticamente; reativação permite somente
   as ainda válidas e não revogadas, sem restaurar acesso já encerrado.

---

### User Story 4 - Derivar produtos com diferentes conjuntos de módulos (Priority: P2)

Como desenvolvedor, quero acrescentar ou omitir módulos de um produto derivado sem
alterar os controles comuns de identidade, autorização e isolamento.

**Why this priority**: reutilização só é efetiva quando a fundação não obriga todos
os produtos a carregar as mesmas funcionalidades.

**Independent Test**: comparar uma variante sem módulos de negócio com outra que
inclui componentes sintéticos opcionais, usados exclusivamente na validação.

**Requisitos relacionados**: FR-017 a FR-019, FR-028, NFR-007 e NFR-010.

**Acceptance Scenarios**:

1. **Given** produto sem módulos de negócio, **When** a fundação é executada,
   **Then** acesso, contexto da empresa e controles comuns funcionam normalmente.
2. **Given** componente opcional de teste com responsabilidade e contrato declarados,
   **When** é acrescentado ou omitido, **Then** sua disponibilidade é coerente na
   interface e nas integrações, sem referências quebradas nem alteração dos controles
   comuns de acesso.
3. **Given** componente com dependência não atendida, circular ou acesso indevido a
   outro componente, **When** a configuração é validada, **Then** a incompatibilidade
   é identificada antes da liberação e não é contornada por dependência oculta.
4. **Given** proposta de nova dependência ou decisão arquitetural, **When** é revisada,
   **Then** existe necessidade concreta, alternativas e consequências registradas;
   sem justificativa a proposta não é aprovada.

---

### User Story 5 - Consumir a fundação por um contrato independente da interface (Priority: P2)

Como responsável por integração, quero usar os serviços documentados da fundação
sem depender da interface web ou das implementações internas de módulos.

**Why this priority**: o mesmo produto deve suportar consumidores independentes e
permitir integração futura com outras aplicações.

**Independent Test**: um consumidor de teste independente usa apenas a documentação
para consultar o próprio contexto autorizado e verificar respostas de erro.

**Requisitos relacionados**: FR-020 a FR-024, NFR-006 e NFR-010.

**Acceptance Scenarios**:

1. **Given** consumidor independente e identidade autorizada, **When** segue o contrato
   documentado, **Then** consegue consultar identidade e contexto de empresa sem
   consultar código interno ou depender da interface web.
2. **Given** entrada incompleta, inválida ou incompatível com o contrato, **When** ela
   é enviada diretamente ao serviço, **Then** a validação ocorre antes de uso e o erro
   é consistente, sem efeitos indevidos.
3. **Given** consumidor não autorizado ou falha de operação, **When** recebe a resposta,
   **Then** distingue a categoria do erro e dispõe de uma referência para suporte,
   sem secrets, detalhes internos ou dados de outra empresa.
4. **Given** alteração incompatível de um contrato publicado, **When** a mudança é
   proposta, **Then** consumidores afetados e estratégia de transição são documentados
   antes da publicação.

---

### User Story 6 - Validar alterações antes de incorporá-las à fundação (Priority: P2)

Como mantenedor, quero receber evidências automatizadas de qualidade e segurança
para impedir que uma regressão seja propagada a novos produtos.

**Why this priority**: defeitos no boilerplate são multiplicados quando a base é reutilizada.

**Independent Test**: executar a validação em uma alteração controlada válida e em
alterações deliberadamente inválidas, usando apenas dados e recursos de teste.

**Requisitos relacionados**: FR-024 a FR-028, NFR-001, NFR-004, NFR-007 e NFR-008.

**Acceptance Scenarios**:

1. **Given** alteração que atende aos critérios aplicáveis, **When** a validação
   automatizada é executada, **Then** apresenta os resultados e as evidências de
   regras da fundação, acesso, contratos, persistência e isolamento.
2. **Given** erro de compilação, tipagem, convenção, regressão de acesso, persistência
   ou isolamento introduzido de forma controlada, **When** a validação é executada,
   **Then** falha explicitamente e a integração é bloqueada.
3. **Given** credencial fictícia reconhecível pelo controle ou dependência com achado
   de segurança preparado para teste, **When** a validação é executada,
   **Then** o achado é reportado e riscos críticos ou altos não mitigados bloqueiam
   a liberação, sem usar ou publicar secrets reais no exercício.
4. **Given** mudança de contrato, configuração ou decisão relevante, **When** a revisão
   ocorre, **Then** a documentação afetada e a justificativa da decisão estão
   atualizadas no mesmo conjunto de alterações.

---

### User Story 7 - Publicar e recuperar a fundação com procedimentos verificáveis (Priority: P3)

Como operador, quero publicar uma versão validada e recuperar os dados de uma empresa
sem depender de conhecimento não documentado ou comprometer as demais.

**Why this priority**: preparação para publicação inclui recuperação, não apenas
conseguir iniciar a aplicação em outro ambiente.

**Independent Test**: em ambiente de homologação preparado, seguir os guias de
publicação e restauração usando duas empresas e informações sintéticas.

**Requisitos relacionados**: FR-024, FR-029 a FR-032, NFR-001, NFR-002 e NFR-009.

**Acceptance Scenarios**:

1. **Given** ambiente de publicação com pré-requisitos atendidos e versão validada,
   **When** o operador segue o roteiro, **Then** identifica a versão publicada,
   confirma saúde e acesso seguro e preserva dados após reiniciar a aplicação.
2. **Given** duas empresas com cópias de segurança externas ao ambiente publicado,
   **When** uma delas é restaurada em destino de validação, **Then** seu conteúdo
   inclui os dados de negócio e de acesso da própria empresa, corresponde ao ponto
   de recuperação e não contém nem altera dados ou contas da outra. Sessões anteriores
   não são reativadas e permissões recuperadas são revisadas antes de liberar acesso.
3. **Given** falha de publicação ou de atualização das informações,
   **When** o procedimento de recuperação é seguido, **Then** há decisão documentada
   sobre recuperação de aplicação e dados, sem presumir que reverter a aplicação
   desfaz alterações de persistência.
4. **Given** primeira liberação de produção, **When** sua autorização é revisada,
   **Then** existem evidências de publicação, restauração e definição dos responsáveis,
   frequência, retenção e metas de recuperação.

### Edge Cases

- Identificação da empresa ausente, adulterada, desconhecida ou incompatível com a
  identidade: negar a operação dependente, sem escolher empresa padrão.
- Empresa inativada ou vínculo revogado durante uma sessão: validar o novo estado
  antes de cada nova operação protegida de uso da empresa; estado antigo da interface
  não concede acesso.
- Empresa inativa com sessão própria ainda válida: permitir obter a proteção da
  requisição e encerrar essa sessão, sem liberar consultas; falha de revogação
  não pode ser apresentada como saída concluída.
- Empresa reativada: aceitar somente sessões ainda válidas e não revogadas,
  mantendo seu prazo original. Inativação não pausa a contagem da validade
  nem desfaz logout, expiração ou revogação da conta.
- Tentativas simultâneas de empresas diferentes, com identificadores de registros
  iguais: manter leitura, gravação e estado temporário isolados.
- CPF, CNPJ ou email iguais em empresas distintas: não mesclar cadastros, vincular
  contas automaticamente ou impor unicidade global de dados próprios dos tenants.
- Sessão autenticada em A usada para indicar B: negar o acesso; mudar o identificador
  da empresa não converte a sessão de A em uma sessão de B.
- Falha ao substituir a sessão do navegador em novo login: não confirmar novo acesso
  nem encerrar a sessão anterior pela troca incompleta; manter seu prazo e respeitar
  bloqueios, expiração ou revogações confirmados independentemente.
- Logins concorrentes com a mesma sessão anterior: permitir somente uma substituição;
  a tentativa perdedora recebe erro explícito e não cria sessão adicional. Repetição
  exige contexto atual, sem interferir em sessões independentes de outros dispositivos.
- Alteração de senha, bloqueio, recuperação ou revogação de conta em A: não alterar
  identidade, credenciais, sessões ou permissões das contas de B.
- Fim de bloqueio temporário por tentativas inválidas: não reativar sessões anteriores;
  exigir novo login depois de invalidá-las quando o bloqueio for confirmado.
- Limpeza de sessões concorrente com login, logout ou renovação: remover somente
  sessões já expiradas ou revogadas além da retenção; sessão válida nunca é removida
  e falha na limpeza não altera sessões nem é apresentada como concluída.
- Restauração de A: incluir seus dados de acesso, invalidar sessões anteriores e
  revisar permissões recuperadas antes da liberação, sem restaurar ou alterar contas de B.
- Empresa indicada no login: resolver e validar o alvo antes de verificar suas
  credenciais; não enumerar empresas ou contas de outros tenants a usuários anônimos.
- Falha de acesso ao armazenamento de uma empresa: reportar indisponibilidade apenas
  da operação dependente e não redirecioná-la para outra empresa.
- Cadastro duplicado de identificação de empresa: rejeitar sem sobrescrever a
  referência de armazenamento ou o estado já registrado.
- Preparação ou atualização interrompida: registrar a falha e impedir servir um estado
  não validado; o procedimento deve orientar retomada ou recuperação.
- Tentativa de acesso direto sem passar pela interface web: aplicar os mesmos
  controles de identificação, entrada, vínculo e permissão.
- Módulo opcional omitido ou com dependência ausente: não anunciar funcionalidade
  inexistente; dependência obrigatória ausente deve ser diagnosticada antes da liberação.
- Configuração incompleta e secret ausente: não iniciar a operação dependente nem
  preencher a ausência com credenciais fictícias ou credenciais de outro ambiente.
- Erros, logs e documentação de integração: não revelar credenciais, informações
  protegidas de outra empresa ou detalhes internos de produção.
- Falha de validação automatizada: não transformar resultado incompleto em aprovação.
- Restauração dirigida à empresa ou ambiente errado: bloquear o procedimento antes
  de alteração de dados; manter identificação explícita da origem e do destino.

## Requirements *(mandatory)*

### Objetivos e escopo

#### Objetivos

- Permitir iniciar produtos profissionais sem reconstruir os controles comuns.
- Evitar propagação de falhas de segurança e isolamento para produtos derivados.
- Tornar preparação, validação, operação e recuperação reproduzíveis e documentadas.
- Permitir extensão por módulos e consumidores independentes sem acoplamento oculto.

#### Em escopo

- Fundação web com entrada, saída e identificação do contexto de acesso.
- Primeiro incremento de backend com cadastro persistível de usuários/acesso e
  migration aplicável, validado sem frontend ou painel.
- Contas, credenciais, permissões e sessões independentes por tenant,
  mantidas na fonte de dados da própria empresa, sem identidade compartilhada entre
  tenants e sem funcionalidades comerciais.
- Evolução posterior para acesso funcional e testado, com preparação e manutenção de
  contas por procedimento restrito; operações protegidas não são liberadas com controles
  de segurança incompletos.
- Cadastro operacional de empresas, resolução confiável de contexto e persistência isolada.
- Preparação e evolução da estrutura de informações, com resultado por empresa.
- Contratos de integração documentados, validação de entradas e erros consistentes.
- Extensibilidade modular demonstrável por testes, não por módulos de negócio entregues.
- Configuração por ambiente, secrets protegidos, execução local e preparação para publicação.
- Testes automatizados, validação de alterações e documentação de decisões e operação.

#### Fora de escopo

- Implementar código, testes de aplicação, infraestrutura ou deploy nesta etapa de especificação.
- Entregar Clientes, Financeiro, Produtos, Estoque ou outro módulo de negócio,
  inclusive um módulo de referência.
- Aplicativo mobile, microsserviços, mensageria e integrações comerciais específicas.
- Qualquer painel administrativo ou de gestão comercial, autocadastro público,
  cobrança, contratação e provisionamento comercial autônomo.
- Integração com provedor corporativo específico de identidade, federação entre
  produtos ou login compartilhado entre aplicações independentes.
- Implantar serviços reais, registrar empresas reais ou migrar dados de produção agora.

### Functional Requirements

- **FR-001**: O boilerplate DEVE permitir criar um repositório de produto com identidade
  e configuração próprias, sem copiar dados reais ou vínculos com o ambiente de origem.
- **FR-002**: O desenvolvedor DEVE conseguir executar a fundação a partir de um clone
  limpo, seguindo um roteiro único que declare todos os pré-requisitos.
- **FR-003**: Configuração DEVE ser separada por ambiente; ausência ou invalidade de
  valor obrigatório DEVE impedir a operação dependente com diagnóstico seguro.
- **FR-004**: A interface web DEVE apresentar entrada no contexto do tenant,
  estado de autenticação, empresa corrente, contexto do próprio usuário, saída e estados
  de erro, sem menus de negócio; identificar a empresa NÃO DEVE conceder acesso por si só.
- **FR-005**: A fundação DEVE autenticar contas válidas da empresa indicada e negar
  identificação inválida. Cada conta pertence exclusivamente a um tenant; o mesmo
  email em outro tenant NÃO DEVE compartilhar senha, bloqueio, recuperação ou identidade.
  A empresa-alvo DEVE ser resolvida e validada antes de verificar suas credenciais;
  login anônimo NÃO DEVE enumerar contas ou empresas de outros tenants.
- **FR-006**: A fundação DEVE permitir encerrar o acesso e rejeitar novas operações
  protegidas com autenticação expirada ou contexto de acesso revogado ou encerrado.
  Empresa inativa NÃO DEVE impedir obter a proteção da requisição e revogar a própria
  sessão ainda válida. Essa exceção permite somente encerrar acesso, não consultar
  identidade, contexto ou dados; confirmação exige revogação persistida.
  Bloqueio da conta por tentativas inválidas DEVE invalidar todas as suas sessões
  existentes. O fim do bloqueio NÃO DEVE reativá-las; novo acesso exige novo login.
  A sessão DEVE expirar em prazo absoluto contado da sua criação, padrão de 30 minutos,
  sem renovação por atividade, remember-me ou refresh. Produtos derivados PODEM
  configurar esse prazo em minutos inteiros entre 5 e 480 (8 horas), inclusive;
  valor ausente usa o padrão, e valor fora da faixa ou não inteiro DEVE impedir a
  inicialização com diagnóstico seguro (FR-003). O prazo é fixado na criação de
  cada sessão; alterar a configuração não muda o prazo de sessões já emitidas.
- **FR-007**: Toda operação protegida DEVE verificar identidade e permissão no servidor;
  operações de uso de uma empresa também DEVEM validar vínculo ativo. Administração
  técnica da própria instalação e procedimentos técnicos DEVEM exigir autorização específica para o alvo,
  sem conceder acesso de usuário por privilégio implícito; a ausência de autorização
  DEVE resultar em acesso negado.
- **FR-008**: A sessão autenticada DEVE permanecer vinculada à conta e à instalação de
  origem. Para acessar outra empresa, a pessoa DEVE autenticar a conta própria na
  instalação dessa empresa; uma sessão emitida na instalação A NÃO DEVE ser aceita
  na instalação B, mesmo quando o email seja igual.
  Na própria instalação, a conta DEVE poder manter sessões simultâneas independentes
  em dispositivos diferentes. Novo login em outro dispositivo NÃO DEVE encerrar
  as demais; logout encerra somente a sessão corrente. Reset, desativação, bloqueio
  ou revogação operacional de acesso DEVEM encerrar todas as sessões da conta.
  Ao substituir a sessão do navegador em novo login, criação da nova e encerramento
  da anterior DEVEM ser confirmados juntos. Falha na troca NÃO DEVE encerrar a
  anterior nem confirmar novo acesso; NÃO DEVE estender seu prazo ou desfazer
  bloqueios e revogações confirmados por outra operação.
  Dois logins concorrentes com a mesma sessão anterior NÃO DEVEM confirmar duas
  substituições: somente uma conclui, e a tentativa perdedora DEVE retornar erro
  explícito sem criar sessão adicional. Repetição exige o contexto atual.
- **FR-009**: Cada instalação de cliente DEVE identificar a empresa de forma única por
  configuração segura do ambiente e marcador persistido no próprio banco da aplicação,
  sem banco apartado de catálogo central e sem concentrar dados ou contas de múltiplos clientes.
- **FR-010**: Um operador autorizado DEVE conseguir provisionar, ativar e inativar a
  instalação de cada empresa por procedimento documentado no ambiente de hospedagem
  (ex.: stack dedicada no Dokploy/VPS); identidades de instalação conflitantes DEVEM ser rejeitadas.
- **FR-011**: O servidor DEVE validar a configuração da própria instalação e o marcador
  de identidade do seu banco antes de operar, sem aceitar credenciais ou conexões
  escolhidas pelo consumidor HTTP.
- **FR-012**: Cada empresa DEVE ter instância de backend e banco PostgreSQL exclusivos
  (single-tenant por deploy) conforme o isolamento obrigatório da Constituição; operações
  DEVEM usar somente o banco configurado para a própria instalação. Clientes, hóspedes e
  outros cadastros da mesma pessoa em empresas distintas DEVEM ser registros independentes,
  sem compartilhamento, sincronização ou deduplicação automática. Contas, credenciais,
  permissões e sessões DEVEM pertencer ao banco da própria instalação, sem cadastro central.
- **FR-013**: Empresa ausente, desconhecida, inativa, não autorizada ou indisponível
  DEVE causar erro explícito em operações de uso dessa empresa, sem fallback para
  outra. Procedimentos técnicos somente podem tratar estados não ativos com alvo
  validado e autorização específica, sem liberar acesso de usuário. Obtenção da
  proteção da requisição e encerramento da própria sessão seguem a exceção restrita
  de FR-006 e NÃO constituem autorização de uso dos dados da empresa.
  Inativação da instalação suspende o uso, sem revogar sessões automaticamente
  ou estender seus prazos. Reativação permite somente sessões ainda válidas e
  não revogadas; NÃO DEVE restaurar acesso encerrado, expirado ou revogado.
- **FR-014**: A preparação do armazenamento DEVE registrar resultado por empresa;
  a empresa somente pode atender operações após preparação bem-sucedida e ativação.
- **FR-015**: A evolução da estrutura persistida DEVE suportar criação inicial e
  atualização de estado anterior suportado, quando existente, registrando a versão
  alcançada e preservando os dados em caso de sucesso, sem alterar outras empresas.
- **FR-016**: Leitura, gravação e tratamento de falhas por empresa DEVEM comprovar,
  com dados sintéticos, os resultados e efeitos persistidos esperados, inclusive
  restrições e transações quando aplicáveis, concorrência e reutilização de recursos.
- **FR-017**: Cada módulo acrescentado a um produto DEVE declarar responsabilidade,
  dados sob sua gestão, contratos públicos e dependências; consumo DEVE ocorrer por contratos.
- **FR-018**: Um produto DEVE funcionar sem módulos de negócio ou com um subconjunto
  compatível de módulos, sem expor interfaces de módulos ausentes ou quebrar a fundação.
- **FR-019**: Dependências circulares, obrigatórias não atendidas ou acesso às
  implementações internas de outro módulo DEVEM ser identificados antes da liberação.
- **FR-020**: A fundação DEVE oferecer integração para consumidores independentes,
  inclusive consulta do próprio contexto autorizado, sem dependência da interface web.
- **FR-021**: Os contratos de integração DEVEM documentar operações disponíveis, entradas,
  respostas, autenticação, contexto de empresa, permissões e erros; alteração incompatível
  DEVE declarar consumidores afetados e estratégia de transição.
- **FR-022**: Entradas externas DEVEM ser validadas no servidor antes de uso;
  entrada inválida DEVE ser rejeitada sem efeitos de persistência indevidos.
- **FR-023**: Erros DEVEM ter categoria consistente e referência para investigação,
  sem sucesso aparente, secrets, detalhes internos de produção ou dados de outra empresa.
- **FR-024**: A documentação DEVE cobrir arquitetura, instalação, execução local,
  configuração, identidades, permissões, empresas, testes, contratos, extensão modular,
  publicação, recuperação e decisões relevantes, sem depender de instruções informais.
- **FR-025**: DEVEM existir testes automatizados das regras da fundação, incluindo
  validação, estado de empresa, vínculo e permissão, com caminhos válidos, limites e violações.
- **FR-026**: DEVEM existir testes de integração de contratos, persistência, atualização
  da estrutura e isolamento com pelo menos duas empresas e seus armazenamentos reais
  dedicados a testes; simulações isoladas não são evidência suficiente de persistência.
- **FR-027**: A validação automatizada DEVE verificar compilação, tipos, convenções,
  testes, exposição de secrets e vulnerabilidades. Falhas DEVEM bloquear integração;
  riscos críticos ou altos não mitigados DEVEM bloquear liberação.
- **FR-028**: Alterações DEVEM apresentar risco, evidências, documentação afetada e
  justificativa de dependências e decisões arquiteturais relevantes antes de aprovação.
- **FR-029**: DEVEM existir preparação e roteiro de publicação na infraestrutura
  prescrita pela Constituição, com pré-requisitos, configuração e acesso seguro.
- **FR-030**: O operador DEVE conseguir identificar versão e saúde da aplicação
  publicada; reinícios NÃO DEVEM eliminar informações persistidas das empresas.
- **FR-031**: DEVEM existir procedimentos de cópia externa e restauração por instalação
  de empresa, com validação do alvo, do conteúdo recuperado e de ausência de impacto nas demais.
  A cópia DEVE incluir os dados de acesso da própria empresa. Restaurar A NÃO DEVE
  exigir restaurar contas de B; sessões anteriores DEVEM ser invalidadas e permissões
  recuperadas revisadas antes da liberação. A identidade configurada da instalação e
  o marcador do banco continuam necessários para validar o destino restaurado.
- **FR-032**: Falhas de publicação e de atualização de dados DEVEM ter procedimento
  de recuperação que trate aplicação e persistência, com evidências antes de produção.
- **FR-033**: Um operador autorizado DEVE conseguir preparar contas, desativá-las,
  redefinir seu acesso e conceder ou revogar permissões por procedimento restrito,
  com empresa-alvo explícita, verificação de autorização e resultado auditável.
  Cada alteração DEVE ser atribuída a um operador individual autenticado, com
  correlação para investigação; nome declarado livremente ou papel técnico
  compartilhado NÃO DEVEM ser considerados prova de autoria individual.
  Na L2, contas, permissões e sessões DEVEM compartilhar uma base auditável de
  informações de autor/data de criação, autor/data da última alteração e correlação.
  Autores de ações de usuário DEVEM ser verificados; alterações automáticas e
  tentativas anônimas DEVEM ser distinguíveis, sem inventar identidade.
  Histórico de operações DEVE permanecer imutável; o recorte NÃO
  inclui aplicar essa base ao marcador ou às tabelas auxiliares de identidade.
  A operação NÃO DEVE modificar contas ou permissões de outro tenant, mesmo
  com email igual. Não é exigida tela administrativa nem autocadastro público.
  Na L2, alteração e recuperação de senha ocorrem somente por esse reset restrito,
  que encerra todas as sessões da conta; troca de senha pelo próprio usuário fica
  para L3 ou posterior, e recuperação por email/token exige novo plano.
  O procedimento restrito DEVE também remover sessões expiradas ou revogadas há
  mais que a retenção configurada, com auditoria e sem afetar sessões válidas.
  A retenção padrão é de 30 dias, configurável em dias inteiros de 1 a 365, contada
  da expiração ou da revogação; valor fora da faixa DEVE impedir a limpeza com
  diagnóstico seguro. Cada execução DEVE gerar exatamente um registro imutável,
  atribuído ao operador individual autenticado, com retenção aplicada, instante de
  corte, quantidade removida, resultado e correlação, inclusive quando nada for
  removido ou houver falha; o registro NÃO DEVE listar sessões ou contas removidas;
  a API NÃO DEVE executar essa remoção em segundo plano. Revogação DEVE descartar
  o ticket protegido da sessão; registro expirado nunca autoriza uso.
- **FR-034**: O primeiro incremento DEVE entregar backend iniciado e executável,
  modelo persistível de usuários e acessos e migration versionada para criar sua
  estrutura na fonte de dados de cada tenant. DEVEM existir instruções para aplicar
  a migration e testes de criação da estrutura, persistência e isolamento, sem depender
  de frontend, painel administrativo ou módulo de negócio. Não é exigida nessa entrega
  uma suíte de comandos de gestão operacional; endpoints protegidos NÃO DEVEM ser
  liberados sem autenticação e autorização completas.

### Non-Functional Requirements

- **NFR-001 — Confidencialidade**: nenhum secret real DEVE estar em arquivos versionados,
  configuração pública, artefatos distribuídos ao navegador, imagens ou logs. Exemplos
  DEVEM usar valores fictícios; valores sensíveis de execução DEVEM permanecer protegidos.
- **NFR-002 — Menor privilégio**: identidades de execução e operação DEVEM ter somente
  permissões necessárias; publicação DEVE proteger tráfego e serviços internos,
  sem usar acesso administrativo em operações rotineiras.
- **NFR-003 — Isolamento**: a matriz de acesso, leitura e gravação DEVE apresentar zero
  acesso cruzado, inclusive sob execução concorrente, alteração de contexto e falhas.
  Alteração de credenciais, bloqueio, recuperação ou revogação de conta em um tenant
  NÃO DEVE afetar contas e sessões de outro tenant.
- **NFR-004 — Reprodutibilidade**: versões e pré-requisitos DEVEM ser controlados;
  a mesma revisão DEVE permitir executar as validações em ambiente limpo sem ajustes
  manuais não documentados e sem erros de compilação ou tipagem.
- **NFR-005 — Clareza operacional**: a interface e os diagnósticos DEVEM distinguir
  ausência de acesso, entrada inválida e indisponibilidade sem declarar sucesso;
  a empresa selecionada DEVE permanecer reconhecível durante o uso autenticado.
- **NFR-006 — Investigação segura**: falhas de autenticação, autorização, resolução
  de empresa e persistência DEVEM poder ser investigadas com referência à operação,
  sem exposição de secrets ou informações protegidas nos registros e respostas.
- **NFR-007 — Manutenibilidade**: limites e responsabilidades DEVEM ser verificáveis;
  a extensão por módulos NÃO DEVE exigir alterar regras comuns de isolamento ou
  acessar dados internos de outro módulo; decisões relevantes DEVEM ter registro.
- **NFR-008 — Validação confiável**: testes DEVEM ser repetíveis, usar dados sintéticos
  e recursos dedicados, e NÃO DEVEM executar em dados de produção. Cenários críticos
  comprovados são obrigatórios, independentemente do percentual global de cobertura.
- **NFR-009 — Recuperabilidade**: antes de produção, responsáveis, frequência e
  retenção de cópias, metas de recuperação e periodicidade de testes de restauração
  DEVEM estar definidos para o produto; restauração DEVE ter evidência executada.
- **NFR-010 — Compatibilidade**: consumidores DEVEM poder usar contratos publicados
  sem conhecer código interno; mudanças incompatíveis DEVEM ter transição documentada.

### Restrições vinculantes

- **RC-001**: a implementação DEVE obedecer integralmente à Constituição, especialmente
  às responsabilidades e limites dos princípios I e II, stack do princípio III,
  isolamento do princípio IV, contratos do princípio V e segurança do princípio VI.
- **RC-002**: qualidade, execução local, preparação para publicação e automação de
  integração DEVEM seguir os princípios VII a X e o fluxo de desenvolvimento
  constitucional. Esta especificação não substitui essas escolhas por novas tecnologias.
- **RC-003**: segurança, isolamento e integridade prevalecem sobre velocidade.
  Prazos DEVEM ser atendidos por redução de escopo, não por retirar controles críticos.
- **RC-004**: mecanismos concretos, versões, estrutura de código, formato dos contratos
  e procedimentos técnicos serão definidos no planejamento, dentro dessas restrições.
  A presente entrega é documental e não executa implementação ou publicação.

### Key Entities *(include if feature involves data)*

- **Tenant / Empresa**: unidade de isolamento, com identificação única, nome de
  apresentação, estado de disponibilidade e referência ao seu armazenamento.
- **Referência de armazenamento**: associação confiável entre empresa e destino de
  seus dados, com configuração sensível acessível somente à operação autorizada.
- **Identidade de usuário**: identificação autenticável e estado de acesso;
  pertence a um único tenant. Email é único no escopo desse tenant,
  não globalmente entre tenants; o mesmo email pode ter contas independentes.
  Seu cadastro e credenciais pertencem à fonte de dados dessa empresa.
- **Vínculo com empresa**: associação da conta com sua empresa de origem, com estado
  ativo ou revogado e permissões aplicáveis; não é uma concessão de acesso da mesma
  identidade a várias empresas independentes.
- **Permissão**: autorização para uma operação protegida em um contexto definido;
  ausência de concessão implica acesso negado. Dados de concessão e de sessão
  pertencem à mesma fonte de dados da conta e não são compartilhados entre tenants.
- **Definição de módulo**: responsabilidade, contratos e dependências de componente
  opcional acrescentado por um produto, sem representar módulo de negócio entregue.
- **Configuração de ambiente**: parâmetros necessários à execução, com distinção
  entre informação pública e sensível e sem credenciais reais em exemplos.
- **Metadados de auditoria**: base comum dos registros de conta, permissão e sessão,
  com autor/data de criação e última alteração e correlação. Distingue operador,
  usuário verificado, sistema e ação anônima; não falsifica autoria ou data
  e não contém senha, hash, ticket ou cópia de campos secretos.
- **Registro de execução operacional**: empresa-alvo, tipo de preparação, atualização,
  recuperação ou limpeza de sessões, operador individual autenticado, resultado, versão
  e referência de investigação, sem conteúdo secreto. Na limpeza, inclui retenção
  aplicada, instante de corte e quantidade removida, sem ids de sessões ou contas.

## Success Criteria *(mandatory)*

Os critérios abaixo serão demonstrados na entrega da fundação, não nesta etapa
documental. SC-009 define o aceite do primeiro incremento de backend; SC-001 a SC-008
permanecem metas da evolução da fundação completa. As metas de tempo e volume não são
promessas de capacidade para todos os produtos derivados.

### Measurable Outcomes

- **SC-001**: dois desenvolvedores que não escreveram o guia conseguem, cada um,
  executar a fundação e abrir a interface de acesso em até 45 minutos a partir de
  um clone limpo, com pré-requisitos instalados, sem assistência do autor.
- **SC-002**: 100% dos casos da matriz de acesso são aprovados: entrada válida,
  identificação inválida, ausência de permissão, vínculo revogado, acesso expirado,
  empresa inativa, tentativa de usar sessão de A em B e encerramento de acesso,
  sem exposição ou alteração de informação protegida em casos negados. Com contas
  de mesmo email em dois tenants, alteração de senha, bloqueio, recuperação e
  revogação em A DEVEM produzir zero alteração na conta, sessões e permissões de B.
  Com empresa inativa e sessão própria ainda válida, encerramento protegido DEVE
  revogar a sessão sem liberar consultas ou dados dessa empresa.
  Após bloqueio da conta por tentativas inválidas e término desse bloqueio, zero
  sessões anteriores voltam a autorizar operações; somente novo login permite acesso.
  Com o prazo padrão, sessão usada continuamente é negada a partir de 30 minutos
  após sua criação; uso durante o prazo não o estende. Prazos configurados de 5 e
  480 minutos são aceitos; configuração ausente usa 30 minutos; 4, 481 ou valor
  não inteiro impedem a inicialização sem expor secrets.
  Com duas sessões simultâneas da mesma conta, logout em uma DEVE preservar a outra;
  reset, desativação, bloqueio ou revogação operacional DEVEM invalidar ambas.
  Falha na troca de sessão em novo login DEVE confirmar zero novas sessões e
  preservar o estado e prazo anteriores, respeitando bloqueios e revogações
  confirmados independentemente.
  Dois logins válidos concorrentes com a mesma sessão anterior DEVEM confirmar
  exatamente uma substituição e zero sessões adicionais pela tentativa perdedora,
  que recebe erro explícito; outras sessões independentes permanecem válidas.
  Inativação/reativação da instalação DEVE preservar o prazo original: sessão ainda
  válida e não revogada pode retomar; sessão expirada ou revogada permanece negada.
  Criação/alteração de conta, permissão e sessão DEVE preservar os metadados de
  auditoria e distinguir dois operadores individuais autenticados. Falha ao gravar
  auditoria NÃO DEVE confirmar a alteração; nenhuma autoria é fictícia.
  Com retenção padrão, a limpeza remove 100% das sessões vencidas ou revogadas há
  mais de 30 dias e 0% das sessões válidas ou encerradas há 30 dias ou menos;
  cada execução, inclusive sem remoções ou com falha, gera exatamente um registro
  atribuído ao operador que a executou.
- **SC-003**: em 200 operações sintéticas de leitura e gravação intercaladas entre
  duas empresas, 100 por empresa e pelo menos 20 simultâneas, ocorrem zero acessos
  ou alterações cruzadas, incluindo reutilização de recursos e falhas provocadas;
  operações válidas produzem os resultados esperados e operações negadas não
  produzem efeitos indevidos. Negar todas as operações não satisfaz o critério.
- **SC-004**: duas variantes de produto são executadas, uma sem módulos de negócio
  e outra com componente opcional exclusivamente de teste, sem alterar controles
  comuns de identidade, permissão e isolamento nem manter referências quebradas.
- **SC-005**: um consumidor independente consulta seu contexto autorizado e reconhece
  corretamente as categorias de entrada inválida e acesso negado usando somente
  os contratos publicados, sem consultar implementação ou interface web.
- **SC-006**: 100% das alterações controladas com falhas de compilação, tipos,
  convenções, regras de acesso, persistência ou isolamento são rejeitadas antes
  da integração; achados de segurança de teste são reportados e triados.
- **SC-007**: um operador que não escreveu os guias publica uma versão em ambiente
  de validação e demonstra restauração de cada uma de duas empresas, preservando
  seu ponto de recuperação e sem alterar a outra empresa, usando somente os guias.
  Cada restauração recupera suas contas e permissões sem restaurar as da outra
  empresa; sessões anteriores permanecem inválidas até nova autenticação autorizada.
- **SC-008**: os revisores conseguem localizar, na documentação, instruções para
  todos os tópicos de FR-024 e evidências para todos os controles de liberação
  aplicáveis, sem depender de orientações informais.
- **SC-009**: em duas bases de tenants inicialmente vazias, a estrutura de usuários/acesso
  é criada pela migration e o backend inicia sem frontend ou painel. Registros sintéticos
  com email igual podem ser persistidos e consultados independentemente em A e B;
  repetir a aplicação da versão vigente produz zero duplicação e zero perda de dados.
  A validação não exige módulo de negócio nem cria credenciais padrão.

## Assumptions

- **AS-001 — Nome confirmado**: o usuário confirmou `boilerplate-react-dotnet-sdd`.
  A menção anterior a outro nome no texto de entrada não renomeia o projeto.
- **AS-002 — Escopo confirmado**: a feature entrega somente a fundação técnica,
  começando pelo backend e pela migration de usuários/acesso.
  Módulos de negócio, inclusive um módulo de referência, não fazem parte desta feature.
- **AS-003 — Operação inicial confirmada**: contas, permissões e empresas são preparadas
  e mantidas por procedimentos técnicos restritos e documentados; não há tela administrativa
  nem cadastro público nesta versão. Esses procedimentos não dispensam autorização
  nem equivalem ao acesso de um usuário aos dados de negócio da empresa.
- **AS-004 — Instalações e contas independentes**: cada cliente possui sua própria
  instalação (backend + banco da aplicação) configurada por variáveis de ambiente/secrets
  no Dokploy/VPS ou cofre externo; não existe banco apartado de catálogo central.
  A mesma pessoa pode ter contas distintas nas instalações de vários clientes, com email
  repetido, mas credenciais e ciclo de acesso independentes. Não há troca de cliente na
  mesma sessão nem reaproveitamento de uma identidade autenticada em outra instalação.
- **AS-005 — Dados de teste**: componentes e registros sintéticos podem existir
  exclusivamente nos testes de persistência e modularidade, sem expor funcionalidades
  de negócio na distribuição da fundação.
- **AS-006 — Decisões do plano**: métodos concretos de autenticação e resolução da
  empresa, versões da stack e organização interna serão escolhidos no planejamento,
  respeitando a Constituição e sem exigir integração com provedor específico.
- **AS-007 — Dependências de avaliação**: a execução local pressupõe pré-requisitos
  instalados e conectividade para obter recursos documentados; o prazo de SC-001
  exclui instalação desses pré-requisitos e indisponibilidade externa.
- **AS-008 — Dependências de publicação**: infraestrutura de homologação, acesso do
  operador, domínio, proteção do tráfego e destino externo de backup são fornecidos
  pelo responsável pelo ambiente, conforme requisitos constitucionais de publicação.
- **AS-009 — Metas por produto**: capacidade de produção, retenção e tempos de
  recuperação dependem do produto derivado. O boilerplate fornece controles e guias
  para defini-los antes da liberação, sem impor um perfil de carga universal.
- **AS-010 — Estado anterior**: atualizações de estrutura devem ser verificadas quando
  houver estado anterior suportado; a criação inicial e o comportamento de falha
  continuam obrigatórios desde a primeira versão.
- **AS-011 — Etapa atual**: a checklist avalia a qualidade dos requisitos. Sua aprovação
  não afirma que a fundação, seus testes ou os critérios de sucesso já foram implementados.
- **AS-012 — Cenários futuros**: validação fiscal com atendimento técnico e gestão
  de hotelaria são produtos derivados distintos, em repositórios próprios, com módulos
  e dados próprios. Servem para verificar adequação da fundação; não incluem entregas
  de arquivos fiscais, leads, chamados, hóspedes, quartos, reservas ou relatórios nesta feature.
- **AS-013 — Primeiro incremento de backend**: a implementação começa pela base
  executável e pela migration do cadastro de usuários/acesso. Login, sessão, autorização
  e frontend completos pertencem à evolução posterior; operações protegidas não são
  expostas antes desses controles. Não há painel nem gestão comercial.
- **AS-014 — Sincronização do desenho**: plano, modelo, contratos e guia DEVEM seguir
  contas, permissões e sessões locais ao tenant, sem identidade central compartilhável.
  DEVEM separar o primeiro incremento de backend das entregas posteriores e manter
  os gates constitucionais aplicáveis, sem ampliar o recorte com gestão comercial.
- **AS-015 — Migration inicial**: a migration cria a estrutura, não usuários com senhas
  padrão. Contas sintéticas de validação são preparadas somente em ambiente de teste,
  sem secrets reais em código ou dados compartilhados entre tenants.
