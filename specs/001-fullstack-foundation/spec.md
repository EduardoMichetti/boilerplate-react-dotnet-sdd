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

**Recorte vigente L3/I3**: planejamento sobre a L2/I2 entregue e validada
(quickstart §1–§7, CI verde), sem reabrir o histórico de L1/L2 em tasks.md.
L3 cobre a interface web de acesso ao tenant (FR-004) e a extensão modular
(FR-017 a FR-019). SC-001 e SC-004 definem o aceite da L3; SC-002/SC-003/SC-005/SC-009
e os critérios de aceite da L2 permanecem como regressão obrigatória, e
SC-006/SC-008 acompanham os controles aplicáveis, agora incluindo o frontend.
Não inclui painel administrativo, menus ou módulos de negócio, troca de senha pelo
próprio usuário, recuperação por email/token, nem publicação real;
publicação/recuperação (Dokploy/VPS, backups, restauração) continuam L4/I4.

**Prioridade antes da L3 (reabertura da L2)**: o modelo de sessão passa a combinar
expiração por inatividade e teto absoluto (FR-006, sessão de esclarecimentos de
2026-11-23). Essa alteração da L2 (backend, persistência, SC-002 e testes de regressão)
DEVE ser planejada, implementada e validada antes de iniciar a L3 ou qualquer outra
estratégia; a nova entrada no próprio local (FR-004) pertence à L3 e depende dela.

## Clarifications

### Session 2026-11-24

- Q: Para não gravar no banco a cada requisição, o último uso da sessão pode ser gravado no máximo 1 vez por minuto, o que pode expirar a sessão até 60 s antes dos 30 min de inatividade (nunca depois). O SC-002 aceita essa tolerância? → A: Sim. A negação por inatividade ocorre no máximo no fim da janela contada da última requisição autenticada aceita e nunca antes da janela menos 60 segundos; o registro de uso é gravado no máximo uma vez por minuto por sessão.
- Q: Quando o usuário pede para sair e a API responde que a sessão já estava expirada ou revogada (401), o que a interface faz? → A: Conclui a saída localmente: descarta todo o estado da aba, vai à entrada com aviso de sessão encerrada e avisa as demais abas como logout explícito. Não abre o diálogo de nova entrada.
- Q: Quais requisições contam como "requisição autenticada aceita" para renovar a inatividade? → A: As que passaram por sessão válida, instalação ativa, CSRF e permissão e chegaram à operação protegida, mesmo que a operação recuse a entrada (400/422). Negações de acesso (401/403/409/429/503, CSRF inválido), login, logout, token CSRF, identidade pública do tenant, health, documento de contrato e arquivos da interface não renovam.
- Q: Um produto derivado que ainda tenha configurada a chave antiga de prazo absoluto único deve ter qual comportamento ao iniciar a API? → A: Com valor preenchido, a API não inicia e o diagnóstico indica as duas configurações novas (inatividade e teto); valor vazio é tratado como ausente.
- Q: Como nada foi publicado, as mudanças de esquema da L2.1 (sessão) e da L3 (permissões) regeneram a migration inicial única ou criam migrations incrementais? → A: Regeneram a migration inicial única enquanto não houver publicação com dados (até a L4), recriando bancos locais e de CI; "versão anterior suportada" significa versão publicada. Após a primeira publicação, só migrations incrementais com upgrade validado.
- Q: As revalidações automáticas da interface ao ganhar foco, ficar visível ou navegar (`GET /api/v1/me`) devem renovar a janela de inatividade da sessão? → A: Sim. São requisições autenticadas aceitas como qualquer outra (evento sempre disparado por ação humana, sem polling); renovam a inatividade e nunca o teto absoluto.
- Q: Se duas abas fizerem a nova entrada no próprio local ao mesmo tempo e uma receber "conflito de sessão" (409), o que a aba perdedora deve fazer? → A: Uma única revalidação da identidade (`/me`) logo após o 409: mesma conta válida restaura a aba sem diálogo; outra conta descarta o estado; sessão inválida mantém o diálogo com a mensagem de conflito. O login nunca é repetido automaticamente.
- Q: Quando a sessão é negada (401), o backend deve registrar o motivo (inatividade, teto, revogação, ausente) e a API deve expô-lo à interface? → A: Somente no log. A resposta continua genérica (`AUTH_REQUIRED`); o log do backend registra o motivo categorizado (inatividade, teto absoluto, revogada, ausente/inválida) com a referência de investigação, sem identificador de sessão, ticket ou cookie.

### Session 2026-11-23

