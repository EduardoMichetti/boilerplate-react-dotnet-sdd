namespace Platform.IntegrationTests.Tenancy;

using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Platform.IntegrationTests.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T116: Testes do estado da instalação, preflight de marcador e cadeia de migrations (US3 / FR-009, FR-011–FR-016).
/// Garante que marcador divergente, banco indisponível ou migração futura retornem 503 DEPENDENCY_UNAVAILABLE
/// nas cinco rotas e na readiness, que configuração cruzada A->B bloqueie antes de dados, e que inativação
/// suspenda consultas sem revogar sessões nem alterar epoch/prazos.
/// </summary>
public sealed class TestesEstadoInstalacaoEMarcador(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    private const string IdMigracaoFutura = "29991231000000_MigracaoDeVersaoFutura";

    [Fact]
    public async Task MarcadorDivergente_Retorna503NasCincoRotasENaReadiness()
    {
        Guid tenantDivergente = Guid.NewGuid();
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi { IdTenantConfigurado = tenantDivergente });
        ClienteAcessoHttp cliente = CriarCliente(api);

        // Readiness retorna 503
        using (HttpResponseMessage ready = await api.CreateClient().GetAsync("/health/ready", TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        }

        // As cinco rotas L2 retornam 503 DEPENDENCY_UNAVAILABLE
        await AssegurarRotasRetornam503Async(cliente);
    }

    [Fact]
    public async Task BancoIndisponivel_Retorna503NasCincoRotasENaReadiness()
    {
        // Conexão apontando para porta fechada
        NpgsqlConnectionStringBuilder csb = new(A.StringConexaoExecucao)
        {
            Host = "127.0.0.1",
            Port = 1,
            Timeout = 1,
            CommandTimeout = 1,
        };

        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi
        {
            Extras = new Dictionary<string, string?>
            {
                ["StringsConexao:BancoApp"] = csb.ConnectionString,
            },
        });
        ClienteAcessoHttp cliente = CriarCliente(api);

        using (HttpResponseMessage ready = await api.CreateClient().GetAsync("/health/ready", TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        }

        await AssegurarRotasRetornam503Async(cliente);
    }

    [Fact]
    public async Task HistoricoComIdDesconhecido_Retorna503NasCincoRotasENaReadiness()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;

        // Insere migration desconhecida no histórico do banco
        await using (NpgsqlConnection conexaoOwner = new(A.StringConexaoProprietario))
        {
            await conexaoOwner.OpenAsync(cancelamento);
            await using NpgsqlCommand comando = new(
                $"INSERT INTO foundation.\"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{IdMigracaoFutura}', '10.0.12')",
                conexaoOwner);
            await comando.ExecuteNonQueryAsync(cancelamento);
        }

        try
        {
            FabricaApiInstalacao api = CriarApi(A);
            ClienteAcessoHttp cliente = CriarCliente(api);

            using (HttpResponseMessage ready = await api.CreateClient().GetAsync("/health/ready", cancelamento))
            {
                Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
            }

            await AssegurarRotasRetornam503Async(cliente);
        }
        finally
        {
            // Remove a migration sintética para não afetar outros testes
            await using (NpgsqlConnection conexaoOwner = new(A.StringConexaoProprietario))
            {
                await conexaoOwner.OpenAsync(cancelamento);
                await using NpgsqlCommand comando = new(
                    $"DELETE FROM foundation.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{IdMigracaoFutura}'",
                    conexaoOwner);
                await comando.ExecuteNonQueryAsync(cancelamento);
            }
        }
    }

    [Fact]
    public async Task ConfiguracaoDeAApontandoParaB_BloqueiaAntesDeDados()
    {
        // Instalação A configurada apontando para o banco de dados de B
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi
        {
            Extras = new Dictionary<string, string?>
            {
                ["StringsConexao:BancoApp"] = B.StringConexaoExecucao,
            },
        });
        ClienteAcessoHttp cliente = CriarCliente(api);

        using (HttpResponseMessage ready = await api.CreateClient().GetAsync("/health/ready", TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        }

        await AssegurarRotasRetornam503Async(cliente);
    }

    [Fact]
    public async Task InativacaoEReativacao_SuspendeConsultasSemRevogarSessoesEPermiteLogout()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;

        string email1 = ContasSinteticas.NovoEmail("inativo_1");
        string email2 = ContasSinteticas.NovoEmail("inativo_2");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email1, ContasSinteticas.SenhaPadraoA);
        await ContasSinteticas.CriarContaAsync(A, Relogio, email2, ContasSinteticas.SenhaPadraoA);

        Guid epocaOriginal;
        await using (AcessoTenantDbContext contextoLeitura = BancoTeste.CriarContexto(A.StringConexaoExecucao))
        {
            IdentidadeBancoTenant marcador = await contextoLeitura.IdentidadeBanco.SingleAsync(cancelamento);
            epocaOriginal = marcador.EpocaAcesso;
        }

        // 1. Login no tenant A enquanto ATIVO
        FabricaApiInstalacao apiAtiva = CriarApi(A);
        ClienteAcessoHttp cliente1 = CriarCliente(apiAtiva);
        ClienteAcessoHttp cliente2 = CriarCliente(apiAtiva);

        await cliente1.ObterTokenAsync();
        using (HttpResponseMessage login1 = await cliente1.LoginAsync(email1, ContasSinteticas.SenhaPadraoA, novoToken: false))
        {
            Assert.Equal(HttpStatusCode.Created, login1.StatusCode);
        }

        await cliente2.ObterTokenAsync();
        using (HttpResponseMessage login2 = await cliente2.LoginAsync(email2, ContasSinteticas.SenhaPadraoA, novoToken: false))
        {
            Assert.Equal(HttpStatusCode.Created, login2.StatusCode);
        }

        // 2. Transição para INATIVO
        FabricaApiInstalacao apiInativa = CriarApi(A, new OpcoesFabricaApi
        {
            Estado = EstadoTenant.Inativo,
            DiretorioChaves = apiAtiva.DiretorioChaves,
        });

        ClienteAcessoHttp clienteInativo1 = CriarCliente(apiInativa);
        CopiarCookies(cliente1, clienteInativo1);

        ClienteAcessoHttp clienteInativo2 = CriarCliente(apiInativa);
        CopiarCookies(cliente2, clienteInativo2);

        // Novo login é bloqueado com 401 AUTH_INVALID sem consultar senha
        using (HttpResponseMessage loginBloqueado = await clienteInativo1.LoginAsync(email1, "QualquerSenha123!"))
        {
            await AssegurarProblemaAsync(loginBloqueado, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        }

        // Consultas /me e /context retornam 409 TENANT_UNAVAILABLE
        using (HttpResponseMessage meBloqueado = await clienteInativo1.MeAsync())
        {
            await AssegurarProblemaAsync(meBloqueado, HttpStatusCode.Conflict, "TENANT_UNAVAILABLE");
        }

        using (HttpResponseMessage contextoBloqueado = await clienteInativo1.ContextoAsync())
        {
            await AssegurarProblemaAsync(contextoBloqueado, HttpStatusCode.Conflict, "TENANT_UNAVAILABLE");
        }

        // Antiforgery e Logout continuam permitidos enquanto inativo
        Assert.Equal(HttpStatusCode.OK, await clienteInativo1.ObterTokenAsync());
        using (HttpResponseMessage logoutInativo = await clienteInativo1.LogoutAsync())
        {
            Assert.Equal(HttpStatusCode.NoContent, logoutInativo.StatusCode);
        }

        // Confere que o marcador não alterou sua época
        await using (AcessoTenantDbContext contextoLeitura = BancoTeste.CriarContexto(A.StringConexaoExecucao))
        {
            IdentidadeBancoTenant marcador = await contextoLeitura.IdentidadeBanco.SingleAsync(cancelamento);
            Assert.Equal(epocaOriginal, marcador.EpocaAcesso);
        }

        // 3. Reativação do tenant
        FabricaApiInstalacao apiReativada = CriarApi(A, new OpcoesFabricaApi
        {
            Estado = EstadoTenant.Ativo,
            DiretorioChaves = apiAtiva.DiretorioChaves,
        });

        ClienteAcessoHttp clienteReativado1 = CriarCliente(apiReativada);
        CopiarCookies(clienteInativo1, clienteReativado1);

        ClienteAcessoHttp clienteReativado2 = CriarCliente(apiReativada);
        CopiarCookies(clienteInativo2, clienteReativado2);

        // Cliente 1 fez logout -> sessão revogada continua 401 AUTH_REQUIRED
        using (HttpResponseMessage meRevogado = await clienteReativado1.MeAsync())
        {
            await AssegurarProblemaAsync(meRevogado, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        // Cliente 2 não fez logout -> sessão ainda válida é retomada com 200 OK
        using (HttpResponseMessage meValido = await clienteReativado2.MeAsync())
        {
            JsonElement corpo = await AssegurarSucessoAsync(meValido);
            Assert.Equal(email2, corpo.GetProperty("email").GetString());
        }
    }

    private static void CopiarCookies(ClienteAcessoHttp origem, ClienteAcessoHttp destino)
    {
        destino.Cookies.Clear();
        foreach ((string chave, string valor) in origem.Cookies)
        {
            destino.Cookies[chave] = valor;
        }

        destino.TokenCsrf = origem.TokenCsrf;
    }

    private static async Task AssegurarRotasRetornam503Async(ClienteAcessoHttp cliente)
    {
        // 1. Antiforgery
        using (HttpResponseMessage resp = await cliente.EnviarAsync(HttpMethod.Get, ClienteAcessoHttp.RotaAntiforgery))
        {
            await AssegurarProblemaAsync(resp, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        // 2. Login
        using (HttpResponseMessage resp = await cliente.LoginAsync("teste@tenant.local", "SenhaValida123!"))
        {
            await AssegurarProblemaAsync(resp, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        // 3. Logout
        using (HttpResponseMessage resp = await cliente.LogoutAsync())
        {
            await AssegurarProblemaAsync(resp, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        // 4. Me
        using (HttpResponseMessage resp = await cliente.MeAsync())
        {
            await AssegurarProblemaAsync(resp, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        // 5. Contexto
        using (HttpResponseMessage resp = await cliente.ContextoAsync())
        {
            await AssegurarProblemaAsync(resp, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }
    }
}
