namespace Platform.IntegrationTests.Http;

using System.Net;
using System.Text.Json;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Xunit;

/// <summary>
/// T091/T149: sessão por inatividade + teto absoluto (D31, quickstart §7.1) fixados na emissão,
/// renovação só por requisição aceita, revogação confirmada antes do 204, independência entre
/// sessões e isolamento A/B.
/// </summary>
public sealed class TestesSessaoExpiracaoRevogacao(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    private static readonly TimeSpan Microssegundo = TimeSpan.FromTicks(10);

    [Fact]
    public async Task InatividadePadrao_UltimoUsoMais2959Autoriza_Mais3000Retorna401SemRevogar()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);

        DateTimeOffset usoAceito = InstanteInicial.AddMinutes(29).AddSeconds(59);
        Relogio.DefinirAgora(usoAceito);
        using (HttpResponseMessage antes = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        Assert.Equal(usoAceito, (await api.SessaoCorrenteAsync(id)).UltimoUsoEm);

        Relogio.DefinirAgora(usoAceito.AddMinutes(30));
        using (HttpResponseMessage depois = await cliente.MeAsync())
        {
            await AssegurarProblemaAsync(depois, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        SessaoAutenticacao sessao = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Null(sessao.RevogadaEm);
        Assert.NotNull(sessao.TicketProtegido);
        Assert.Equal(usoAceito, sessao.UltimoUsoEm);
    }

    [Fact]
    public async Task UsoACada29Minutos_ValidoAte59959_60000Retorna401MesmoEmUso()
    {
        (ClienteAcessoHttp cliente, _) = await EntrarAsync(CriarApi(A));

        for (int minuto = 29; minuto < 600; minuto += 29)
        {
            Relogio.DefinirAgora(InstanteInicial.AddMinutes(minuto));
            using HttpResponseMessage uso = await cliente.ContextoAsync();
            Assert.True(uso.StatusCode == HttpStatusCode.OK, $"Uso no minuto {minuto} deveria ser aceito.");
        }

        await AssegurarTetoAsync(cliente, 600);
    }

    [Fact]
    public async Task UsoACada1Minuto_ValidoAte59959_60000Retorna401MesmoEmUso()
    {
        (ClienteAcessoHttp cliente, _) = await EntrarAsync(CriarApi(A));

        for (int minuto = 1; minuto < 600; minuto++)
        {
            Relogio.DefinirAgora(InstanteInicial.AddMinutes(minuto));
            using HttpResponseMessage uso = await cliente.MeAsync();
            Assert.True(uso.StatusCode == HttpStatusCode.OK, $"Uso no minuto {minuto} deveria ser aceito.");
        }

        await AssegurarTetoAsync(cliente, 600);
    }

    [Fact]
    public async Task RevalidacaoSoPorMe_ACada29Minutos_MantemAteOTeto_E30MinutosSemChamadaRetorna401()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp revalidada, _) = await EntrarAsync(api);
        (ClienteAcessoHttp parada, _) = await EntrarAsync(api);

        for (int minuto = 29; minuto < 600; minuto += 29)
        {
            Relogio.DefinirAgora(InstanteInicial.AddMinutes(minuto));
            using HttpResponseMessage me = await revalidada.MeAsync();
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        using (HttpResponseMessage semChamada = await parada.MeAsync())
        {
            await AssegurarProblemaAsync(semChamada, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        await AssegurarTetoAsync(revalidada, 600);
    }

    [Theory]
    [InlineData("5", null, 5, 600)]
    [InlineData("240", null, 240, 600)]
    [InlineData(null, "30", 30, 30)]
    [InlineData(null, "720", 30, 720)]
    [InlineData("", "", 30, 600)]
    [InlineData(null, null, 30, 600)]
    public async Task FaixasAceitas_FixadasNaEmissao_ExpiramExatamente(string? inatividade, string? teto, int janelaEsperada, int tetoEsperado)
    {
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi { InatividadeMinutos = inatividade, TetoAbsolutoMinutos = teto });
        string email = ContasSinteticas.NovoEmail("faixa");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp expira = CriarCliente(api);
        ClienteAcessoHttp usa = CriarCliente(api);
        JsonElement login = await expira.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        await usa.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);

        Assert.Equal(InstanteInicial.AddMinutes(tetoEsperado), login.GetProperty("expiresAt").GetDateTimeOffset());
        Assert.Equal(janelaEsperada, login.GetProperty("idleTimeoutMinutes").GetInt32());
        foreach (SessaoAutenticacao sessao in await ContasSinteticas.SessoesAsync(A, id))
        {
            Assert.Equal(InstanteInicial.AddMinutes(tetoEsperado), sessao.TetoAbsolutoEm);
            Assert.Equal(janelaEsperada, sessao.JanelaInatividadeMinutos);
            Assert.Equal(sessao.CriadoEm, sessao.UltimoUsoEm);
        }

        // Inatividade (ou teto, se menor): sem uso, válida até limite − 1 µs e negada no limite.
        int limite = Math.Min(janelaEsperada, tetoEsperado);
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(limite) - Microssegundo);
        using (HttpResponseMessage valida = await usa.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, valida.StatusCode);
        }

        Relogio.DefinirAgora(InstanteInicial.AddMinutes(limite));
        using (HttpResponseMessage expirada = await expira.MeAsync())
        {
            await AssegurarProblemaAsync(expirada, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        }

        // Teto: mantida em uso a cada (janela − 1) minutos, válida até teto − 1 µs e negada no teto.
        DateTimeOffset ultimoUso = InstanteInicial.AddMinutes(limite) - Microssegundo;
        DateTimeOffset tetoAbsoluto = InstanteInicial.AddMinutes(tetoEsperado);
        while (ultimoUso.AddMinutes(janelaEsperada) < tetoAbsoluto)
        {
            ultimoUso = ultimoUso.AddMinutes(janelaEsperada - 1);
            Relogio.DefinirAgora(ultimoUso < tetoAbsoluto ? ultimoUso : tetoAbsoluto - Microssegundo);
            using HttpResponseMessage uso = await usa.MeAsync();
            Assert.Equal(HttpStatusCode.OK, uso.StatusCode);
        }

        await AssegurarTetoAsync(usa, tetoEsperado);
    }

    [Theory]
    [InlineData("4", null, null, "Sessao:InatividadeMinutos", "5–240")]
    [InlineData("241", null, null, "Sessao:InatividadeMinutos", "5–240")]
    [InlineData("30.5", null, null, "Sessao:InatividadeMinutos", "5–240")]
    [InlineData(" 30", null, null, "Sessao:InatividadeMinutos", "5–240")]
    [InlineData("-5", null, null, "Sessao:InatividadeMinutos", "5–240")]
    [InlineData("texto", null, null, "Sessao:InatividadeMinutos", "5–240")]
    [InlineData(null, "29", null, "Sessao:TetoAbsolutoMinutos", "30–720")]
    [InlineData(null, "721", null, "Sessao:TetoAbsolutoMinutos", "30–720")]
    [InlineData(null, "texto", null, "Sessao:TetoAbsolutoMinutos", "30–720")]
    [InlineData("60", "30", null, "Sessao:InatividadeMinutos maior que Sessao:TetoAbsolutoMinutos", "")]
    [InlineData(null, null, "30", "Sessao:PrazoAbsolutoMinutos", "Sessao:TetoAbsolutoMinutos")]
    public void FaixasRejeitadas_ImpedemInicializacaoComDiagnosticoSeguro(
        string? inatividade, string? teto, string? obsoleta, string trechoChave, string trechoFaixa)
    {
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi
        {
            InatividadeMinutos = inatividade,
            TetoAbsolutoMinutos = teto,
            PrazoAbsolutoMinutosObsoleto = obsoleta,
        });

        string diagnostico = api.ExigirFalhaInicializacao();
        Assert.Contains(trechoChave, diagnostico, StringComparison.Ordinal);
        Assert.Contains(trechoFaixa, diagnostico, StringComparison.Ordinal);
        Assert.DoesNotContain(A.SenhaExecucao, diagnostico, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InatividadeIgualAoTeto_Inicia()
    {
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi { InatividadeMinutos = "30", TetoAbsolutoMinutos = "30" });
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);

        using HttpResponseMessage me = await cliente.MeAsync();
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        SessaoAutenticacao sessao = await api.SessaoCorrenteAsync(id);
        Assert.Equal(30, sessao.JanelaInatividadeMinutos);
        Assert.Equal(InstanteInicial.AddMinutes(30), sessao.TetoAbsolutoEm);
    }

    [Fact]
    public async Task SessaoEmitidaCom30E600_ApiReiniciadaCom5E30_MantemJanelaETetoOriginais()
    {
        FabricaApiInstalacao original = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(original);

        FabricaApiInstalacao reiniciada = CriarApi(A, new OpcoesFabricaApi
        {
            InatividadeMinutos = "5",
            TetoAbsolutoMinutos = "30",
            DiretorioChaves = original.DiretorioChaves,
        });
        ClienteAcessoHttp aposReinicio = CriarCliente(reiniciada);
        CopiarCookies(cliente, aposReinicio);

        foreach (int minuto in new[] { 29, 58, 87 })
        {
            Relogio.DefinirAgora(InstanteInicial.AddMinutes(minuto));
            using HttpResponseMessage me = await aposReinicio.MeAsync();
            JsonElement corpo = await AssegurarSucessoAsync(me);
            Assert.Equal(InstanteInicial.AddMinutes(600), corpo.GetProperty("sessionExpiresAt").GetDateTimeOffset());
            Assert.Equal(30, corpo.GetProperty("sessionIdleTimeoutMinutes").GetInt32());
        }

        SessaoAutenticacao sessao = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(InstanteInicial.AddMinutes(600), sessao.TetoAbsolutoEm);
        Assert.Equal(30, sessao.JanelaInatividadeMinutos);
        Assert.Equal(InstanteInicial.AddMinutes(87), sessao.UltimoUsoEm);
    }

    [Fact]
    public async Task InstalacaoInativa_NaoPausa_ReativacaoApos30MinutosSemUsoAceitoRetorna401()
    {
        FabricaApiInstalacao ativa = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(ativa);

        FabricaApiInstalacao inativa = CriarApi(A, new OpcoesFabricaApi { Estado = EstadoTenant.Inativo, DiretorioChaves = ativa.DiretorioChaves });
        ClienteAcessoHttp duranteInativacao = CriarCliente(inativa);
        CopiarCookies(cliente, duranteInativacao);
        foreach (int minuto in new[] { 10, 20, 29 })
        {
            Relogio.DefinirAgora(InstanteInicial.AddMinutes(minuto));
            using HttpResponseMessage me = await duranteInativacao.MeAsync();
            await AssegurarProblemaAsync(me, HttpStatusCode.Conflict, "TENANT_UNAVAILABLE");
        }

        Assert.Equal(InstanteInicial, (await ativa.SessaoCorrenteAsync(id)).UltimoUsoEm);

        FabricaApiInstalacao reativada = CriarApi(A, new OpcoesFabricaApi { DiretorioChaves = ativa.DiretorioChaves });
        ClienteAcessoHttp aposReativar = CriarCliente(reativada);
        CopiarCookies(cliente, aposReativar);
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(30));
        using HttpResponseMessage expirada = await aposReativar.MeAsync();
        await AssegurarProblemaAsync(expirada, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    [Fact]
    public async Task FalhaInjetadaNaTrocaDoLogin_PreservaTetoJanelaEUltimoUsoDaAnterior()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("troca");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);

        Relogio.DefinirAgora(InstanteInicial.AddMinutes(5));
        using (HttpResponseMessage uso = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, uso.StatusCode);
        }

        SessaoAutenticacao antes = await api.SessaoCorrenteAsync(id);
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(10));
        api.Falhas.FalharProximosCommits(1);
        using (HttpResponseMessage login = await cliente.LoginAsync(email, ContasSinteticas.SenhaPadraoA))
        {
            await AssegurarProblemaAsync(login, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        SessaoAutenticacao depois = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(antes.IdSessao, depois.IdSessao);
        Assert.Equal(antes.TetoAbsolutoEm, depois.TetoAbsolutoEm);
        Assert.Equal(antes.JanelaInatividadeMinutos, depois.JanelaInatividadeMinutos);
        Assert.Equal(InstanteInicial.AddMinutes(5), depois.UltimoUsoEm);
        Assert.Equal(antes.Versao, depois.Versao);
        Assert.Null(depois.RevogadaEm);
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

    /// <summary>
    /// Teto: válida em teto − 1 µs (registrando uso) e negada no teto, mesmo com uso recente.
    /// </summary>
    private async Task AssegurarTetoAsync(ClienteAcessoHttp cliente, int tetoMinutos)
    {
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(tetoMinutos) - Microssegundo);
        using (HttpResponseMessage antes = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, antes.StatusCode);
        }

        Relogio.DefinirAgora(InstanteInicial.AddMinutes(tetoMinutos));
        using HttpResponseMessage noTeto = await cliente.MeAsync();
        await AssegurarProblemaAsync(noTeto, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
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