- Q: Como um produto derivado escolhe os módulos incluídos? → A: Na compilação: o host referencia e registra os projetos dos módulos usados; omitir é remover a referência. Sem liga/desliga por configuração em runtime na L3.
- Q: A operação pública de identidade do tenant revela a anônimos que a empresa está inativa? → A: Não. Ela devolve id e nome sempre que a instalação está configurada e o marcador do banco confere, ativa ou inativa; o estado inativo só aparece após tentativa de login ou para sessão existente. Configuração/marcador inválido ou banco indisponível resultam em indisponibilidade genérica.
- Q: Quais proteções de navegador o backend exige ao servir a interface? → A: Política de segurança de conteúdo estrita (somente recursos da própria origem, sem scripts inline, sem incorporação em frames), bloqueio de detecção de tipo, política de referência restrita e cache controlado (página inicial sem cache; recursos versionados com cache longo), verificadas por teste. HSTS fica para a publicação (L4).
- Q: O que a tela autenticada de contexto mostra, e ela lista módulos? → A: Identidade da conta, empresa corrente (sempre visível), permissões efetivas e ação de sair. Não lista módulos; os módulos habilitados ficam disponíveis somente no contrato da API, para integrações e testes.
- Q: Como a interface garante que o usuário passe a usar a versão nova após uma atualização, sem limpar cache manualmente? → A: Sem service worker. A página inicial é sempre revalidada e os arquivos da interface têm nome versionado. A API informa a versão publicada; se ela diferir da versão carregada, a interface avisa que há nova versão e oferece recarregar. Se um arquivo da interface falhar ao carregar, recarrega automaticamente uma única vez. Limpar cache nunca é passo exigido.
- Q: Respostas da API com dados da conta, contexto ou empresa podem ser armazenadas em cache por navegador ou proxies? → A: Não. Toda resposta da API, inclusive erros e a identidade pública do tenant, proíbe armazenamento (`no-store`), verificado em teste. Cache em memória da própria interface é permitido, mas não pode ser persistido no armazenamento do navegador e é descartado no logout ou na negação por sessão inválida; a biblioteca de estado/consultas é decidida no plano com justificativa de dependência.
- Q: Quais testes do frontend existem na L3 e quais bloqueiam a integração? → A: Lint, verificação estrita de tipos e testes de unidade/componentes (Vitest + Testing Library) bloqueiam; testes ponta a ponta (Playwright) da entrada, contexto, saída e estados de erro, executados contra a stack local com o backend servindo a interface, também bloqueiam. O isolamento A×B continua comprovado pelos testes de backend.
- Q: Qual idioma e nível mínimo de acessibilidade a interface de acesso atende na L3? → A: Somente pt-BR, sem mecanismo de tradução; telas de acesso conforme WCAG 2.1 nível AA, com verificação automática nos testes ponta a ponta exigindo zero violações sérias ou críticas.
- Q: Quais navegadores a interface suporta e quais os testes ponta a ponta cobrem? → A: As duas últimas versões estáveis de Chrome, Edge e Firefox em desktop; testes ponta a ponta bloqueantes somente em Chromium. Safari e navegadores móveis sem garantia formal na L3.
- Q: O que a interface faz quando uma chamada é negada por sessão expirada/revogada ou empresa inativa durante o uso? → A: Sessão inválida: descarta todo o estado local de identidade e contexto e volta à tela de entrada com aviso de sessão encerrada. Empresa inativa: mostra o estado de empresa indisponível, oferecendo apenas sair. Sem temporizador de expiração no cliente e sem novo login silencioso. Preservar trabalho em andamento fica a cargo de módulos futuros. *(Parte de sessão inválida substituída nesta mesma sessão pela nova entrada no próprio local; empresa inativa, ausência de temporizador e de login silencioso permanecem.)*
- Q: Como o SC-001 é evidenciado para aceitar a L3? → A: Uma pessoa que não escreveu o guia o executa sem assistência, do clone limpo até a tela autenticada, e registra tempo, revisão e dúvidas. A segunda execução é o mesmo roteiro automatizado no CI (ambiente limpo → stack local → teste ponta a ponta), concluído em até 45 minutos.
- Q: O que a tela autenticada mostra quando a conta entra com sucesso mas não tem `foundation.context.read`? → A: Mostra a identidade da conta (`/me`) e a empresa (operação pública), com aviso de que a conta não tem permissão para ver o contexto de acesso, oferecendo apenas sair; não lista permissões. O contrato L2 não muda e nenhuma permissão é concedida automaticamente.
- Q: A interface exibe ao usuário a referência de suporte (`traceId`) devolvida pela API em erros? → A: Sim, de forma seletiva: mensagem pt-BR por categoria (credenciais inválidas, muitas tentativas, conflito de sessão, indisponibilidade, erro inesperado); indisponibilidade e erro inesperado mostram também o código de referência copiável. Credenciais inválidas mostram somente a mensagem genérica.
- Q: De onde a interface obtém a versão publicada para detectar atualizações? → A: Da operação pública de identidade do tenant, que devolve também um identificador opaco do build (sem número de versão, commit, data ou ambiente). A interface compara com o identificador embutido nela e consulta somente em eventos (carga, retorno de foco, navegação), sem polling curto.
- Q: A política de segurança de conteúdo também proíbe estilos inline? → A: Sim. Estilos somente de arquivos da própria origem, sem `'unsafe-inline'`, nonce ou hash; o CSS é extraído em arquivos no build e bibliotecas de UI que injetam estilos em runtime não são compatíveis (escolha justificada no plano).
- Q: A interface de acesso tem meta de desempenho verificável no CI na L3? → A: Sim, orçamento de tamanho: JavaScript do carregamento inicial ≤ 200 KB gzip, bloqueante no CI; chunks carregados sob demanda não contam; produto derivado pode ajustar o limite com justificativa documentada. Sem meta de tempo de carga na L3.
- Q: Como as permissões próprias de um módulo entram no catálogo de permissões? → A: Declaradas pelo módulo em código, com prefixo obrigatório igual ao id do módulo; o catálogo efetivo é fundação + módulos registrados. Prefixo divergente ou código duplicado falha a inicialização e o teste de arquitetura. Concessões a códigos de módulo omitido ficam inertes (não autorizam) e não são apagadas.
- Q: Quem pode ver quais módulos estão habilitados na instalação? → A: Somente quem consulta o contexto de acesso: a lista (id e versão do contrato) vem em `GET /api/v1/context`, exigindo sessão válida e `foundation.context.read`. Nenhuma operação anônima a revela; a identidade pública do tenant não muda.
- Q: Na L3, a fundação já oferece persistência própria para módulos (schema e migrations separados)? → A: Não. O módulo declara os dados sob sua gestão, que na L3 deve ser "nenhum"; módulo que declarar dados persistidos é rejeitado na inicialização e nos testes de arquitetura. Schema e migrations por módulo ficam para o primeiro módulo de negócio, com novo plano.
- Q: Com a interface aberta em duas abas, o que a outra aba faz após logout ou sessão negada em uma delas? → A: A aba que sai ou detecta sessão inválida avisa as demais abas da mesma origem por mensagem entre abas (sem gravar no armazenamento do navegador); elas descartam o estado de identidade/contexto e voltam à entrada. Se o aviso não chegar, a próxima chamada negada ou a revalidação no retorno de foco garante o mesmo efeito. Coberto por teste ponta a ponta. *(Substituída nesta mesma sessão: somente o logout explícito é avisado às demais abas; sessão expirada ou revogada leva cada aba à nova entrada no próprio local.)*
- Q: A interface envia erros do navegador ou telemetria para algum lugar na L3? → A: Não. Nenhum envio a terceiros nem ao próprio backend; a investigação usa o código de referência exibido ao usuário e os logs do backend. Relato de erros do frontend fica para a L4 ou para o produto derivado.
- Q: Ao servir a interface, como o backend trata caminhos desconhecidos e links diretos para telas protegidas sem sessão? → A: Fallback restrito. `/api/*`, `/openapi/*` e `/health*` nunca devolvem a interface: rota inexistente recebe erro JSON 404 padronizado. Demais navegações GET devolvem a página inicial com os mesmos cabeçalhos de segurança e cache; rota desconhecida da interface mostra "página não encontrada". Tela protegida sem sessão leva à entrada e, após login, sempre à tela de contexto, sem parâmetro de retorno.
- Q: Como a dependência de um módulo considera a versão do contrato exigido? → A: Versão maior exata. Cada contrato tem versão maior inteira (v1, v2…); a dependência declara id do contrato e versão maior exigida. Versão maior diferente ou contrato ausente é dependência não atendida: falha a inicialização e o teste de arquitetura. Mudança compatível não altera a versão maior.
- Q: Qual formato o id de módulo deve seguir e quais ids são reservados? → A: 2 a 32 caracteres, letras minúsculas sem acento, dígitos e hífen, começando por letra, sem ponto; `foundation` é reservado. Permissão do módulo é `<id>.` seguido do restante do código. Id inválido, reservado ou duplicado falha a inicialização e o teste de arquitetura.
- Q: Como a interface se comporta durante carregamentos/envios e após indisponibilidade? → A: Indicador de carregamento anunciado a leitores de tela; botão de entrar desabilitado enquanto o envio está pendente (sem envio duplo). Indisponibilidade oferece "tentar novamente"; nenhuma nova tentativa automática de login ou de leitura de identidade/contexto. Coberto por testes de componente e ponta a ponta.
- Q: O documento OpenAPI servido (`/openapi/v1.json`, anônimo) deve incluir operações de módulos, e quem pode acessá-lo? → A: Somente fundação. O documento servido anonimamente descreve apenas operações da fundação e é idêntico com ou sem módulos. Contratos de módulos são publicados como arquivos versionados no repositório do produto, não servidos pela instância. Teste compara o documento nas duas variantes do SC-004.
- Q: O que a interface faz quando o pedido de sair falha (rede, 5xx ou banco indisponível)? → A: Permanece na tela autenticada, mostra indisponibilidade com código de referência copiável e oferece "tentar sair novamente", sem avisar as outras abas. Se a resposta indicar sessão já inválida, trata como sessão encerrada e volta à entrada.
- Q: O que o backend faz se os arquivos compilados da interface não estiverem presentes ao iniciar? → A: Depende do ambiente. Em Produção, a ausência impede a inicialização com diagnóstico seguro. Em Desenvolvimento/Testes, a API inicia normalmente e navegações da interface recebem 404 padronizado (no desenvolvimento a interface é servida pelo servidor local). Testes cobrem os dois casos.
- Q: Quando o login recebe "muitas tentativas" (429 com `Retry-After`), o que a tela de entrada faz? → A: Mostra mensagem pt-BR com o tempo aproximado de espera e mantém o botão de entrar desabilitado até o prazo; depois libera sem reenviar automaticamente. Não revela conta nem bloqueio de conta.
- Q: Na tela de contexto, as permissões efetivas aparecem como código técnico ou com descrição em pt-BR? → A: Descrição pt-BR declarada em código junto de cada permissão (fundação e módulos) e devolvida pela API em `GET /api/v1/context`; a tela mostra descrição e código. Permissão sem descrição falha a inicialização e o teste de arquitetura.
- Q: A tela de contexto mostra até quando a sessão vale? → A: Sim. Mostra no fuso local, como texto estático, o horário do teto absoluto informado pela API e a regra de inatividade (ex.: "Sessão válida até 18:35, ou após 30 min sem uso"), sem contagem regressiva, aviso prévio ou ação de renovar.
- Q: Qual estratégia de sessão evita que o usuário perca o que está fazendo quando a sessão expira? → A: Expiração por inatividade com teto absoluto (o uso renova a inatividade, nunca o teto) somada a nova entrada no próprio local: ao expirar, a interface oculta o conteúdo, preserva o estado da aba em memória e pede credenciais num diálogo; mesma conta restaura a tela, outra conta ou sair descarta tudo. Reabre a L2 e é priorizado antes de iniciar a L3.
- Q: Quais são os padrões e faixas da inatividade e do teto absoluto da sessão? → A: Inatividade padrão de 30 minutos (faixa 5–240); teto absoluto padrão de 600 minutos/10 h (faixa 30–720); inatividade maior que o teto, valor fora da faixa ou não inteiro impede a inicialização.

