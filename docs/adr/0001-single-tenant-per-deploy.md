# 1. Implantação dedicada por cliente (Single-Tenant por Deploy)

Data: 2026-10-30

## Status

Aceito

## Contexto

A plataforma boilerplate destina-se a atender empresas/clientes corporativos que exigem isolamento rigoroso de dados, conformidade regulatória e autonomia operacional. Avaliamos duas abordagens fundamentais de arquitetura multitenancy:

1. **Multi-tenant Compartilhado (Catálogo Central + Roteamento Dinâmico):** Um único backend compartilhado roteia requisições em tempo de execução para bancos de dados distintos com base em cabeçalhos ou subdomínios, mantendo um banco central `platform_catalog` e credenciais compartilhadas em memória.
2. **Single-Tenant por Deploy (Instalação Dedicada por Cliente):** Cada cliente possui sua própria instância de backend executável conectada exclusivamente ao seu próprio banco de dados PostgreSQL dedicado (relação 1:1), sem banco de catálogo central (`platform_catalog`). A identidade do cliente (`TenantId`, `EnvironmentId`) e as conexões de banco são configuradas via variáveis de ambiente da aplicação/container no Dokploy ou Docker Compose.

## Decisão

Adotamos a **Opção 2: Implantação dedicada por cliente (Single-Tenant por Deploy)**:

- **1 Backend + 1 Banco PostgreSQL por Instalação:** Cada cliente opera em seu próprio container de backend e sua própria base PostgreSQL isolada.
- **Eliminação de Catálogo Central:** Não existe banco de dados central `platform_catalog`, nem entidade `CatalogDbContext`, eliminando o risco de um único ponto de falha (*Single Point of Failure*) e vazamento cruzado entre inquilinos por credencial compartilhada.
- **Marcador Físico de Integridade (`TenantDatabaseIdentity`):** Cada banco de dados contém uma tabela singleton no schema `foundation` com o `TenantId`, `EnvironmentId` e `AccessEpoch`. O backend em runtime valida esse marcador contra as variáveis de ambiente antes de aceitar qualquer requisição de negócio, impedindo que uma instalação aponte acidentalmente para a base de outro cliente.
- **Gerenciamento de Segredos e Conexões no Dokploy/VPS:** As variáveis de ambiente e conexões de banco são provisionadas de forma isolada por aplicação/serviço no painel Dokploy na VPS ou em arquivos `.env*.local` no ambiente local.
- **Separação Estrita de Privilégios:** O runtime da aplicação opera com credencial de acesso sem permissões de DDL (`CREATE`, `ALTER`, `DROP`). As migrações de esquema são executadas exclusivamente de forma pontual via tooling (`dotnet-ef` no profile `migrations`), com aprovação explícita e controlada (`Migrations__AllowInitializeEmptyDatabase`).

## Consequências

### Positivas
- **Isolamento Físico e Segurança Absoluta:** Impossibilidade arquitetural de vazamento acidental de dados entre clientes via falha de código ou injeção de parâmetros em runtime.
- **Ciclo de Vida Independente:** Cada cliente pode ter rotinas de backup, restore, janelas de manutenção e atualizações de versão de forma independente sem impactar outros clientes.
- **Simplicidade de Domínio e Código:** O código da aplicação não precisa injetar filtros globais em consultas EF Core (`HasQueryFilter(e => e.TenantId == ...)`), reduzindo a complexidade de desenvolvimento e otimizando consultas SQL.
- **Facilidade de Backup e Restauração:** O backup e restore de uma empresa é uma operação nativa de banco (`pg_dump`/`pg_restore`), sem necessidade de extrair dados específicos de tabelas compartilhadas.

### Negativas / Mitigações
- **Consumo de Recursos:** Cada instalação executa sua própria instância do backend .NET.
  - *Mitigação:* A imagem de runtime .NET 10 é enxuta (~100-150MB de RAM ociosa), e o Dokploy orquestra múltiplos containers com baixo overhead em uma única VPS de 4GB.
- **Gestão Operacional de Múltiplos Deploys:** Requer ferramentas de orquestração para gerenciar múltiplas instâncias.
  - *Mitigação:* O Dokploy oferece gerenciamento centralizado de serviços via UI e API, com provisionamento automatizado de certificados SSL (Let's Encrypt) e configuração declarativa via Docker Compose.
