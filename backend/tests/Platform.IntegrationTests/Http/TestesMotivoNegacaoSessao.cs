namespace Platform.IntegrationTests.Http;

using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Xunit;

/// <summary>
/// T152 (D37): sessões negadas por inatividade, teto, revogação, cookie ilegível e ausência de cookie
/// recebem a mesma resposta 401 <c>AUTH_REQUIRED</c> (exceto <c>traceId</c>); o motivo aparece só no
/// log estruturado <c>EventId(1002, "SessaoNegada")</c>, sem dados de sessão, conta, cookie ou IP.
/// </summary>
public sealed class TestesMotivoNegacaoSessao(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    private const int EventoSessaoNegada = 1002;

    [Fact]
    public async Task CincoMotivos_RespostasIdenticasExcetoTraceId_MotivoSoNoLog()
    {
        FabricaApiInstalacao api = CriarApi(A);
        FabricaApiInstalacao tetoCurto = CriarApi(A, new OpcoesFabricaApi { InatividadeMinutos = "30", TetoAbsolutoMinutos = "30", DiretorioChaves = api.DiretorioChaves });
        List<string> sensiveis = [];

        (ClienteAcessoHttp inativa, Guid idInativa, string emailInativa) = await EntrarAsync(api);
        (ClienteAcessoHttp noTeto, Guid idTeto, string emailTeto) = await EntrarAsync(tetoCurto);
        (ClienteAcessoHttp revogada, Guid idRevogada, string emailRevogada) = await EntrarAsync(api);
        foreach ((Guid id, string email, ClienteAcessoHttp cliente) in new[] { (idInativa, emailInativa, inativa), (idTeto, emailTeto, noTeto), (idRevogada, emailRevogada, revogada) })
        {
            sensiveis.Add(id.ToString());
            sensiveis.Add(email);
            sensiveis.Add(cliente.Cookies["bps-session"]);
            sensiveis.AddRange((await ContasSinteticas.SessoesAsync(A, id)).Select(s => s.IdSessao.ToString()));
        }

        // Revogada: logout e reenvio do mesmo cookie.
        Dictionary<string, string> cookiesRevogada = new(revogada.Cookies);
        using (HttpResponseMessage logout = await revogada.LogoutAsync(novoToken: false))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        foreach ((string nome, string valor) in cookiesRevogada)
        {
            revogada.Cookies[nome] = valor;
        }

        ClienteAcessoHttp ilegivel = CriarCliente(api);
        ilegivel.Cookies["bps-session"] = "CfDJ8-cookie-ilegivel-sintetico";
        ClienteAcessoHttp ausente = CriarCliente(api);

        // Inatividade e teto vencem no mesmo instante (30 min sem uso); com 30/30 o teto prevalece.
        Relogio.DefinirAgora(InstanteInicial.AddMinutes(30));
        (string Motivo, ClienteAcessoHttp Cliente)[] casos =
        [
            ("Inatividade", inativa),
            ("TetoAbsoluto", noTeto),
            ("Revogada", revogada),
            ("Invalida", ilegivel),
            ("Ausente", ausente),
        ];

        List<string> respostasNormalizadas = [];
        foreach ((string motivo, ClienteAcessoHttp cliente) in casos)
        {
            using HttpResponseMessage resposta = await cliente.MeAsync();
            JsonElement problema = await AssegurarProblemaAsync(resposta, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
            string traceId = problema.GetProperty("traceId").GetString()!;
            respostasNormalizadas.Add(await NormalizarAsync(resposta));

            EntradaLog entrada = Assert.Single(EntradasSessaoNegada(api, tetoCurto), e => e.Estado.Contains($"TraceId={traceId}", StringComparison.Ordinal));
            Assert.Equal(LogLevel.Information, entrada.Nivel);
            Assert.Equal("SessaoNegada", entrada.Evento.Name);
            Assert.Contains($"Motivo={motivo}", entrada.Estado, StringComparison.Ordinal);
            Assert.Matches("IdOperacao=[0-9a-f]{8}-[0-9a-f]{4}-7[0-9a-f]{3}-", entrada.Estado);
        }

        Assert.Single(respostasNormalizadas.Distinct(StringComparer.Ordinal));

        // Logout com sessão inválida também registra o motivo.
        using (HttpResponseMessage logoutInvalido = await revogada.LogoutAsync())
        {
            JsonElement problema = await AssegurarProblemaAsync(logoutInvalido, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
            string traceId = problema.GetProperty("traceId").GetString()!;
            EntradaLog entrada = Assert.Single(EntradasSessaoNegada(api, tetoCurto), e => e.Estado.Contains($"TraceId={traceId}", StringComparison.Ordinal));
            Assert.Contains("Motivo=Revogada", entrada.Estado, StringComparison.Ordinal);
        }

        foreach (EntradaLog entrada in EntradasSessaoNegada(api, tetoCurto))
        {
            foreach (string sensivel in sensiveis.Append("127.0.0.1").Append("::1").Append("CfDJ8-cookie-ilegivel-sintetico"))
            {
                Assert.DoesNotContain(sensivel, entrada.TextoCompleto, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public async Task LoginComAuthInvalid_NaoGeraSessaoNegada()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("motivo");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);

        using HttpResponseMessage login = await cliente.LoginAsync(email, "SenhaErrada#2026x");
        await AssegurarProblemaAsync(login, HttpStatusCode.Unauthorized, "AUTH_INVALID");
        Assert.Empty(EntradasSessaoNegada(api));
    }

    [Fact]
    public async Task ContaDesativadaDepoisDaAutenticacao_RegistraInvalida()
    {
        FabricaApiInstalacao api = CriarApi(A);
        (ClienteAcessoHttp cliente, Guid id, _) = await EntrarAsync(api);
        await ContasSinteticas.AlterarContaAsync(A, Relogio, id, u => u.EstaAtivo = false);

        using HttpResponseMessage me = await cliente.MeAsync();
        JsonElement problema = await AssegurarProblemaAsync(me, HttpStatusCode.Unauthorized, "AUTH_REQUIRED");
        string traceId = problema.GetProperty("traceId").GetString()!;
        EntradaLog entrada = Assert.Single(EntradasSessaoNegada(api), e => e.Estado.Contains($"TraceId={traceId}", StringComparison.Ordinal));
        Assert.Contains("Motivo=Invalida", entrada.Estado, StringComparison.Ordinal);
        Assert.DoesNotContain(id.ToString(), entrada.TextoCompleto, StringComparison.OrdinalIgnoreCase);
    }

    private static List<EntradaLog> EntradasSessaoNegada(params FabricaApiInstalacao[] apis) =>
        apis.SelectMany(a => a.Logs.Entradas).Where(e => e.Evento.Id == EventoSessaoNegada).ToList();

    /// <summary>
    /// Status, headers (sem <c>Date</c>) e corpo sem <c>traceId</c>, para comparar respostas.
    /// </summary>
    private static async Task<string> NormalizarAsync(HttpResponseMessage resposta)
    {
        IEnumerable<string> headers = resposta.Headers.Concat(resposta.Content.Headers)
            .Where(h => !string.Equals(h.Key, "Date", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(h.Key, "Content-Length", StringComparison.OrdinalIgnoreCase))
            .Select(h => $"{h.Key}:{string.Join(',', h.Value)}")
            .Order(StringComparer.Ordinal);
        JsonObject corpo = JsonNode.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))!.AsObject();
        corpo.Remove("traceId");
        return $"{(int)resposta.StatusCode}|{string.Join('|', headers)}|{corpo.ToJsonString()}";
    }

    private async Task<(ClienteAcessoHttp Cliente, Guid IdUsuario, string Email)> EntrarAsync(FabricaApiInstalacao api)
    {
        string email = ContasSinteticas.NovoEmail("negacao");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        return (cliente, id, email);
    }
}