### Session 2026-11-22

- Q: Na L3, como o frontend deve ser servido junto da API, mantendo cookie de sessão e CSRF na mesma origem? → A: O backend serve o SPA: o build do Vite é incluído na imagem da API e servido por ela (mesma origem real, uma stack por tenant, sem CORS). Em desenvolvimento, o servidor do Vite faz proxy das chamadas de API para o backend.
- Q: Como o frontend obtém o identificador do tenant enviado nas operações? → A: O próprio backend informa em runtime, por operação pública mínima que devolve somente id e nome de apresentação da instalação configurada. Não há variável de ambiente de tenant no frontend, campo na tela ou subdomínio; um único bundle serve todas as instalações. A informação não concede acesso nem enumera outros tenants.
- Q: Na L3, como um módulo é declarado e como ciclos/dependências não atendidas são verificados? → A: Módulo somente de backend: projetos .NET próprios (contratos separados da implementação) com declaração em código de responsabilidade, dados, contratos e dependências, registrada na inicialização. Ciclo ou dependência obrigatória ausente falha a inicialização com diagnóstico seguro e é rejeitado por testes de arquitetura bloqueantes no CI. O frontend não tem módulos próprios; apenas reflete o que o backend anuncia como habilitado.
- Q: O que o componente opcional de teste do SC-004 expõe e como se garante que não é distribuído? → A: Um projeto sob `tests/` com uma única operação de API sintética, protegida por permissão própria e com dependência declarada de um contrato, carregado somente pelos testes de integração/CI. Solução e imagem de produção não o referenciam, o que é verificado por teste/CI. Não há tela.
- Q: A troca de senha pelo próprio usuário entra na L3? → A: Não. Na L3, senha continua sendo alterada somente pelo reset restrito do operador. Troca pelo próprio usuário e recuperação por email/token ficam fora da L3 e exigem novo plano.

