namespace Platform.IntegrationTests.Http;

using System.Net;
using System.Text.Json;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Xunit;

/// <summary>
/// T093: limiter nativo do login com a política real (G11): cinco por minuto por IP efetivo,
/// janela fixa e fila zero; <c>RemoteIpAddress</c> nulo (padrão do <c>TestServer</c>) cai na partição
/// fixa; <c>X-Forwarded-For</c> forjado não contorna (forwarded desligado, G7). Separado do lockout.
/// </summary>
public sealed class TestesLimiteLogin(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    [Fact]
    public async Task SextoLoginNaJanela_Retorna429ComRetryAfter_SemRevelarValidadeDaEntrada()
    {
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi { LimiteLogin = null });
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.ObterTokenAsync();

        // Emails desconhecidos e distintos: nenhuma conta acumula falhas (sem lockout).
        for (int tentativa = 1; tentativa <= 5; tentativa++)
        {
            using HttpResponseMessage permitido = await cliente.LoginAsync(ContasSinteticas.NovoEmail("limite"), "Qualquer#2026x", novoToken: false);
            await AssegurarProblemaAsync(permitido, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        }

        using (HttpResponseMessage limitado = await cliente.LoginAsync(ContasSinteticas.NovoEmail("limite"), "Qualquer#2026x", novoToken: false))
        {
            await AssegurarProblemaAsync(limitado, HttpStatusCode.TooManyRequests, "RATE_LIMITED");
            string retry = Assert.Single(limitado.Headers.GetValues("Retry-After"));
            Assert.Matches("^[0-9]+$", retry);
            Assert.InRange(int.Parse(retry, System.Globalization.CultureInfo.InvariantCulture), 1, 60);
        }

        // Header ausente, corpo inválido ou CSRF ausente: o limiter responde antes e não revela nada.
        cliente.IdTenant = null;
        cliente.TokenCsrf = null;
        using (HttpResponseMessage semTenant = await cliente.EnviarAsync(HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, "{"))
        {
            await AssegurarProblemaAsync(semTenant, HttpStatusCode.TooManyRequests, "RATE_LIMITED");
        }

        // Outras rotas não são limitadas.
        cliente.IdTenant = api.IdTenant.ToString();
        Assert.Equal(HttpStatusCode.OK, await cliente.ObterTokenAsync());
    }

    [Fact]
    public async Task XForwardedForForjado_NaoContornaLimite_EIpNuloNaoEhIsento()
    {
        FabricaApiInstalacao api = CriarApi(A, new OpcoesFabricaApi { LimiteLogin = null });
        string corpo = JsonSerializer.Serialize(new { email = "ninguem@teste.local", password = "Qualquer#2026x" });
        List<HttpStatusCode> status = [];

        for (int tentativa = 1; tentativa <= 6; tentativa++)
        {
            ClienteAcessoHttp cliente = CriarCliente(api);
            using HttpResponseMessage resposta = await cliente.EnviarAsync(
                HttpMethod.Post,
                ClienteAcessoHttp.RotaSessoes,
                corpo,
                r => r.Headers.TryAddWithoutValidation("X-Forwarded-For", $"203.0.113.{tentativa}"));
            status.Add(resposta.StatusCode);
        }

        Assert.All(status.Take(5), s => Assert.NotEqual(HttpStatusCode.TooManyRequests, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, status[5]);
    }
}
