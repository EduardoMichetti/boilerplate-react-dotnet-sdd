namespace Platform.IntegrationTests.Http;

using System.Net;
using Platform.Domain.Auditoria;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Xunit;

/// <summary>
/// T150: registro de uso da sessão (D33, G31). Só requisições autenticadas aceitas (que chegam ao
/// handler de <c>/me</c>/<c>/context</c>) renovam a inatividade, no máximo 1 escrita/minuto; negadas,
/// login, logout, antiforgery, health e OpenAPI nunca gravam; nenhuma renovação emite <c>Set-Cookie</c>.
/// </summary>
public sealed class TestesRegistroUsoSessao(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    [Fact]
    public async Task NegadasAntesDoHandler_NaoRenovam_ExpiracaoConta30MinutosDoUltimoUsoAceito()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api, conceder: false);

        // 403 sem foundation.context.read.
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(2));
        using (HttpResponseMessage semPermissao = await cliente.ContextoAsync())
        {
            await AssegurarProblemaAsync(semPermissao, HttpStatusCode.Forbidden, "TENANT_ACCESS_DENIED");
        }

        // 409 com instalação inativa, em /me e /context.
        FabricaApiInstalacao inativa = CriarApi(A, new OpcoesFabricaApi { Estado = EstadoTenant.Inativo, DiretorioChaves = api.DiretorioChaves });
        ClienteAcessoHttp duranteInativacao = CriarCliente(inativa);
        CopiarCookies(cliente, duranteInativacao);
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(3));
        foreach (string rota in new[] { ClienteAcessoHttp.RotaMe, ClienteAcessoHttp.RotaContexto })
        {
            using HttpResponseMessage indisponivel = await duranteInativacao.EnviarAsync(HttpMethod.Get, rota);
            await AssegurarProblemaAsync(indisponivel, HttpStatusCode.Conflict, "TENANT_UNAVAILABLE");
        }

        // 400 TENANT_REQUIRED e 403 TENANT_ACCESS_DENIED (tenant divergente).
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(4));
        string tenant = cliente.IdTenant!;
        cliente.IdTenant = null;
        using (HttpResponseMessage semTenant = await cliente.MeAsync())
        {
            await AssegurarProblemaAsync(semTenant, HttpStatusCode.BadRequest, "TENANT_REQUIRED");
        }

        cliente.IdTenant = Guid.NewGuid().ToString();
        using (HttpResponseMessage outroTenant = await cliente.MeAsync())
        {
            await AssegurarProblemaAsync(outroTenant, HttpStatusCode.Forbidden, "TENANT_ACCESS_DENIED");
        }

        cliente.IdTenant = tenant;

        // 400 CSRF_INVALID no logout.
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(5));
        string token = cliente.TokenCsrf!;
        cliente.TokenCsrf = "token-invalido";
        using (HttpResponseMessage csrf = await cliente.LogoutAsync(novoToken: false))
        {
            await AssegurarProblemaAsync(csrf, HttpStatusCode.BadRequest, "CSRF_INVALID");
        }

        cliente.TokenCsrf = token;

        SessaoAutenticacao sessao = await api.SessaoCorrenteAsync(id);
        Assert.Equal(InstanteInicial, sessao.UltimoUsoEm);
        Assert.Equal(1, sessao.Versao);
        Assert.Null(sessao.AlteradoEm);

        Relogio.DefinirAgora(InstanteInicial.AddMinutes(30));
        using HttpResponseMessage expirada = await cliente.MeAsync();
        await AssegurarProblemaAsync(expirada, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    [Fact]
    public async Task LoginGravaUltimoUsoIgualACriacao_E_AntiforgeryHealthOpenApiELogoutNaoRegistramUso()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);
        SessaoAutenticacao emitida = await api.SessaoCorrenteAsync(id);
        Assert.Equal(emitida.CriadoEm, emitida.UltimoUsoEm);
        Assert.Equal(InstanteInicial, emitida.UltimoUsoEm);
        Assert.Equal(1, emitida.Versao);

        Relogio.DefinirAgora(InstanteInicial.AddMinutes(2));
        Assert.Equal(HttpStatusCode.OK, await cliente.ObterTokenAsync());
        foreach (string rota in new[] { "/health/live", "/health/ready", "/openapi/v1.json" })
        {
            using HttpResponseMessage resposta = await cliente.EnviarAsync(HttpMethod.Get, rota);
            Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        }

        SessaoAutenticacao intacta = await api.SessaoCorrenteAsync(id);
        Assert.Equal(InstanteInicial, intacta.UltimoUsoEm);
        Assert.Equal(1, intacta.Versao);

        using (HttpResponseMessage logout = await cliente.LogoutAsync(novoToken: false))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        SessaoAutenticacao revogada = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(InstanteInicial, revogada.UltimoUsoEm);
        Assert.Equal(InstanteInicial.AddMinutes(2), revogada.RevogadaEm);
        Assert.Equal(2, revogada.Versao);
    }

    [Fact]
    public async Task MeEContextoAceitos_GravamApos60Segundos_SemSetCookie_ComAuditoriaDoUsuario()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);

        Relogio.DefinirAgora(InstanteInicial.AddSeconds(60));
        using (HttpResponseMessage me = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            Assert.Empty(cliente.UltimoSetCookie);
        }

        SessaoAutenticacao aposMe = await api.SessaoCorrenteAsync(id);
        Assert.Equal(InstanteInicial.AddSeconds(60), aposMe.UltimoUsoEm);
        Assert.Equal(2, aposMe.Versao);
        Assert.Equal(InstanteInicial.AddSeconds(60), aposMe.AlteradoEm);
        Assert.Equal(TipoAutor.Usuario, aposMe.TipoAutorAlteracao);
        Assert.Equal(id.ToString(), aposMe.AutorAlteracao);
        Assert.NotNull(aposMe.IdOperacaoAlteracao);
        Assert.NotEqual(aposMe.IdOperacaoCriacao, aposMe.IdOperacaoAlteracao);
        Assert.Equal(InstanteInicial.AddMinutes(600), aposMe.TetoAbsolutoEm);

        Relogio.DefinirAgora(InstanteInicial.AddSeconds(120));
        using (HttpResponseMessage contexto = await cliente.ContextoAsync())
        {
            Assert.Equal(HttpStatusCode.OK, contexto.StatusCode);
            Assert.Empty(cliente.UltimoSetCookie);
        }

        SessaoAutenticacao aposContexto = await api.SessaoCorrenteAsync(id);
        Assert.Equal(InstanteInicial.AddSeconds(120), aposContexto.UltimoUsoEm);
        Assert.Equal(3, aposContexto.Versao);
        Assert.Equal(aposMe.TicketProtegido, aposContexto.TicketProtegido);
    }

    [Fact]
    public async Task UsoA30Segundos_NaoGrava_ExpiracaoContaDoRegistroAnterior()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);

        Relogio.DefinirAgora(InstanteInicial.AddSeconds(30));
        using (HttpResponseMessage me = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
            Assert.Empty(cliente.UltimoSetCookie);
        }

        SessaoAutenticacao sessao = await api.SessaoCorrenteAsync(id);
        Assert.Equal(InstanteInicial, sessao.UltimoUsoEm);
        Assert.Equal(1, sessao.Versao);

        // Tolerância a favor da segurança: negada 30 min após o último uso gravado, não após o uso a +30 s.
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(30));
        using HttpResponseMessage expirada = await cliente.MeAsync();
        await AssegurarProblemaAsync(expirada, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
    }

    [Fact]
    public async Task FalhaInjetadaNaGravacaoDoUso_Retorna503SemCorpoDeSucesso()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id) = await EntrarAsync(api);

        Relogio.DefinirAgora(InstanteInicial.AddMinutes(2));
        api.Falhas.FalharProximosRegistrosUso(1);
        foreach (string rota in new[] { ClienteAcessoHttp.RotaMe })
        {
            using HttpResponseMessage resposta = await cliente.EnviarAsync(HttpMethod.Get, rota);
            System.Text.Json.JsonElement problema = await AssegurarProblemaAsync(resposta, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
            Assert.False(problema.TryGetProperty("email", out _));
            Assert.False(problema.TryGetProperty("sessionExpiresAt", out _));
        }

        SessaoAutenticacao sessao = await api.SessaoCorrenteAsync(id);
        Assert.Equal(InstanteInicial, sessao.UltimoUsoEm);
        Assert.Equal(1, sessao.Versao);

        api.Falhas.FalharProximosRegistrosUso(1);
        using (HttpResponseMessage contexto = await cliente.ContextoAsync())
        {
            await AssegurarProblemaAsync(contexto, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE");
        }

        using HttpResponseMessage recuperada = await cliente.MeAsync();
        Assert.Equal(HttpStatusCode.OK, recuperada.StatusCode);
        Assert.Equal(InstanteInicial.AddMinutes(2), (await api.SessaoCorrenteAsync(id)).UltimoUsoEm);
    }

    private static void CopiarCookies(ClienteAcessoHttp origem, ClienteAcessoHttp destino)
    {
        foreach ((string nome, string valor) in origem.Cookies)
        {
            destino.Cookies[nome] = valor;
        }

        destino.TokenCsrf = origem.TokenCsrf;
    }

    private async Task<(ClienteAcessoHttp Cliente, Guid IdUsuario)> EntrarAsync(FabricaApiInstalacao api, bool conceder = true)
    {
        string email = ContasSinteticas.NovoEmail("uso");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA, conceder);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        return (cliente, id);
    }
}