### Session 2026-11-08

- Q: Qual deve ser o limite máximo configurável para o prazo absoluto da sessão em produtos derivados (o padrão continua 30 minutos)? → A: Opção B. Faixa permitida de 5 a 480 minutos (8 horas), limites inclusivos; padrão de 30 minutos quando ausente; valor fora da faixa ou não inteiro impede a inicialização com diagnóstico seguro. *(Substituída em 2026-11-23: inatividade 5–240 min e teto absoluto 30–720 min.)*
- Q: Por quantos dias, por padrão, sessões vencidas ou revogadas devem ser mantidas antes que o comando de limpeza possa removê-las? → A: Opção B. Retenção padrão de 30 dias, configurável em dias inteiros de 1 a 365; contada a partir da expiração (sessão vencida) ou da revogação (sessão revogada); valor fora da faixa impede a execução do comando com diagnóstico seguro.
- Q: Retenção inválida ou operador não autorizado em `sessao limpar` contam como execução que gera registro? → A: Não. A execução auditável começa após operador individual autenticado e alvo validado; antes disso o comando encerra com diagnóstico seguro, sem registro e sem efeito (`/speckit-analyze`, mantenedor).
- Q: Como a execução da limpeza de sessões deve ficar registrada no histórico imutável de operações? → A: Opção A. Um registro por execução (inclusive sem remoções ou com falha), atribuído ao operador individual autenticado, com retenção aplicada, instante de corte, quantidade removida, resultado e correlação; sem identificadores de sessões ou contas removidas e sem autoria "Sistema".

### Session 2026-11-07

- Q: Se um novo login falhar ao salvar a nova sessão, a sessão anterior desse navegador deve continuar válida? → A: Sim. A troca é atômica: encerramento da sessão anterior e criação da nova são confirmados juntos. Falha preserva o estado e o prazo anteriores, sem desfazer bloqueios ou revogações concorrentes e sem confirmar novo acesso.
- Q: Quando dois logins simultâneos tentarem substituir a mesma sessão do navegador, apenas um deve conseguir concluir a troca? → A: Sim. Apenas uma substituição conclui; a tentativa concorrente retorna erro explícito sem criar sessão adicional. Nova tentativa exige o contexto atual, sem limitar sessões independentes em outros dispositivos.
- Q: Na L2, "alteração de senha" significa só a redefinição por operador autorizado ou também troca pelo próprio usuário via API? → A: Somente redefinição por operador (reset restrito que encerra todas as sessões da conta). Troca pelo próprio usuário fica para L3 ou posterior; recuperação por email/token não faz parte da feature sem novo plano.
- Q: A sessão deve durar no máximo 30 minutos desde o login, sem renovação por atividade? → A: Sim, como padrão da fundação: prazo absoluto de 30 minutos contado da criação, sem sliding, remember-me ou refresh. O valor é configurável por produto derivado dentro de um limite máximo definido no plano; valor ausente usa o padrão e valor fora do limite impede a inicialização. *(Substituída em 2026-11-23: inatividade de 30 min renovada pelo uso, com teto absoluto de 10 h.)*
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
  não concede acesso. Na primeira negação, a interface oculta o conteúdo e pede nova
  entrada no próprio local (sessão inválida) ou apresenta empresa indisponível com
  opção de sair.
- Sessão expirada por inatividade ou teto durante o uso: o conteúdo é ocultado e a
  nova entrada da mesma conta restaura o estado da aba, sem reenviar gravações
  automaticamente; outra conta ou sair descarta tudo. Computador abandonado não
  exibe dados após a expiração.
- Empresa inativa com sessão própria ainda válida: permitir obter a proteção da
  requisição e encerrar essa sessão, sem liberar consultas; falha de revogação
  não pode ser apresentada como saída concluída: a interface permanece autenticada
  e oferece tentar sair novamente.
- Empresa reativada: aceitar somente sessões ainda válidas e não revogadas,
  mantendo seu teto original. Inativação não pausa o teto nem a inatividade
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
- Aba aberta durante uma atualização publicada: a interface antiga não pode falhar
  silenciosamente; deve detectar a nova versão ou arquivo ausente e conduzir à recarga,
  sem exigir limpeza de cache.
- Conta autenticada sem permissão de consultar o próprio contexto: mostrar identidade,
  empresa e aviso de permissão ausente, somente com opção de sair, sem tratar como erro
  de sessão ou de empresa.
