namespace Platform.IntegrationTests.Http;

using System.Net;
using Microsoft.Extensions.Logging;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Xunit;

/// <summary>
/// T129: com captura de logs em nível Trace e todas as categorias, nenhum fluxo de acesso registra
/// senha, email ou nome de exibição (dados pessoais), valores de cookies ou token antiforgery,
/// ticket protegido, string de conexão ou senha do runtime; erros carregam <c>traceId</c> e <c>IdOperacao</c>.
/// </summary>
public sealed class TestesLogsSemSegredos(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    [Fact]
    public async Task FluxosDeAcesso_NaoRegistramSegredosNemDadosPessoais()
    {
        const string NomeExibicao = "Nome Pessoal Sigiloso";
        const string SenhaErrada = "SenhaErradaSigilosa#77";
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("logs");
        string emailBloqueio = ContasSinteticas.NovoEmail("logsbloqueio");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA, nome: NomeExibicao);
        await ContasSinteticas.CriarContaAsync(A, Relogio, emailBloqueio, ContasSinteticas.SenhaPadraoA, nome: NomeExibicao);
        HashSet<string> segredos = new(StringComparer.Ordinal) { ContasSinteticas.SenhaPadraoA, SenhaErrada, email, emailBloqueio, NomeExibicao, A.SenhaExecucao, A.StringConexaoExecucao };

        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        Coletar(cliente, segredos);
        using (HttpResponseMessage me = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        }

        // Senha errada e lockout.
        ClienteAcessoHttp atacante = CriarCliente(api);
        for (int tentativa = 1; tentativa <= 5; tentativa++)
        {
            using HttpResponseMessage falha = await atacante.LoginAsync(emailBloqueio, SenhaErrada);
            Assert.Equal(HttpStatusCode.Unauthorized, falha.StatusCode);
            Coletar(atacante, segredos);
        }

        // CSRF inválido e JSON malformado contendo a senha.
        cliente.TokenCsrf = "token-csrf-invalido-sigiloso";
        using (HttpResponseMessage csrf = await cliente.EnviarAsync(HttpMethod.Delete, ClienteAcessoHttp.RotaSessaoAtual))
        {
            Assert.Equal(HttpStatusCode.BadRequest, csrf.StatusCode);
        }

        await cliente.ObterTokenAsync();
        Coletar(cliente, segredos);
        using (HttpResponseMessage malformado = await cliente.EnviarAsync(
            HttpMethod.Post, ClienteAcessoHttp.RotaSessoes, $"{{\"email\":\"{email}\",\"password\":\"{ContasSinteticas.SenhaPadraoA}\""))
        {
            Assert.Equal(HttpStatusCode.BadRequest, malformado.StatusCode);
        }

        // Falha injetada (503) e logout bem-sucedido.
        api.Falhas.FalharProximasGravacoesSessao(1);
        using (HttpResponseMessage indisponivel = await cliente.LogoutAsync(novoToken: false))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, indisponivel.StatusCode);
        }

        Dictionary<string, string> cookiesAntesDoLogout = new(cliente.Cookies);
        using (HttpResponseMessage logout = await cliente.LogoutAsync(novoToken: false))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        // Sessão negada (L2.1, D37): o evento 1002 entra na mesma varredura.
        foreach ((string nome, string valor) in cookiesAntesDoLogout)
        {
            cliente.Cookies[nome] = valor;
        }

        using (HttpResponseMessage negada = await cliente.MeAsync())
        {
            Assert.Equal(HttpStatusCode.Unauthorized, negada.StatusCode);
        }

        EntradaLog sessaoNegada = Assert.Single(api.Logs.Entradas, e => e.Evento.Id == 1002);
        Assert.Contains("Motivo=Revogada", sessaoNegada.Estado, StringComparison.Ordinal);

        Assert.NotEmpty(api.Logs.Entradas);
        List<string> vazamentos = api.Logs.Entradas
            .SelectMany(e => segredos.Where(s => s.Length >= 6 && e.TextoCompleto.Contains(s, StringComparison.Ordinal)).Select(s => $"{e.Categoria}: {s[..3]}…"))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        Assert.True(vazamentos.Count == 0, $"Segredos em logs: {string.Join("; ", vazamentos)}");

        EntradaLog erro = Assert.Single(api.Logs.Entradas, e => e.Evento.Id == 1000 && e.Nivel == LogLevel.Error);
        Assert.Matches("TraceId=[0-9a-f]{32}", erro.Estado);
        Assert.Matches("IdOperacao=[0-9a-f-]{36}", erro.Estado);
    }

    private static void Coletar(ClienteAcessoHttp cliente, HashSet<string> segredos)
    {
        foreach (string valor in cliente.Cookies.Values)
        {
            segredos.Add(valor);
        }

        if (cliente.TokenCsrf is { } token)
        {
            segredos.Add(token);
        }
    }
}
