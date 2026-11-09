namespace Platform.IntegrationTests.Http;

using System.Net;
using System.Text.Json;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Xunit;

/// <summary>
/// T091: prazo absoluto configurável fixado na emissão, sem extensão por uso, revogação
/// confirmada antes do 204, independência entre sessões e isolamento A/B.
/// </summary>
public sealed class TestesSessaoExpiracaoRevogacao(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    private static readonly TimeSpan Microssegundo = TimeSpan.FromTicks(10);

    [Fact]
    public async Task PrazoPadrao_2959Autoriza_3000Retorna401()
    {
        (ClienteAcessoHttp cliente, _) = await EntrarAsync(CriarApi(A));

        Relogio.Avancar(TimeSpan.FromMinutes(29) + TimeSpan.FromSeconds(59));
        using (HttpResponseMessage antes = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        Relogio.Avancar(TimeSpan.FromSeconds(1));
        using HttpResponseMessage depois = await cliente.MeAsync();
        await AssegurarProblemaAsync(depois, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("480", 480)]
    [InlineData("", 30)]
    public async Task PrazosConfigurados_ExpiramExatamente(string valor, int minutos)
    {
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi { PrazoAbsolutoMinutos = valor });
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);
        SessaoAutenticacao sessao = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(TesteHttpBase.InstanteInicial.AddMinutes(minutos), sessao.ExpiraEm);

        Relogio.Avancar(TimeSpan.FromMinutes(minutos) - Microssegundo);
        using (HttpResponseMessage antes = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        Relogio.Avancar(Microssegundo);
        using HttpResponseMessage depois = await cliente.MeAsync();
        await AssegurarProblemaAsync(depois, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    [Theory]
    [InlineData("4")]
    [InlineData("481")]
    [InlineData("0")]
    [InlineData("30.5")]
    [InlineData("texto")]
    [InlineData(" 30")]
    public void PrazoInvalido_ImpedeInicializacaoComDiagnosticoSeguro(string valor)
    {
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi { PrazoAbsolutoMinutos = valor });

        Exception erro = Assert.ThrowsAny<Exception>(() => api.CreateClient());
        string diagnostico = erro.ToString();
        Assert.Contains("Sessao:PrazoAbsolutoMinutos", diagnostico, StringComparison.Ordinal);
        Assert.Contains("5–480", diagnostico, StringComparison.Ordinal);
        Assert.DoesNotContain(A.SenhaExecucao, diagnostico, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SessaoEmitidaCom30_ApiReiniciadaCom5_MantemExpiraEmOriginal_EUsoNaoEstende()
    {
        FabricaApiInstalacao trinta = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(trinta);

        FabricaApiInstalacao cinco = CriarApi(A, new OpcoesFabricaApi { PrazoAbsolutoMinutos = "5", DiretorioChaves = trinta.DiretorioChaves });
        ClienteAcessoHttp reiniciado = CriarCliente(cinco);
        CopiarCookies(cliente, reiniciado);

        foreach (int minuto in new[] { 10, 20, 29 })
        {
            Relogio.DefinirAgora(TesteHttpBase.InstanteInicial.AddMinutes(minuto));
            using HttpResponseMessage me = await reiniciado.MeAsync();
            JsonElement corpo = await AssegurarSucessoAsync(me);
            Assert.Equal(TesteHttpBase.InstanteInicial.AddMinutes(30), corpo.GetProperty("sessionExpiresAt").GetDateTimeOffset());
        }

        Relogio.DefinirAgora(TesteHttpBase.InstanteInicial.AddMinutes(30));
        using (HttpResponseMessage expirada = await reiniciado.MeAsync())
        {
            await AssegurarProblemaAsync(expirada, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        SessaoAutenticacao sessao = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(TesteHttpBase.InstanteInicial.AddMinutes(30), sessao.ExpiraEm);
        Assert.Equal(1, sessao.Versao);
    }

    [Fact]
    public async Task Logout_204AposCommit_ReplayRetorna401()
    {
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(CriarApi(A));
        Dictionary<string, string> cookiesAntes = new(cliente.Cookies);
        string token = cliente.TokenCsrf!;

        using (HttpResponseMessage logout = await cliente.LogoutAsync(novoToken: false))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            Assert.Contains(cliente.UltimoSetCookie, c => c.StartsWith("bps-session=;", StringComparison.Ordinal));
        }

        SessaoAutenticacao sessao = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.NotNull(sessao.RevogadaEm);
        Assert.Null(sessao.TicketProtegido);

        foreach ((string nome, string valor) in cookiesAntes)
        {
            cliente.Cookies[nome] = valor;
        }

        cliente.TokenCsrf = token;
        using (HttpResponseMessage replay = await cliente.EnviarAsync(HttpMethod.Delete, ClienteAcessoHttp.RotaSessaoAtual))
        {
            await AssegurarProblemaAsync(replay, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        using HttpResponseMessage me = await cliente.MeAsync();
        await AssegurarProblemaAsync(me, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    [Fact]
    public async Task FalhaNoCommitDoLogout_Retorna503SemApagarCookie()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);

        api.Falhas.FalharProximasGravacoesSessao(1);
        using (HttpResponseMessage logout = await cliente.LogoutAsync(novoToken: false))
        {
            await AssegurarProblemaAsync(logout, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
            Assert.DoesNotContain(cliente.UltimoSetCookie, c => c.StartsWith("bps-session=", StringComparison.Ordinal));
        }

        Assert.True(cliente.Cookies.ContainsKey("bps-session"));
        SessaoAutenticacao sessao = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Null(sessao.RevogadaEm);
        using HttpResponseMessage me = await cliente.MeAsync();
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }

    [Fact]
    public async Task DuasSessoesDaMesmaConta_SaoIndependentesNoLoginENoLogout()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("duas");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp um = CriarCliente(api);
        ClienteAcessoHttp dois = CriarCliente(api);
        await um.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        await dois.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);

        // Novo login no jar "um" troca só a sessão desse cookie.
        await um.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        using (HttpResponseMessage meDois = await dois.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, meDois.StatusCode);
        }

        using (HttpResponseMessage logoutUm = await um.LogoutAsync(novoToken: false))
        {
            Assert.Equal(HttpStatusCode.NoContent, logoutUm.StatusCode);
        }

        using (HttpResponseMessage meDoisDepois = await dois.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, meDoisDepois.StatusCode);
        }

        List<SessaoAutenticacao> sessoes = await ContasSinteticas.SessoesAsync(A, id);
        Assert.Equal(3, sessoes.Count);
        Assert.Single(sessoes, s => s.RevogadaEm is null);
    }

    [Fact]
    public async Task CookieDeAReenviadoAB_Retorna401SemDados()
    {
        string email = ContasSinteticas.NovoEmail("cruzado");
        await ContasSinteticas.CriarContaAsync(B, Relogio, email, ContasSinteticas.SenhaPadraoB);
        (ClienteAcessoHttp clienteA, _) = await EntrarAsync(CriarApi(A), email);
        ClienteAcessoHttp clienteB = CriarCliente(CriarApi(B));
        clienteA.CopiarCookieSessaoPara(clienteB);

        foreach (string rota in new[] { ClienteAcessoHttp.RotaMe, ClienteAcessoHttp.RotaContexto })
        {
            using HttpResponseMessage resposta = await clienteB.EnviarAsync(HttpMethod.Get, rota);
            JsonElement problema = await AssegurarProblemaAsync(resposta, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
            Assert.DoesNotContain(email, problema.GetRawText(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task EpocaAlterada_Retorna401()
    {
        (ClienteAcessoHttp cliente, _) = await EntrarAsync(CriarApi(B));
        await ContasSinteticas.RenovarEpocaAsync(B);

        using HttpResponseMessage me = await cliente.MeAsync();
        await AssegurarProblemaAsync(me, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    [Fact]
    public async Task Reativacao_RetomaSoSessaoDentroDoPrazoOriginal()
    {
        FabricaApiInstalacao ativa = CriarApi(A);
        (ClienteAcessoHttp cliente, _) = await EntrarAsync(ativa);

        FabricaApiInstalacao inativa = CriarApi(A, new OpcoesFabricaApi { Estado = EstadoTenant.Inativo, DiretorioChaves = ativa.DiretorioChaves });
        ClienteAcessoHttp duranteInativacao = CriarCliente(inativa);
        CopiarCookies(cliente, duranteInativacao);
        Relogio.DefinirAgora(TesteHttpBase.InstanteInicial.AddMinutes(10));
        using (HttpResponseMessage me = await duranteInativacao.MeAsync())
        {
            await AssegurarProblemaAsync(me, HttpStatusCode.Conflict, "TENANT_UNAVAILABLE");
        }

        FabricaApiInstalacao reativada = CriarApi(A, new OpcoesFabricaApi { DiretorioChaves = ativa.DiretorioChaves });
        ClienteAcessoHttp aposReativar = CriarCliente(reativada);
        CopiarCookies(cliente, aposReativar);
        Relogio.DefinirAgora(TesteHttpBase.InstanteInicial.AddMinutes(20));
        using (HttpResponseMessage me = await aposReativar.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        Relogio.DefinirAgora(TesteHttpBase.InstanteInicial.AddMinutes(31));
        using HttpResponseMessage expirada = await aposReativar.MeAsync();
        await AssegurarProblemaAsync(expirada, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    [Fact]
    public async Task Restart_ComMesmoKeyRingMantemSessao_ComOutroKeyRingNaoAutentica()
    {
        FabricaApiInstalacao original = CriarApi(A);
        (ClienteAcessoHttp cliente, _) = await EntrarAsync(original);

        ClienteAcessoHttp mesmoKeyRing = CriarCliente(CriarApi(A, new OpcoesFabricaApi { DiretorioChaves = original.DiretorioChaves }));
        CopiarCookies(cliente, mesmoKeyRing);
        using (HttpResponseMessage me = await mesmoKeyRing.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        ClienteAcessoHttp outroKeyRing = CriarCliente(CriarApi(A));
        CopiarCookies(cliente, outroKeyRing);
        using HttpResponseMessage negado = await outroKeyRing.MeAsync();
        await AssegurarProblemaAsync(negado, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    private static void CopiarCookies(ClienteAcessoHttp origem, ClienteAcessoHttp destino)
    {
        foreach ((string nome, string valor) in origem.Cookies)
        {
            destino.Cookies[nome] = valor;
        }

        destino.TokenCsrf = origem.TokenCsrf;
    }

    private async Task<(ClienteAcessoHttp Cliente, Guid IdUsuario)> EntrarAsync(FabricaApiInstalacao api, string? emailExistenteEmOutra = null)
    {
        string email = emailExistenteEmOutra ?? ContasSinteticas.NovoEmail("sessao");
        InstalacaoTestePostgres instalacao = api.IdTenant == A.IdTenant ? A : B;
        string senha = instalacao == A ? ContasSinteticas.SenhaPadraoA : ContasSinteticas.SenhaPadraoB;
        Guid id = await ContasSinteticas.CriarContaAsync(instalacao, Relogio, email, senha);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, senha);
        return (cliente, id);
    }
}