- Logout explícito com a interface aberta em várias abas: as demais abas descartam
  identidade e contexto e voltam à entrada, sem continuar exibindo dados da conta
  encerrada. Expiração em várias abas: cada aba oculta o conteúdo e pede nova entrada
  no próprio local; nova entrada em uma aba restaura as outras no retorno de foco
  somente se for a mesma conta. Novas entradas simultâneas em duas abas: a que receber
  conflito de sessão revalida a identidade uma vez e restaura sem diálogo se a outra aba
  já autenticou a mesma conta, sem reenviar o login.
- Caminho inexistente sob a API, documentação ou saúde: responder erro 404 padronizado,
  nunca a página da interface; link para tela protegida sem sessão leva à entrada,
  sem redirecionamento posterior a destino indicado na URL.
- Arquivos compilados da interface ausentes na inicialização: em Produção, falhar
  a inicialização com diagnóstico seguro; em Desenvolvimento/Testes, iniciar a API
  e responder 404 padronizado às navegações da interface.
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
  A interface DEVE ser servida pela própria instância do backend do tenant, na mesma
  origem da API, sem CORS e sem serviço adicional por instalação; em desenvolvimento,
  o servidor local da interface DEVE encaminhar as chamadas de API ao backend.
  Em Produção, ausência dos arquivos compilados da interface DEVE impedir a
  inicialização com diagnóstico seguro (FR-003); em Desenvolvimento e Testes, a API
  DEVE iniciar sem eles e navegações da interface DEVEM receber 404 padronizado.
  Caminhos `/api/*`, `/openapi/*` e `/health*` NUNCA DEVEM devolver a interface: rota
  inexistente nesses prefixos DEVE receber erro 404 no formato padronizado da API.
  Demais navegações GET DEVEM devolver a página inicial com as mesmas proteções e regras
  de cache (NFR-012); rota desconhecida da interface DEVE mostrar "página não encontrada".
  Acesso a tela protegida sem sessão DEVE levar à entrada; após login pela tela de
  entrada, a interface DEVE ir sempre à tela de contexto, sem parâmetro ou destino de
  retorno informado pela URL (a nova entrada no próprio local restaura a tela em memória
  da aba, nunca um destino vindo da URL).
  A interface DEVE obter id e nome de apresentação do tenant de uma operação pública
  mínima da própria instalação, que devolve somente a identidade configurada dessa
  instalação e o identificador opaco do build (NFR-012), sem enumerar outros tenants
  e sem conceder acesso. O bundle NÃO DEVE conter identificador de tenant; o mesmo build DEVE servir qualquer instalação.
  Falha ao obter essa identidade DEVE ser apresentada como indisponibilidade, sem
  permitir entrada nem escolher empresa padrão. A operação NÃO DEVE revelar a
  anônimos se a instalação está ativa ou inativa: responde id e nome sempre que a
  configuração e o marcador do banco conferem; caso contrário, indisponibilidade
  genérica, sem detalhes internos.
  Após a entrada, a interface DEVE mostrar a identidade da conta, a empresa corrente
  (reconhecível durante todo o uso autenticado), as permissões efetivas e a ação de sair.
  Cada permissão efetiva DEVE ser exibida com sua descrição pt-BR e seu código, ambos
  devolvidos pela consulta de contexto da API; a interface NÃO DEVE manter tabela própria
  de descrições. A tela DEVE mostrar, no fuso local e como texto estático, o horário
  do teto absoluto da sessão informado pela API e a regra de inatividade vigente
  (ex.: "Sessão válida até 18:35, ou após 30 min sem uso"), sem contagem regressiva,
  aviso prévio ou ação de renovar.
  Se a conta autenticada não tiver permissão para consultar o próprio contexto, a
  interface DEVE mostrar identidade e empresa com aviso de ausência dessa permissão,
  sem listar permissões e oferecendo somente sair; isso NÃO DEVE ser tratado como
  sessão inválida, empresa indisponível ou erro genérico, nem conceder permissão implícita.
  Na L3, a interface NÃO DEVE ter rotas, telas, menus ou listas de módulos; módulos
  habilitados (id e versão do contrato) são informados somente na consulta do contexto
  de acesso da API, que exige sessão válida e `foundation.context.read`; nenhuma operação
  anônima, inclusive a identidade pública do tenant e o documento de contrato servido
  pela instância, os revela, e nada de módulo ausente é exibido ou anunciado (FR-018).
  Quando uma chamada de uma aba já autenticada, exceto o pedido de sair, é negada por
  sessão expirada (inatividade ou teto absoluto), revogada ou ausente, a interface DEVE
  fazer nova entrada no próprio local: ocultar todo o conteúdo protegido da tela (dados não podem permanecer visíveis
  atrás do diálogo), preservar em memória o estado da aba e apresentar diálogo de
  credenciais com aviso de sessão encerrada e opção de sair. Se a nova entrada
  autenticar a mesma conta, a tela DEVE ser restaurada com o estado preservado; leituras
  PODEM ser repetidas automaticamente, mas gravações NÃO DEVEM ser reenviadas sem ação do
  usuário. Se autenticar outra conta, ou se o usuário escolher sair, todo o estado local
  DEVE ser descartado e a interface segue para a tela de contexto da nova conta ou para
  a entrada. Esse estado NÃO DEVE ser gravado no armazenamento do navegador e se perde
  ao fechar a aba. Quando a chamada é negada por empresa inativa, a interface DEVE
  apresentar empresa indisponível, permitindo somente sair (FR-006). A interface NÃO DEVE
  controlar a expiração por temporizador próprio, fazer requisições periódicas que
  mantenham a sessão ativa, nem renovar o acesso sem nova digitação de credenciais.
  Logout explícito em uma aba DEVE ser comunicado às demais abas abertas da mesma origem
  por mensagem entre abas, sem gravar dados no armazenamento do navegador; elas DEVEM
  descartar o estado de identidade e contexto e voltar à entrada. Expiração ou revogação
  detectada em uma aba NÃO DEVE descartar o estado das outras: cada aba aplica a nova
  entrada no próprio local na sua próxima chamada negada ou na revalidação no retorno de
  foco; se essa revalidação encontrar sessão válida da mesma conta (nova entrada feita em
  outra aba), a aba é restaurada sem novo diálogo, e se encontrar outra conta, descarta
  o estado. Na ausência do aviso de logout, a revalidação no retorno de foco ou a próxima
  chamada negada DEVE levar à entrada sem exibir dados da conta encerrada.
  Se o pedido de sair falhar por indisponibilidade ou erro inesperado, a interface
  DEVE permanecer na tela autenticada, exibir o erro com a referência copiável e
  oferecer nova tentativa de sair; NÃO DEVE descartar o estado nem avisar outras abas
  como se a saída tivesse ocorrido. Resposta de sessão já inválida (expirada, revogada
  ou ausente) ao pedido de sair DEVE concluir a saída localmente: descartar todo o estado
  da aba, ir à entrada com aviso de sessão encerrada e avisar as demais abas como logout
  explícito; esse caso NÃO abre a nova entrada no próprio local.
  Erros DEVEM ser apresentados em pt-BR por categoria distinta: credenciais inválidas,
  excesso de tentativas, conflito de sessão (nova tentativa a partir do contexto atual),
  indisponibilidade e erro inesperado. Indisponibilidade e erro inesperado DEVEM exibir
  a referência de investigação devolvida pela API, copiável (FR-023); credenciais
  inválidas DEVEM mostrar somente mensagem genérica, sem revelar conta, bloqueio ou empresa.
  Excesso de tentativas DEVE informar o tempo aproximado de espera indicado pela API e
  manter o envio do login desabilitado até esse prazo; depois, o envio é liberado sem
  nova tentativa automática, e a mensagem NÃO DEVE revelar conta ou bloqueio de conta.
  Enquanto uma leitura ou envio está pendente, a interface DEVE exibir indicador de
  carregamento anunciado a tecnologias assistivas e DEVE impedir novo envio do login
  até a resposta. Indisponibilidade DEVE oferecer ação manual de tentar novamente; a
  interface NÃO DEVE repetir automaticamente o login nem leituras de identidade ou contexto.
  Exceção única: conflito de sessão no diálogo da nova entrada no próprio local DEVE
  disparar uma só revalidação da identidade da sessão; se ela encontrar sessão válida da
  mesma conta (nova entrada concluída em outra aba), a aba é restaurada sem diálogo; se
  encontrar outra conta, o estado é descartado; se a sessão for inválida, o diálogo
  permanece com a mensagem de conflito. O login nunca é reenviado automaticamente.
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
  A sessão DEVE expirar no que ocorrer primeiro: inatividade (tempo sem requisição
  autenticada aceita) ou teto absoluto contado da sua criação. Cada requisição
  autenticada aceita DEVE renovar a janela de inatividade; requisições negadas não a
  renovam. A renovação NUNCA DEVE ultrapassar o teto absoluto, e não há remember-me nem
  refresh. Requisição autenticada aceita é a requisição a operação protegida por sessão
  que passou por sessão válida, instalação ativa, proteção contra CSRF e permissão e
  chegou à operação, mesmo que esta recuse a entrada (400/422); negações de acesso
  (401, 403, 409, 429, 503), login, logout, obtenção do token CSRF, identidade pública
  do tenant, health, documento de contrato e arquivos da interface não renovam.
  Revalidações da interface por carga, retorno de foco/visibilidade ou navegação que
  consultam a identidade da sessão são requisições autenticadas aceitas e renovam a
  inatividade (nunca o teto); por isso só podem ser disparadas por esses eventos, nunca
  por temporizador (FR-004).
  Padrões: inatividade de 30 minutos e teto absoluto de 600 minutos (10 horas).
  Produtos derivados PODEM configurar, em minutos inteiros e limites inclusivos, a
  inatividade entre 5 e 240 e o teto absoluto entre 30 e 720; valor ausente ou vazio usa
  o padrão, e valor fora da faixa, não inteiro ou inatividade maior que o teto DEVE
  impedir a inicialização com diagnóstico seguro (FR-003). A configuração anterior de
  prazo absoluto único, se presente com valor, também DEVE impedir a inicialização, com
  diagnóstico que indica as novas configurações; vazia é tratada como ausente. Teto e janela de inatividade
  são fixados na criação de cada sessão; alterar a configuração não muda sessões já
  emitidas. A verificação de expiração ocorre no servidor a cada requisição; sessão
  expirada nunca volta a autorizar uso. O registro do último uso PODE ser gravado no
  máximo uma vez por minuto por sessão; com isso, a negação por inatividade DEVE ocorrer
  no máximo ao fim da janela contada da última requisição autenticada aceita e NÃO DEVE
  ocorrer antes da janela menos 60 segundos (tolerância sempre a favor da segurança).
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
  Estado anterior suportado é somente o de uma versão publicada com dados; até a
  primeira publicação (L4), mudanças de esquema regeneram a migration inicial única e
  bancos locais e de CI são recriados do zero. A partir dela, toda mudança de esquema
  DEVE ser migration incremental validada em banco novo e na atualização da versão publicada.
