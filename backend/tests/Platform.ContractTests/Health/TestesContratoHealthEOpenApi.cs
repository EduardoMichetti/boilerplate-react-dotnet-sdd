namespace Platform.ContractTests.Health;

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Platform.Application.Configuration;
using Platform.Application.Tenancy;
using Platform.Domain.Tenancy;
using Xunit;

/// <summary>
/// Testes de contrato HTTP para os endpoints mínimos expostos no Incremento I1 (/health/live, /health/ready, /openapi/v1.json),
/// garantindo respostas seguras, diagnóstico com ProblemDetails e ausência de rotas futuras.
/// Atende às tarefas T025 e aos requisitos da User Story 5.
/// </summary>
public sealed class TestesContratoHealthEOpenApi
{
    private static readonly Guid IdAmbientePadrao = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid IdInquilinoPadrao = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid EpocaAcessoPadrao = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task GetLive_Retorna200EStatusAlive_SemDependerDeBancoOuCookies()
    {
        await using WebApplicationFactory<Program> aplicacao = CriarAplicacaoCom(
            CriarOpcoesValidas(),
            new SondaIdentidadeBancoFake(() => throw new InvalidOperationException("Banco não deve ser acessado por liveness!")));

        using HttpClient cliente = aplicacao.CreateClient();
        cliente.DefaultRequestHeaders.Add("Cookie", "__Host-bps-session=token-sintetico-que-nao-deve-ser-lido");
        cliente.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);

        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);

        Assert.Equal("alive", documento.RootElement.GetProperty("status").GetString());
        Assert.False(resposta.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task GetReady_Retorna200EStatusReady_QuandoInstalacaoEValidaEPronta()
    {
        InstantaneoIdentidadeBancoInquilino instantaneoEsperado = new(
            IdInquilinoPadrao,
            IdAmbientePadrao,
            EpocaAcessoPadrao,
            PossuiVersaoMigracaoEsperada: true);

        await using WebApplicationFactory<Program> aplicacao = CriarAplicacaoCom(
            CriarOpcoesValidas(),
            new SondaIdentidadeBancoFake(() => Task.FromResult(instantaneoEsperado)));

        using HttpClient cliente = aplicacao.CreateClient();

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);

        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);

        Assert.Equal("ready", documento.RootElement.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetReady_Retorna503ProblemDetails_QuandoMarcadorDoBancoDiverge()
    {
        // IdInquilino diverge da configuração
        InstantaneoIdentidadeBancoInquilino instantaneoDivergente = new(
            Guid.NewGuid(),
            IdAmbientePadrao,
            EpocaAcessoPadrao,
            PossuiVersaoMigracaoEsperada: true);

        await using WebApplicationFactory<Program> aplicacao = CriarAplicacaoCom(
            CriarOpcoesValidas(),
            new SondaIdentidadeBancoFake(() => Task.FromResult(instantaneoDivergente)));

        using HttpClient cliente = aplicacao.CreateClient();

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);
        JsonElement raiz = documento.RootElement;

        Assert.Equal(503, raiz.GetProperty("status").GetInt32());
        Assert.Equal("DEPENDENCY_UNAVAILABLE", raiz.GetProperty("code").GetString());
        Assert.True(Uri.IsWellFormedUriString(raiz.GetProperty("type").GetString(), UriKind.Absolute));
        Assert.Matches("^[0-9a-f]{32}$", raiz.GetProperty("traceId").GetString()!);
        Assert.False(corpo.Contains("Host=", StringComparison.OrdinalIgnoreCase));
        Assert.False(corpo.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetReady_Retorna503ProblemDetails_QuandoBancoEstaIndisponivelOuLancaExcecao()
    {
        const string segredoPrivado = "postgres-secret-que-nao-pode-vazar";

        await using WebApplicationFactory<Program> aplicacao = CriarAplicacaoCom(
            CriarOpcoesValidas(),
            new SondaIdentidadeBancoFake(() => throw new TimeoutException($"Conexão falhou para {segredoPrivado}")));

        using HttpClient cliente = aplicacao.CreateClient();

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);
        JsonElement raiz = documento.RootElement;

        Assert.Equal(503, raiz.GetProperty("status").GetInt32());
        Assert.Equal("DEPENDENCY_UNAVAILABLE", raiz.GetProperty("code").GetString());
        Assert.DoesNotContain(segredoPrivado, corpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetReady_Retorna503ProblemDetails_QuandoInquilinoEstaInativo()
    {
        InstantaneoIdentidadeBancoInquilino instantaneoEsperado = new(
            IdInquilinoPadrao,
            IdAmbientePadrao,
            EpocaAcessoPadrao,
            PossuiVersaoMigracaoEsperada: true);

        await using WebApplicationFactory<Program> aplicacao = CriarAplicacaoCom(
            CriarOpcoesValidas(estado: EstadoInquilino.Inativo),
            new SondaIdentidadeBancoFake(() => Task.FromResult(instantaneoEsperado)));

        using HttpClient cliente = aplicacao.CreateClient();

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);

        Assert.Equal(503, documento.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("DEPENDENCY_UNAVAILABLE", documento.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetReady_Retorna503ProblemDetails_QuandoExistemMigracoesPendentes()
    {
        InstantaneoIdentidadeBancoInquilino instantaneoComMigracoesPendentes = new(
            IdInquilinoPadrao,
            IdAmbientePadrao,
            EpocaAcessoPadrao,
            PossuiVersaoMigracaoEsperada: false);

        await using WebApplicationFactory<Program> aplicacao = CriarAplicacaoCom(
            CriarOpcoesValidas(),
            new SondaIdentidadeBancoFake(() => Task.FromResult(instantaneoComMigracoesPendentes)));

        using HttpClient cliente = aplicacao.CreateClient();

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);

        Assert.Equal(503, documento.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("DEPENDENCY_UNAVAILABLE", documento.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetOpenApi_Retorna200EContratoEmConformidadeComEspecificacao()
    {
        await using WebApplicationFactory<Program> aplicacao = CriarAplicacaoCom(
            CriarOpcoesValidas(),
            new SondaIdentidadeBancoFake(() => Task.FromResult(new InstantaneoIdentidadeBancoInquilino(
                IdInquilinoPadrao, IdAmbientePadrao, EpocaAcessoPadrao, true))));

        using HttpClient cliente = aplicacao.CreateClient();

        using HttpResponseMessage resposta = await cliente.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("application/json", resposta.Content.Headers.ContentType?.MediaType);

        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);
        JsonElement raiz = documento.RootElement;

        Assert.True(raiz.TryGetProperty("openapi", out JsonElement versaoOpenApi));
        Assert.StartsWith("3.", versaoOpenApi.GetString());

        JsonElement paths = raiz.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/health/live", out _));
        Assert.True(paths.TryGetProperty("/health/ready", out _));

        // Rotas futuras de I2/I3 não podem estar no contrato OpenAPI de I1
        Assert.False(paths.TryGetProperty("/api/v1/auth/login", out _));
        Assert.False(paths.TryGetProperty("/api/v1/me", out _));
        Assert.False(paths.TryGetProperty("/api/v1/context", out _));
    }

    [Theory]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/me")]
    [InlineData("/api/v1/context")]
    [InlineData("/rota-inexistente")]
    public async Task RotasAusentes_Retornam404ProblemDetails(string rotaAusente)
    {
        await using WebApplicationFactory<Program> aplicacao = CriarAplicacaoCom(
            CriarOpcoesValidas(),
            new SondaIdentidadeBancoFake(() => Task.FromResult(new InstantaneoIdentidadeBancoInquilino(
                IdInquilinoPadrao, IdAmbientePadrao, EpocaAcessoPadrao, true))));

        using HttpClient cliente = aplicacao.CreateClient();

        using HttpResponseMessage resposta = await cliente.GetAsync(rotaAusente, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);
        JsonElement raiz = documento.RootElement;

        Assert.Equal(404, raiz.GetProperty("status").GetInt32());
        Assert.Equal("NOT_FOUND", raiz.GetProperty("code").GetString());
        Assert.Matches("^[0-9a-f]{32}$", raiz.GetProperty("traceId").GetString()!);
    }

    private static OpcoesInquilinoInstalacao CriarOpcoesValidas(EstadoInquilino estado = EstadoInquilino.Ativo) => new()
    {
        IdAplicacao = "boilerplate-react-dotnet-sdd",
        IdAmbiente = IdAmbientePadrao,
        IdInquilino = IdInquilinoPadrao,
        NomeExibicaoInquilino = "Cliente de Teste",
        EstadoInquilino = estado,
        StringsConexao = new StringsConexaoInstalacao
        {
            BancoApp = "Host=localhost;Database=banco_teste;Username=postgres;Password=teste"
        }
    };

    private static WebApplicationFactory<Program> CriarAplicacaoCom(
        OpcoesInquilinoInstalacao opcoes,
        ISondaIdentidadeBancoInquilino sonda)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IOptions<OpcoesInquilinoInstalacao>>();
                services.AddSingleton<IOptions<OpcoesInquilinoInstalacao>>(Options.Create(opcoes));

                services.RemoveAll<ISondaIdentidadeBancoInquilino>();
                services.AddScoped<ISondaIdentidadeBancoInquilino>(_ => sonda);
            });
        });
    }

    private sealed class SondaIdentidadeBancoFake(
        Func<Task<InstantaneoIdentidadeBancoInquilino>> acaoLeitura) : ISondaIdentidadeBancoInquilino
    {
        public Task<InstantaneoIdentidadeBancoInquilino> LerAsync(CancellationToken tokenCancelamento = default)
            => acaoLeitura();
    }
}
