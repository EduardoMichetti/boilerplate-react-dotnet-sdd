namespace Platform.IntegrationTests.Http;

using System.Net;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T130: atributos dos cookies de sessão e antiforgery em Development e em Production (HTTPS
/// simulado e certificado gerado na execução) e inicialização negada sem certificado utilizável.
/// </summary>
public sealed class TestesCookiesEProtecaoChaves(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    [Fact]
    public async Task Development_CookieDeSessaoHttpOnlyLaxSemDomainComExpiresIgualAoGravado()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("cookiedev");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);

        await cliente.ObterTokenAsync();
        string csrf = Assert.Single(cliente.UltimoSetCookie, c => c.StartsWith("bps-csrf=", StringComparison.Ordinal));
        AssegurarAtributos(csrf, secure: false);

        using HttpResponseMessage login = await cliente.LoginAsync(email, ContasSinteticas.SenhaPadraoA, novoToken: false);
        Assert.Equal(HttpStatusCode.Created, login.StatusCode);
        string sessao = Assert.Single(cliente.UltimoSetCookie, c => c.StartsWith("bps-session=", StringComparison.Ordinal));
        AssegurarAtributos(sessao, secure: false);
        Assert.Contains("samesite=lax", sessao, StringComparison.OrdinalIgnoreCase);

        SessaoAutenticacao gravada = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(gravada.ExpiraEm, ExtrairExpires(sessao));
    }

    [Fact]
    public async Task Production_CookiesComPrefixoHostESecure()
    {
        using CertificadoTeste certificado = new();
        FabricaApiInstalacao api = CriarApi(A, OpcoesProducao(certificado));
        string email = ContasSinteticas.NovoEmail("cookieprod");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api, https: true);

        Assert.Equal(HttpStatusCode.OK, await cliente.ObterTokenAsync());
        string csrf = Assert.Single(cliente.UltimoSetCookie, c => c.StartsWith("__Host-bps-csrf=", StringComparison.Ordinal));
        AssegurarAtributos(csrf, secure: true);

        using (HttpResponseMessage login = await cliente.LoginAsync(email, ContasSinteticas.SenhaPadraoA, novoToken: false))
        {
            Assert.Equal(HttpStatusCode.Created, login.StatusCode);
        }

        string sessao = Assert.Single(cliente.UltimoSetCookie, c => c.StartsWith("__Host-bps-session=", StringComparison.Ordinal));
        AssegurarAtributos(sessao, secure: true);
        Assert.DoesNotContain(cliente.UltimoSetCookie, c => c.StartsWith("bps-session=", StringComparison.Ordinal));

        await cliente.ObterTokenAsync();
        using HttpResponseMessage me = await cliente.MeAsync();
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        Assert.True(Directory.EnumerateFiles(api.DiretorioChaves, "*.xml").Any());
    }

    [Fact]
    public void Production_SemCertificado_NaoInicializa()
    {
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi { Ambiente = "Production" });
        string diagnostico = api.ExigirFalhaInicializacao();
        Assert.Contains("ProtecaoDados:ArquivoCertificado", diagnostico, StringComparison.Ordinal);
        Assert.DoesNotContain(A.SenhaExecucao, diagnostico, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_CertificadoIlegivel_NaoInicializa()
    {
        using CertificadoTeste certificado = new();
        File.WriteAllText(certificado.ArquivoSenha, "senha-incorreta");
        FabricaApiInstalacao api = CriarApi(A, OpcoesProducao(certificado));
        string diagnostico = api.ExigirFalhaInicializacao();
        Assert.Contains("ProtecaoDados:ArquivoCertificado", diagnostico, StringComparison.Ordinal);
        Assert.DoesNotContain(certificado.Senha, diagnostico, StringComparison.Ordinal);
        Assert.DoesNotContain("senha-incorreta", diagnostico, StringComparison.Ordinal);
    }

    private static OpcoesFabricaApi OpcoesProducao(CertificadoTeste certificado) => new()
    {
        Ambiente = "Production",
        Extras = new Dictionary<string, string?>
        {
            ["ProtecaoDados:ArquivoCertificado"] = certificado.ArquivoCertificado,
            ["ProtecaoDados:ArquivoSenhaCertificado"] = certificado.ArquivoSenha,
        },
    };

    private static void AssegurarAtributos(string setCookie, bool secure)
    {
        string[] atributos = setCookie.Split(';', StringSplitOptions.TrimEntries).Skip(1).Select(a => a.ToLowerInvariant()).ToArray();
        Assert.Contains("httponly", atributos);
        Assert.Contains("path=/", atributos);
        Assert.DoesNotContain(atributos, a => a.StartsWith("domain=", StringComparison.Ordinal));
        Assert.Equal(secure, atributos.Contains("secure"));
    }

    private static DateTimeOffset ExtrairExpires(string setCookie)
    {
        string atributo = setCookie.Split(';', StringSplitOptions.TrimEntries)
            .Single(a => a.StartsWith("expires=", StringComparison.OrdinalIgnoreCase));
        return DateTimeOffset.Parse(atributo[8..], System.Globalization.CultureInfo.InvariantCulture);
    }
}