- **FR-016**: Leitura, gravação e tratamento de falhas por empresa DEVEM comprovar,
  com dados sintéticos, os resultados e efeitos persistidos esperados, inclusive
  restrições e transações quando aplicáveis, concorrência e reutilização de recursos.
- **FR-017**: Cada módulo acrescentado a um produto DEVE declarar responsabilidade,
  dados sob sua gestão, contratos públicos e dependências; consumo DEVE ocorrer por contratos.
  Na L3, módulo é um componente de backend composto por projetos .NET próprios,
  separando contratos públicos de implementação, com declaração em código registrada
  na inicialização. A interface não define módulos próprios nesta entrega.
  O id do módulo DEVE ter de 2 a 32 caracteres, somente letras minúsculas sem acento,
  dígitos e hífen, começando por letra e sem ponto; `foundation` é reservado à fundação.
  Id inválido, reservado ou duplicado DEVE falhar a inicialização com diagnóstico seguro
  e os testes de arquitetura.
  Permissões de um módulo DEVEM ser declaradas por ele em código, com código iniciado
  por `<id do módulo>.` e descrição pt-BR não vazia; o catálogo efetivo de permissões é o
  da fundação somado aos dos módulos registrados, e o procedimento de concessão DEVE
  rejeitar código fora dele. Permissões da fundação também DEVEM ter descrição pt-BR.
  Prefixo divergente, código duplicado ou descrição ausente DEVE falhar a inicialização com diagnóstico
  seguro e ser rejeitado pelos testes de arquitetura (FR-019). Concessões existentes
  a códigos de módulo omitido NÃO DEVEM autorizar operação alguma e não são apagadas
  automaticamente.
  Na L3, a declaração de dados sob gestão do módulo DEVE ser vazia: módulo que declarar
  dados persistidos DEVE falhar a inicialização e os testes de arquitetura. Persistência
  própria de módulos (schema e migrations separados) fica para o primeiro módulo de
  negócio, com novo plano; módulos NÃO DEVEM alterar a migration da fundação.
