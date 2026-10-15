# Desenho da arquitetura sugerida

Este desenho consolida o plano da fundação e diferencia o primeiro incremento
(I1) da arquitetura evolutiva. Os componentes indicados como posteriores não fazem
parte da entrega inicial.

## Arquitetura evolutiva

```mermaid
flowchart LR
    User[Usuário] -->|I3: mesma origem| Web[Frontend web<br/>React + TypeScript]
    Web -->|I2+: HTTPS / REST| Api
    Consumer[Consumidor independente] -->|I2+: HTTPS / REST| Api
    Operator[Operador autorizado] -->|procedimento restrito| Ops[Migrations e operações<br/>via tooling controlado]

    subgraph Backend[Backend: monólito modular ASP.NET Core]
        Api[App.Api<br/>transporte, composição e health checks]
        App[Platform.Application<br/>casos de uso e portas]
        Domain[Platform.Domain<br/>regras e modelos centrais]
        Infra[Platform.Infrastructure<br/>Identity, EF Core e persistência]

        Api --> App
        App --> Domain
        Infra -. implementa portas .-> App
        Infra --> Domain
    end

    Api --> Infra
    Ops -->|preflight e migration explícita| Infra

    subgraph Data[PostgreSQL: bancos separados]
        Catalog[(platform_catalog<br/>tenants, estado e StorageBinding)]
        TenantA[(Banco exclusivo<br/>Tenant A)]
        TenantB[(Banco exclusivo<br/>Tenant B)]
    end

    Infra -->|resolve alvo confiável| Catalog
    Infra -->|somente após validação do tenant| TenantA
    Infra -->|somente após validação do tenant| TenantB
    TenantA --- AData[Contas, credenciais,<br/>permissões e dados do tenant]
    TenantB --- BData[Contas, credenciais,<br/>permissões e dados do tenant]
    Ops -->|credencial owner apenas no tooling| Data

    subgraph Delivery[Entrega e operação]
        CI[GitHub Actions<br/>build, testes e verificações]
        Compose[Docker Compose<br/>ambiente local]
        Deploy[I4: VPS Linux + Dokploy<br/>HTTPS / Traefik e backups]
    end

    CI -. valida .-> Backend
    CI -. valida .-> Data
    Compose -. executa localmente .-> Api
    Compose -. serviço local .-> Data
    Deploy -. publica posteriormente .-> Api
    Deploy -. volumes e cópias externas .-> Data
```

### Regras importantes

- O catálogo central contém apenas metadados técnicos de tenants e referências
  seguras aos bancos; não armazena contas globais nem dados de negócio.
- A aplicação resolve o destino pelo catálogo confiável. Um identificador enviado
  pelo cliente não fornece credenciais nem autoriza, por si só, acesso ao tenant.
- Cada tenant tem banco próprio, incluindo suas contas, credenciais e permissões.
  A mesma pessoa ou email em dois tenants não compartilha identidade ou acesso.
- A API não aplica migrations no startup. O tooling executa migrations somente
  após validar o alvo, seu estado e a autorização operacional.
- A seta de frontend, login e operações de tenant representa incrementos posteriores;
  não indica endpoints de usuário disponíveis em I1.

## Recorte do primeiro incremento (I1)

```mermaid
flowchart LR
    Dev[Desenvolvedor] --> Compose[Docker Compose]
    Compose --> Api[App.Api<br/>health e OpenAPI de I1]
    Compose --> Pg[(PostgreSQL local)]

    Operator[Operador autorizado] --> Tool[EF CLI / tooling de migration]
    Tool -->|preflight no catálogo| Catalog[(platform_catalog)]
    Tool -->|migration explícita para alvo inativo| TenantA[(Banco do Tenant A)]
    Tool -->|migration explícita para alvo inativo| TenantB[(Banco do Tenant B)]

    Api -->|readiness: configuração e identidade/versão| Catalog
    Api -. sem endpoints de conta,<br/>login ou negócio em I1 .-> NoRoutes[Sem superfície de usuário]

    CI[Checks mínimos antes da integração] --> Tests[Testes unitários,<br/>contratuais e de integração]
    Tests -->|PostgreSQL real descartável| TestPg[(PostgreSQL de teste<br/>via Testcontainers)]
```

I1 entrega backend executável, catálogo técnico mínimo, modelo e migrations de
usuários/acesso, execução local, testes com PostgreSQL e checks de backend. Não inclui
frontend, login HTTP, painel, endpoints de conta ou módulo de negócio. A API não cria
bancos nem altera esquemas durante o startup.

## Evolução prevista

| Incremento | Acréscimo principal |
|---|---|
| I1 — atual | Backend, persistência e migrations de usuários/acesso, Compose e validação de backend. |
| I2 — posterior | Login/logout e contexto por tenant, sessão revogável e procedimento restrito de contas/permissões. |
| I3 — posterior | Frontend web e validação da extensão modular. |
| I4 — antes de produção | Operação ampliada de tenants, publicação, backup e recuperação. |

**Referência:** [plano de implementação](./plan.md) e [especificação](./spec.md).
