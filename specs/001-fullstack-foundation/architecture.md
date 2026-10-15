# Desenho da arquitetura sugerida

Este desenho consolida o plano da fundação no modelo **single-tenant por deploy
(1 instalação de backend + 1 banco PostgreSQL exclusivo da própria aplicação por cliente)**
e diferencia o primeiro incremento (I1) da arquitetura evolutiva. Não existe banco apartado
de catálogo central (`platform_catalog`).

## Arquitetura evolutiva (implantação dedicada por cliente)

```mermaid
flowchart LR
    UserA[Usuário do Cliente A] -->|I3: mesma origem| WebA[Frontend web - Cliente A<br/>React + TypeScript]
    WebA -->|I2+: HTTPS / REST| ApiA
    ConsumerA[Consumidor independente] -->|I2+: HTTPS / REST| ApiA
    Operator[Operador autorizado] -->|procedimento restrito| OpsA[Migrations e operações<br/>via tooling controlado]

    subgraph StackA[Instalação dedicada — Cliente A no Dokploy / VPS]
        EnvA[Variáveis de ambiente / Secrets no Dokploy<br/>TenantId, EnvironmentId, ConnectionStrings__AppDb]
        subgraph BackendA[Backend: monólito modular ASP.NET Core]
            ApiA[App.Api<br/>transporte, composição e health checks]
            AppA[Platform.Application<br/>casos de uso e portas]
            DomainA[Platform.Domain<br/>regras e modelos centrais]
            InfraA[Platform.Infrastructure<br/>Identity, EF Core e persistência]

            ApiA --> AppA
            AppA --> DomainA
            InfraA -. implementa portas .-> AppA
            InfraA --> DomainA
        end
        DbA[(Banco exclusivo do Cliente A<br/>TenantDatabaseIdentity + contas + permissões + dados)]
    end

    EnvA -. injeta segredos no container .-> ApiA
    ApiA --> InfraA
    InfraA -->|valida marcador do banco e opera| DbA
    OpsA -->|credencial owner apenas no tooling| DbA

    subgraph StackB[Instalação dedicada — Cliente B no Dokploy / VPS]
        EnvB[Variáveis de ambiente / Secrets do Cliente B]
        ApiB[Backend exclusivo — Cliente B]
        DbB[(Banco exclusivo do Cliente B)]
        EnvB -.-> ApiB --> DbB
    end

    subgraph Delivery[Entrega e operação]
        CI[GitHub Actions<br/>build, testes e verificações]
        Compose[Docker Compose<br/>ambiente local]
        Deploy[I4: VPS Linux + Dokploy<br/>HTTPS / Traefik e backups externos]
    end

    CI -. valida código e isolamento .-> BackendA
    Compose -. executa localmente .-> StackA
    Deploy -. publica stacks isoladas .-> StackA
    Deploy -. publica stacks isoladas .-> StackB
```

### Regras importantes

- **Sem banco de catálogo central (`platform_catalog`)**: só existe o banco da própria
  aplicação em cada instalação de cliente.
- **Segredos por cliente no Dokploy / VPS (ou cofre externo)**: cada cliente é uma
  aplicação/stack separada no Dokploy, onde suas variáveis de ambiente e segredos são
  configurados e injetados apenas nos containers daquele cliente.
- **Isolamento total de runtime e banco**: o backend do Cliente A não conhece a conexão
  nem o banco do Cliente B. Antes de operar, valida o marcador `TenantDatabaseIdentity`
  gravado no seu banco contra o `TenantId` e `EnvironmentId` da sua configuração.
- **Contas locais ao banco da instalação**: cada cliente possui suas contas, credenciais,
  permissões e sessões no próprio banco. Email igual em dois clientes não compartilha conta.
- **Migrations fora do startup**: a API não aplica migrations no startup. O tooling executa
  migrations somente após validar a identidade do banco-alvo e a credencial owner.

## Recorte do primeiro incremento (I1)

```mermaid
flowchart LR
    Dev[Desenvolvedor] --> Compose[Docker Compose + .env.local]
    Compose --> Api[App.Api<br/>health e OpenAPI de I1]
    Compose --> Pg[(Banco PostgreSQL da instalação)]

    Operator[Operador autorizado] --> Tool[EF CLI / profile migrations]
    Tool -->|preflight + migration em TenantAccessDbContext| Pg

    Api -->|readiness: valida configuração e marcador TenantDatabaseIdentity| Pg
    Api -. sem endpoints de conta,<br/>login ou negócio em I1 .-> NoRoutes[Sem superfície de usuário]

    CI[Checks mínimos antes da integração] --> Tests[Testes unitários,<br/>contratuais e de integração]
    Tests -->|PostgreSQL real descartável<br/>simulando 2 instalações A e B| TestPg[(PostgreSQL de teste<br/>via Testcontainers)]
```

I1 entrega backend executável, `TenantAccessDbContext` com marcador da instalação e
modelo/migrations de usuários e permissões, execução local via Docker Compose, testes
com PostgreSQL real (validando duas bases independentes A e B) e checks de backend.
Não inclui banco de catálogo central, frontend, login HTTP, painel, endpoints de conta
ou módulo de negócio.

## Evolução prevista

| Incremento | Acréscimo principal |
|---|---|
| I1 — atual | Backend, marcador da instalação, persistência e migrations de usuários/acesso, Compose e validação de backend. |
| I2 — posterior | Login/logout e contexto na instalação do cliente, sessão revogável e procedimento restrito de contas/permissões. |
| I3 — posterior | Frontend web e validação da extensão modular. |
| I4 — antes de produção | Publicação de instâncias por cliente no Dokploy/VPS, backup externo e recuperação. |

**Referência:** [plano de implementação](./plan.md) e [especificação](./spec.md).
