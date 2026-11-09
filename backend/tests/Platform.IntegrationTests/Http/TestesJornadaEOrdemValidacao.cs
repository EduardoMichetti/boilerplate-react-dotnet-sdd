namespace Platform.IntegrationTests.Http;

using System.Net;
using System.Text.Json;
using Platform.Domain.Tenancy;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Xunit;

/// <summary>
/// T090: jornada HTTP L2 em A e B (mesmo email, senhas distintas, jars separados) e ordem de
/// validação G2 revisada/G9/G15, sem host operacional.
/// </summary>
public sealed class TestesJornadaEOrdemValidacao(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    private const string CorpoInvalido = """{"email":"nao-e-email","password":""}""";

    public static TheoryData<string, string> RotasL2 => new()
    {
        { "GET", ClienteAcessoHttp.RotaAntiforgery },
        { "POST", ClienteAcessoHttp.RotaSessoes },
        { "DELETE", ClienteAcessoHttp.RotaSessaoAtual },
        { "GET", ClienteAcessoHttp.RotaMe },
        { "GET", ClienteAcessoHttp.RotaContexto },
    };

    [Fact]
    public async Task JornadaCompleta_EmAEB_ComMesmoEmailESenhasDistintas()
    {
        string email = ContasSinteticas.NovoEmail("jornada");
        Guid idA = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA, nome: "Pessoa em A");
        Guid idB = await ContasSinteticas.CriarContaAsync(B, Relogio, email, ContasSinteticas.SenhaPadraoB, nome: "Pessoa em B");

        foreach ((FabricaApiInstalacao api, Guid id, string senha, string senhaOutra, string nome) in new[]
        {
            (CriarApi(A), idA, ContasSinteticas.SenhaPadraoA, ContasSinteticas.SenhaPadraoB, "Pessoa em A"),
            (CriarApi(B), idB, ContasSinteticas.SenhaPadraoB, ContasSinteticas.SenhaPadraoA, "Pessoa em B"),
        })
        {
            api.AssegurarRelogioControlavel();
            ClienteAcessoHttp cliente = CriarCliente(api);

            using (HttpResponseMessage senhaDeOutraInstalacao = await cliente.LoginAsync(email, senhaOutra))
            {
                await AssegurarProblemaAsync(senhaDeOutraInstalacao, HttpStatusCode.Unauthorized, "AUTH_INVALID");
            }

            Assert.Equal(HttpStatusCode.OK, await cliente.ObterTokenAsync());
            using (HttpResponseMessage login = await cliente.LoginAsync(email, senha, novoToken: false))
            {
                JsonElement corpo = await AssegurarSucessoAsync(login, HttpStatusCode.Created);
                Assert.Equal(api.IdTenant, corpo.GetProperty("tenantId").GetGuid());
                Assert.Equal(TesteHttpBase.InstanteInicial.AddMinutes(30), corpo.GetProperty("expiresAt").GetDateTimeOffset());
                Assert.Equal("/api/v1/sessions/current", login.Headers.Location?.OriginalString);
                Assert.True(cliente.Cookies.ContainsKey("bps-session"));
            }

            // Novo GET antiforgery após o login (nova identidade).
            Assert.Equal(HttpStatusCode.OK, await cliente.ObterTokenAsync());

            using (HttpResponseMessage me = await cliente.MeAsync())
            {
                JsonElement corpo = await AssegurarSucessoAsync(me);
                Assert.Equal(id, corpo.GetProperty("id").GetGuid());
                Assert.Equal(api.IdTenant, corpo.GetProperty("tenantId").GetGuid());
                Assert.Equal(email, corpo.GetProperty("email").GetString());
                Assert.Equal(nome, corpo.GetProperty("displayName").GetString());
                Assert.Equal(TesteHttpBase.InstanteInicial.AddMinutes(30), corpo.GetProperty("sessionExpiresAt").GetDateTimeOffset());
            }

            using (HttpResponseMessage contexto = await cliente.ContextoAsync())
            {
                JsonElement corpo = await AssegurarSucessoAsync(contexto);
                Assert.Equal(id, corpo.GetProperty("user").GetProperty("id").GetGuid());
                Assert.Equal(api.IdTenant, corpo.GetProperty("tenant").GetProperty("id").GetGuid());
                Assert.Equal(["foundation.context.read"], corpo.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()!).ToArray());
                Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("applicationVersion").GetString()));
            }

            using (HttpResponseMessage logout = await cliente.LogoutAsync(novoToken: false))
            {
                Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
                Assert.True(logout.Headers.CacheControl?.NoStore);
                Assert.False(cliente.Cookies.ContainsKey("bps-session"));
            }
        }
    }

    [Theory]
    [MemberData(nameof(RotasL2))]
    public async Task HeaderTenantAusenteOuVazio_Retorna400TenantRequired(string metodo, string rota)
    {
        ClienteAcessoHttp cliente = CriarCliente(CriarApi(A));
        foreach (string? valor in new[] { null, string.Empty })
        {
            cliente.IdTenant = valor;
            using HttpResponseMessage resposta = await cliente.EnviarAsync(new HttpMethod(metodo), rota, metodo == "POST" ? CorpoInvalido : null);
            await AssegurarProblemaAsync(resposta, HttpStatusCode.BadRequest, "TENANT_REQUIRED");
        }
    }

    [Theory]
    [InlineData("nao-e-uuid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData(" ")]
    [InlineData("{0},{0}")]
    [InlineData("DUPLICADO")]
    public async Task HeaderTenantInvalidoDuplicadoOuLista_Retorna400InputInvalid(string valor)
    {
        FabricaApiInstalacao api = CriarApi(A);
        ClienteAcessoHttp cliente = CriarCliente(api);
        string tenant = api.IdTenant.ToString();
        cliente.IdTenant = null;

        using HttpResponseMessage resposta = await cliente.EnviarAsync(HttpMethod.Get, ClienteAcessoHttp.RotaMe, ajustar: r =>
        {
            if (valor == "DUPLICADO")
            {
                r.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
                r.Headers.TryAddWithoutValidation("X-Tenant-Id", tenant);
            }
            else
            {
                r.Headers.TryAddWithoutValidation("X-Tenant-Id", string.Format(System.Globalization.CultureInfo.InvariantCulture, valor, tenant));
            }
        });

        JsonElement problema = await AssegurarProblemaAsync(resposta, HttpStatusCode.BadRequest, "INPUT_INVALID");
        Assert.True(problema.GetProperty("errors").TryGetProperty("X-Tenant-Id", out _));
    }

    [Fact]
    public async Task TenantDivergente_Login401SemLookupDeConta_Demais403()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("divergente");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);
        cliente.IdTenant = B.IdTenant.ToString();

        api.ComandosSql.Clear();
        using (HttpResponseMessage login = await cliente.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, JsonSerializer.Serialize(new { email, password = ContasSinteticas.SenhaPadraoA })))
        {
            await AssegurarProblemaAsync(login, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        }

        Assert.Empty(api.ComandosSql);

        foreach ((string metodo, string rota) in new[] { ("GET", ClienteAcessoHttp.RotaAntiforgery), ("DELETE", ClienteAcessoHttp.RotaSessaoAtual), ("GET", ClienteAcessoHttp.RotaMe), ("GET", ClienteAcessoHttp.RotaContexto) })
        {
            using HttpResponseMessage resposta = await cliente.EnviarAsync(new HttpMethod(metodo), rota);
            await AssegurarProblemaAsync(resposta, HttpStatusCode.Forbidden, "TENANT_ACCESS_DENIED");
        }

        Assert.Empty(api.ComandosSql);
    }

    [Fact]
    public async Task MarcadorDivergente_Retorna503AntesDeCorpoCsrfECookie_SemConsultarContasOuSessoes()
    {
        string email = ContasSinteticas.NovoEmail("marcador");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp clienteValido = CriarCliente(CriarApi(A));
        await clienteValido.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);

        // Configuração aponta para outro tenant, mas a conexão continua no banco de A: marcador diverge.
        FabricaApiInstalacao divergente = CriarApi(A, new OpcoesFabricaApi { IdTenantConfigurado = Guid.NewGuid() });
        ClienteAcessoHttp cliente = CriarCliente(divergente);
        divergente.ComandosSql.Clear();

        using (HttpResponseMessage corpoInvalido = await cliente.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, CorpoInvalido))
        {
            await AssegurarProblemaAsync(corpoInvalido, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        cliente.TokenCsrf = "token-invalido";
        using (HttpResponseMessage csrfInvalido = await cliente.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, JsonSerializer.Serialize(new { email, password = "x" })))
        {
            await AssegurarProblemaAsync(csrfInvalido, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        clienteValido.CopiarCookieSessaoPara(cliente);
        foreach (string rota in new[] { ClienteAcessoHttp.RotaMe, ClienteAcessoHttp.RotaContexto })
        {
            using HttpResponseMessage comCookieValido = await cliente.EnviarAsync(HttpMethod.Get, rota);
            await AssegurarProblemaAsync(comCookieValido, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        cliente.Cookies["bps-session"] = "cookie-invalido";
        using (HttpResponseMessage comCookieInvalido = await cliente.EnviarAsync(HttpMethod.Delete, ClienteAcessoHttp.RotaSessaoAtual))
        {
            await AssegurarProblemaAsync(comCookieInvalido, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        Assert.NotEmpty(divergente.ComandosSql);
        Assert.DoesNotContain(divergente.ComandosSql, sql => sql.Contains("sessoes_autenticacao", StringComparison.Ordinal));
        Assert.DoesNotContain(divergente.ComandosSql, sql => sql.Contains("access.usuarios", StringComparison.Ordinal) || sql.Contains("\"usuarios\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CorpoAntesDeCsrf_ECsrfAusenteInvalidoOuDeIdentidadeAnterior()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("csrf");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);
        string corpoValido = JsonSerializer.Serialize(new { email, password = ContasSinteticas.SenhaPadraoA });

        cliente.TokenCsrf = "token-invalido";
        using (HttpResponseMessage ambosInvalidos = await cliente.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, CorpoInvalido))
        {
            JsonElement problema = await AssegurarProblemaAsync(ambosInvalidos, HttpStatusCode.BadRequest, "INPUT_INVALID");
            Assert.True(problema.GetProperty("errors").TryGetProperty("email", out _));
        }

        using (HttpResponseMessage camposExtras = await cliente.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, """{"email":"a@b.c","password":"x","tenantId":"1"}"""))
        {
            await AssegurarProblemaAsync(camposExtras, HttpStatusCode.BadRequest, "INPUT_INVALID");
        }

        cliente.TokenCsrf = null;
        using (HttpResponseMessage semCsrf = await cliente.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, corpoValido))
        {
            await AssegurarProblemaAsync(semCsrf, HttpStatusCode.BadRequest, "CSRF_INVALID");
        }

        cliente.TokenCsrf = "token-invalido";
        using (HttpResponseMessage csrfInvalido = await cliente.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, corpoValido))
        {
            await AssegurarProblemaAsync(csrfInvalido, HttpStatusCode.BadRequest, "CSRF_INVALID");
        }

        // Token anônimo reutilizado depois do login: identidade anterior.
        await cliente.ObterTokenAsync();
        string tokenAnonimo = cliente.TokenCsrf!;
        using (HttpResponseMessage login = await cliente.LoginAsync(email, ContasSinteticas.SenhaPadraoA, novoToken: false))
        {
            Assert.Equal(HttpStatusCode.Created, login.StatusCode);
        }

        cliente.TokenCsrf = tokenAnonimo;
        using (HttpResponseMessage logoutTokenAnterior = await cliente.EnviarAsync(HttpMethod.Delete, ClienteAcessoHttp.RotaSessaoAtual))
        {
            await AssegurarProblemaAsync(logoutTokenAnterior, HttpStatusCode.BadRequest, "CSRF_INVALID");
        }

        using (HttpResponseMessage loginTokenAnterior = await cliente.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, corpoValido))
        {
            await AssegurarProblemaAsync(loginTokenAnterior, HttpStatusCode.BadRequest, "CSRF_INVALID");
        }

        using (HttpResponseMessage me = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }
    }

    [Fact]
    public async Task LogoutSemSessaoValida_Retorna401AntesDeCsrf()
    {
        ClienteAcessoHttp cliente = CriarCliente(CriarApi(A));
        cliente.TokenCsrf = "token-invalido";

        using (HttpResponseMessage semCookie = await cliente.EnviarAsync(HttpMethod.Delete, ClienteAcessoHttp.RotaSessaoAtual))
        {
            await AssegurarProblemaAsync(semCookie, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        cliente.Cookies["bps-session"] = "cookie-invalido";
        using (HttpResponseMessage cookieInvalido = await cliente.EnviarAsync(HttpMethod.Delete, ClienteAcessoHttp.RotaSessaoAtual, CorpoInvalido))
        {
            await AssegurarProblemaAsync(cookieInvalido, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        foreach (string rota in new[] { ClienteAcessoHttp.RotaMe, ClienteAcessoHttp.RotaContexto })
        {
            using HttpResponseMessage resposta = await cliente.EnviarAsync(HttpMethod.Get, rota);
            await AssegurarProblemaAsync(resposta, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }
    }

    [Fact]
    public async Task CredenciaisInvalidas_Retornam401GenericoSemSessao()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string emailDesativada = ContasSinteticas.NovoEmail("desativada");
        string emailBloqueada = ContasSinteticas.NovoEmail("bloqueada");
        string emailAtiva = ContasSinteticas.NovoEmail("ativa");
        Guid idDesativada = await ContasSinteticas.CriarContaAsync(A, Relogio, emailDesativada, ContasSinteticas.SenhaPadraoA);
        Guid idBloqueada = await ContasSinteticas.CriarContaAsync(A, Relogio, emailBloqueada, ContasSinteticas.SenhaPadraoA);
        Guid idAtiva = await ContasSinteticas.CriarContaAsync(A, Relogio, emailAtiva, ContasSinteticas.SenhaPadraoA);
        await ContasSinteticas.AlterarContaAsync(A, Relogio, idDesativada, u => u.EstaAtivo = false);
        await ContasSinteticas.AlterarContaAsync(A, Relogio, idBloqueada, u => u.LockoutEnd = TesteHttpBase.InstanteInicial.AddMinutes(10));

        foreach ((string email, string senha) in new[]
        {
            (ContasSinteticas.NovoEmail("desconhecido"), ContasSinteticas.SenhaPadraoA),
            (emailAtiva, "SenhaErrada#2026x"),
            (emailAtiva, "curta"),
            (emailDesativada, ContasSinteticas.SenhaPadraoA),
            (emailBloqueada, ContasSinteticas.SenhaPadraoA),
        })
        {
            ClienteAcessoHttp cliente = CriarCliente(api);
            using HttpResponseMessage resposta = await cliente.LoginAsync(email, senha);
            JsonElement problema = await AssegurarProblemaAsync(resposta, HttpStatusCode.Unauthorized, "AUTH_INVALID");
            Assert.False(problema.TryGetProperty("errors", out _));
            Assert.False(cliente.Cookies.ContainsKey("bps-session"));
        }

        foreach (Guid id in new[] { idDesativada, idBloqueada, idAtiva })
        {
            Assert.Empty(await ContasSinteticas.SessoesAsync(A, id));
        }
    }

    [Fact]
    public async Task SemPermissao_Contexto403EMe200_RevogacaoValeNaProximaOperacao()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string semPermissao = ContasSinteticas.NovoEmail("semperm");
        string comPermissao = ContasSinteticas.NovoEmail("comperm");
        await ContasSinteticas.CriarContaAsync(A, Relogio, semPermissao, ContasSinteticas.SenhaPadraoA, conceder: false);
        Guid idCom = await ContasSinteticas.CriarContaAsync(A, Relogio, comPermissao, ContasSinteticas.SenhaPadraoA);

        ClienteAcessoHttp clienteSem = CriarCliente(api);
        await clienteSem.EntrarAsync(semPermissao, ContasSinteticas.SenhaPadraoA);
        using (HttpResponseMessage contexto = await clienteSem.ContextoAsync())
        {
            await AssegurarProblemaAsync(contexto, HttpStatusCode.Forbidden, "TENANT_ACCESS_DENIED");
        }

        using (HttpResponseMessage me = await clienteSem.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        ClienteAcessoHttp clienteCom = CriarCliente(api);
        await clienteCom.EntrarAsync(comPermissao, ContasSinteticas.SenhaPadraoA);
        using (HttpResponseMessage antes = await clienteCom.ContextoAsync())
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        await ContasSinteticas.RevogarPermissaoAsync(A, Relogio, idCom);
        using (HttpResponseMessage depois = await clienteCom.ContextoAsync())
        {
            await AssegurarProblemaAsync(depois, HttpStatusCode.Forbidden, "TENANT_ACCESS_DENIED");
        }
    }

    [Fact]
    public async Task InstalacaoInativa_Login401_MeEContexto409_AntiforgeryELogoutPermitidos()
    {
        string email = ContasSinteticas.NovoEmail("inativa");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        FabricaApiInstalacao ativa = CriarApi(A);
        ClienteAcessoHttp cliente = CriarCliente(ativa);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);

        // "Restart" com a instalação inativa, mesmo key ring.
        FabricaApiInstalacao inativa = CriarApi(A, new OpcoesFabricaApi { Estado = EstadoTenant.Inativo, DiretorioChaves = ativa.DiretorioChaves });
        ClienteAcessoHttp clienteInativa = CriarCliente(inativa);
        foreach ((string nome, string valor) in cliente.Cookies)
        {
            clienteInativa.Cookies[nome] = valor;
        }

        foreach (string rota in new[] { ClienteAcessoHttp.RotaMe, ClienteAcessoHttp.RotaContexto })
        {
            using HttpResponseMessage resposta = await clienteInativa.EnviarAsync(HttpMethod.Get, rota);
            await AssegurarProblemaAsync(resposta, HttpStatusCode.Conflict, "TENANT_UNAVAILABLE");
        }

        ClienteAcessoHttp anonimo = CriarCliente(inativa);
        using (HttpResponseMessage login = await anonimo.LoginAsync(email, ContasSinteticas.SenhaPadraoA))
        {
            await AssegurarProblemaAsync(login, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        }

        Assert.Equal(HttpStatusCode.OK, await clienteInativa.ObterTokenAsync());
        using (HttpResponseMessage logout = await clienteInativa.LogoutAsync(novoToken: false))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }
    }

    [Fact]
    public async Task Health_ComCookiesValidosOuInvalidos_NaoConsultaSessao_EMeTenants404()
    {
        string email = ContasSinteticas.NovoEmail("health");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        FabricaApiInstalacao api = CriarApi(A);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);

        foreach (string valorCookie in new[] { cliente.Cookies["bps-session"], "cookie-invalido" })
        {
            cliente.Cookies["bps-session"] = valorCookie;
            api.ComandosSql.Clear();
            using (HttpResponseMessage live = await cliente.EnviarAsync(HttpMethod.Get, "/health/live"))
            {
                Assert.Equal(HttpStatusCode.OK, live.StatusCode);
            }

            using (HttpResponseMessage ready = await cliente.EnviarAsync(HttpMethod.Get, "/health/ready"))
            {
                Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
            }

            Assert.DoesNotContain(api.ComandosSql, sql => sql.Contains("sessoes_autenticacao", StringComparison.Ordinal));
            Assert.DoesNotContain(api.ComandosSql, sql => sql.Contains("\"usuarios\"", StringComparison.Ordinal));
        }

        using HttpResponseMessage tenants = await cliente.EnviarAsync(HttpMethod.Get, "/api/v1/me/tenants");
        await AssegurarProblemaAsync(tenants, HttpStatusCode.NotFound, "NOT_FOUND");
    }
}