- **FR-018**: Um produto DEVE funcionar sem módulos de negócio ou com um subconjunto
  compatível de módulos, sem expor interfaces de módulos ausentes ou quebrar a fundação.
  A seleção de módulos ocorre na compilação: incluir um módulo é referenciá-lo e
  registrá-lo no host; omiti-lo é remover essa referência. Não há ativação ou
  desativação de módulos por configuração em runtime na L3.
- **FR-019**: Dependências circulares, obrigatórias não atendidas ou acesso às
  implementações internas de outro módulo DEVEM ser identificados antes da liberação.
  A verificação DEVE ocorrer em dois pontos: na inicialização, que DEVE falhar com
  diagnóstico seguro diante de ciclo ou dependência obrigatória ausente, e em testes
  de arquitetura bloqueantes no CI, que DEVEM rejeitar ciclos, dependências não
  declaradas e referência à implementação de outro módulo em vez de seus contratos.
  Cada contrato público de módulo DEVE ter versão maior inteira; a dependência DEVE
  declarar id do contrato e versão maior exigida. Contrato ausente ou com versão maior
  diferente DEVE ser tratado como dependência não atendida. Mudança compatível de
  contrato NÃO DEVE alterar a versão maior; mudança incompatível DEVE incrementá-la (FR-021).
- **FR-020**: A fundação DEVE oferecer integração para consumidores independentes,
  inclusive consulta do próprio contexto autorizado, sem dependência da interface web.
- **FR-021**: Os contratos de integração DEVEM documentar operações disponíveis, entradas,
  respostas, autenticação, contexto de empresa, permissões e erros; alteração incompatível
  DEVE declarar consumidores afetados e estratégia de transição.
  O documento de contrato servido anonimamente pela instância DEVE descrever somente
  operações da fundação e ser idêntico com ou sem módulos registrados. Contratos de
  módulos DEVEM ser publicados como arquivos versionados no repositório do produto,
  sem serem servidos pela instância.
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
  Na L3, a interface DEVE ter lint, verificação estrita de tipos, testes de unidade e
  de componentes e testes ponta a ponta de entrada, contexto, saída e estados de erro
  contra a stack local com o backend servindo a interface, além do orçamento de
  tamanho do NFR-013; todos bloqueiam integração.
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
  que encerra todas as sessões da conta. Na L3 isso se mantém: troca de senha pelo
  próprio usuário e recuperação por email/token ficam fora da L3 e exigem novo plano.
  O procedimento restrito DEVE também remover sessões expiradas ou revogadas há
  mais que a retenção configurada, com auditoria e sem afetar sessões válidas.
  A retenção padrão é de 30 dias, configurável em dias inteiros de 1 a 365, contada
  da expiração ou da revogação; valor fora da faixa DEVE impedir a limpeza com
  diagnóstico seguro. Execução auditável começa com operador individual autenticado
  e alvo validado; retenção inválida ou login não autorizado encerram antes, sem
  registro e sem efeito. Cada execução DEVE gerar exatamente um registro imutável,
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
  Negação por sessão inválida DEVE responder com categoria genérica única, sem revelar
  o motivo ao cliente, e DEVE registrar no log do backend o motivo categorizado
  (inatividade, teto absoluto, revogada, ausente ou inválida) com a referência de
  investigação, sem identificador de sessão, ticket ou cookie.
  Na L3, a interface NÃO DEVE enviar telemetria ou relatos de erro do navegador a
  terceiros nem ao próprio backend; a investigação de falhas da interface usa a
  referência exibida ao usuário (FR-023) e os registros do backend. Relato de erros
  do frontend fica para a L4 ou para o produto derivado, com justificativa.
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
- **NFR-011 — Acessibilidade e idioma da interface**: a interface de acesso DEVE estar
  em pt-BR, sem mecanismo de tradução na L3, e atender WCAG 2.1 nível AA (operação
  por teclado, foco visível, rótulos, contraste e erros anunciados). A verificação
  automática nos testes ponta a ponta DEVE registrar zero violações sérias ou críticas.
  São suportadas as duas últimas versões estáveis de Chrome, Edge e Firefox em desktop;
  os testes ponta a ponta bloqueantes executam em Chromium. Safari e navegadores
  móveis não têm garantia formal na L3.
- **NFR-012 — Proteção, cache e atualização da interface no navegador**: as respostas que entregam a
  interface DEVEM aplicar política de segurança de conteúdo estrita (somente recursos
  da própria origem, sem scripts ou estilos inline, nonce ou hash, e sem incorporação
  em frames de outra origem; o CSS DEVE ser entregue em arquivos gerados no build),
  bloqueio de detecção de tipo de conteúdo e política de referência restrita. A página
  inicial NÃO DEVE ser armazenada em cache; recursos versionados PODEM ter cache longo.
  A interface NÃO DEVE usar service worker ou cache offline. A operação pública de
  identidade do tenant DEVE informar um identificador opaco do build publicado, sem
  número de versão, commit, data ou ambiente; a interface o consulta somente em eventos
  (carga, retorno de foco, navegação), sem polling curto. Quando esse identificador
  diferir do embutido na interface carregada, ela DEVE avisar que há nova
  versão e oferecer recarregar; falha ao carregar um arquivo da interface DEVE causar
  uma única recarga automática, sem laço. Nenhum procedimento DEVE exigir que o
  usuário limpe o cache do navegador para receber uma atualização.
  Toda resposta da API, inclusive erros e a identidade pública do tenant, DEVE proibir
  armazenamento em cache por navegador e intermediários. A interface PODE manter cache
  em memória da aba, mas NÃO DEVE persistir dados de conta, contexto ou empresa no
  armazenamento do navegador e DEVE descartá-lo no logout, ao concluir a saída após
  negação do pedido de sair, na troca de conta ou ao sair pelo diálogo de nova entrada;
  em negação por sessão inválida de uma aba autenticada, ele é preservado em memória,
  oculto e inerte, conforme a nova entrada no próprio local (FR-004). Testes automatizados DEVEM verificar essas regras e proteções.
  HSTS pertence à publicação (L4).
- **NFR-013 — Orçamento de tamanho da interface**: o JavaScript do carregamento inicial
  da interface (entrada e contexto) DEVE somar no máximo 200 KB comprimidos (gzip),
  verificado de forma bloqueante no CI; código carregado sob demanda não entra nessa
  soma. Produtos derivados PODEM ajustar o limite com justificativa documentada.
  Não há meta de tempo de carga na L3.

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
- **Permissão**: autorização para uma operação protegida em um contexto definido,
  identificada por código e com descrição pt-BR declarada em código no catálogo;
  ausência de concessão implica acesso negado. Dados de concessão e de sessão
  pertencem à mesma fonte de dados da conta e não são compartilhados entre tenants.
- **Definição de módulo**: id, responsabilidade, contratos (com versão maior),
  dependências (id do contrato e versão maior exigida) e permissões
  próprias (código prefixado pelo id e descrição pt-BR) de componente opcional acrescentado por um
  produto, sem representar módulo de negócio entregue.
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
permanecem metas da evolução da fundação completa, com o recorte de aceite de cada
incremento definido no cabeçalho "Recorte vigente" (na L3, SC-001 e SC-004). As metas de tempo e volume não são
promessas de capacidade para todos os produtos derivados.

### Measurable Outcomes

- **SC-001**: um desenvolvedor que não escreveu o guia consegue executar a fundação
  e chegar à interface de acesso autenticada em até 45 minutos a partir de um clone
  limpo, com pré-requisitos instalados, sem assistência do autor, registrando tempo,
  revisão e dúvidas encontradas. Além disso, o mesmo roteiro, automatizado em ambiente
  limpo no CI (stack local e teste ponta a ponta), conclui em até 45 minutos.
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
  Com os padrões, sessão sem requisição autenticada aceita por 30 minutos é negada;
  sessão usada a intervalos de no máximo 29 minutos continua válida até 600 minutos
  após sua criação e é negada a partir daí, mesmo em uso (tolerância de até 60 segundos
  antecipando a negação por inatividade, FR-006). Requisições negadas não
  renovam a inatividade. Inatividade de 5 e 240 e teto de 30 e 720 minutos são aceitos;
  configuração ausente usa 30/600; inatividade 4 ou 241, teto 29 ou 721, valor não
  inteiro, inatividade maior que o teto ou a configuração anterior de prazo único
  preenchida impedem a inicialização sem expor secrets.
  Com duas sessões simultâneas da mesma conta, logout em uma DEVE preservar a outra;
  reset, desativação, bloqueio ou revogação operacional DEVEM invalidar ambas.
  Falha na troca de sessão em novo login DEVE confirmar zero novas sessões e
  preservar o estado e prazo anteriores, respeitando bloqueios e revogações
  confirmados independentemente.
  Dois logins válidos concorrentes com a mesma sessão anterior DEVEM confirmar
  exatamente uma substituição e zero sessões adicionais pela tentativa perdedora,
  que recebe erro explícito; outras sessões independentes permanecem válidas.
  Inativação/reativação da instalação DEVE preservar o teto original e não pausar a
  inatividade: sessão ainda válida e não revogada pode retomar; sessão expirada por
  inatividade ou teto, ou revogada, permanece negada.
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
  O componente de teste é um módulo de backend com uma única operação sintética
  protegida por permissão própria e dependência declarada de um contrato, carregado
  somente pelos testes. Na variante com o componente, a operação responde conforme
  a permissão (concedida → sucesso; ausente → negada); na variante sem módulos, ela
  não existe e a interface não a anuncia. O documento de contrato servido pela instância
  DEVE ser idêntico nas duas variantes. Solução e imagem distribuídas DEVEM ter
  zero referências ao componente, verificado automaticamente no CI.
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
