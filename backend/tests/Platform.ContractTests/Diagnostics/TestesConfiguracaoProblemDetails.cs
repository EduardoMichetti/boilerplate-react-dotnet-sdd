using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using App.Api.Diagnostics;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Platform.Application.Access;
using Platform.Application.Configuration;
using Platform.ContractTests.Suporte;
using Platform.Domain.Resultados;
using Platform.Domain.Tenancy;
using Xunit;

namespace Platform.ContractTests.Diagnostics;

/// <summary>
/// Formato Problem Details seguro (L1) e, na L2 (T083), entrada inválida 400 com <c>errors</c> por
/// campo, formatos 400/401/403/409/422/429 e <c>Cache-Control: no-store</c>, usando endpoints
/// registrados somente no host do teste.
/// </summary>
public sealed class TestesConfiguracaoProblemDetails
{
    private const string ValorSecreto = "valor-secreto-nao-ecoar-7731";

    private static readonly Erro ErroRegraTeste = new("TESTE_REGRA_VIOLADA", "Regra de teste violada.", TipoErro.RegraNegocio);

    [Fact]
    public async Task EndpointsDeFasesPosterioresPermanecemAusentesRetornando404ProblemDetails()
    {
        // Host Production da fábrica compartilhada (T119): rotas fora do contrato L2 seguem 404.
        await using FabricaApiContrato aplicacao = new();
        using HttpClient cliente = aplicacao.CriarCliente();

        foreach (string caminho in new[] { "/api/v1/auth/login", "/api/v1/me/tenants", "/api/v1/users" })
        {
            using HttpResponseMessage resposta = await cliente.GetAsync(caminho, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
            Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        }
    }

    [Theory]
    [InlineData("/nao-encontrado", HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData("/falha", HttpStatusCode.InternalServerError, "INTERNAL_ERROR")]
    [InlineData("/isolamento", HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE")]
    [InlineData("/configuracao", HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE")]
    public async Task ErrosUtilizamProblemDetailsSeguroMesmoComCabecalhoHtml(
        string caminho, HttpStatusCode statusEsperado, string codigoEsperado)
    {
        const string SegredoPrivado = "segredo-sintetico-nao-pode-vazar";
        ProvedorLogCaptura logs = new();
        WebApplicationBuilder construtor = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production"
        });
        construtor.WebHost.UseTestServer();
        construtor.Logging.ClearProviders().AddProvider(logs);
        construtor.Services.AdicionarProblemDetailsSeguro();
        await using WebApplication aplicacao = construtor.Build();
        aplicacao.UsarProblemDetailsSeguro();
        aplicacao.Run(contexto =>
        {
            switch (contexto.Request.Path.Value)
            {
                case "/falha":
                    throw new InvalidOperationException(SegredoPrivado);
                case "/isolamento":
                    throw new ExcecaoIsolamentoTenant();
                case "/configuracao":
                    throw new ExcecaoConfiguracaoInstalacao(["CodigoAplicacao é obrigatório."]);
                default:
                    contexto.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
            }
        });
        await aplicacao.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient cliente = aplicacao.GetTestClient();
        cliente.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        using HttpResponseMessage resposta = await cliente.GetAsync(caminho, TestContext.Current.CancellationToken);
        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);
        JsonElement problema = documento.RootElement;

        Assert.Equal(statusEsperado, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.True(resposta.Headers.CacheControl?.NoStore, "Cache-Control sem no-store");
        Assert.Equal((int)statusEsperado, problema.GetProperty("status").GetInt32());
        Assert.Equal(codigoEsperado, problema.GetProperty("code").GetString());
        Assert.True(Uri.IsWellFormedUriString(problema.GetProperty("type").GetString(), UriKind.Absolute));
        Assert.Matches("^[0-9a-f]{32}$", problema.GetProperty("traceId").GetString()!);
        Assert.DoesNotContain(SegredoPrivado, corpo, StringComparison.Ordinal);
        Assert.False(problema.TryGetProperty("detail", out _));
        Assert.False(problema.TryGetProperty("exception", out _));
        Assert.All(logs.Mensagens, mensagem => Assert.DoesNotContain(SegredoPrivado, mensagem, StringComparison.Ordinal));

        if (statusEsperado != HttpStatusCode.NotFound)
        {
            Assert.Contains(logs.Mensagens, mensagem => mensagem.Contains(codigoEsperado, StringComparison.Ordinal));
        }
    }

    public static TheoryData<string, string, string> CorposInvalidos()
    {
        TheoryData<string, string, string> dados = [];
        foreach (string ambiente in new[] { "Production", "Development" })
        {
            dados.Add(ambiente, "{\"email\":\"" + ValorSecreto + "\",", "$");
            dados.Add(ambiente, "isso-nao-e-json " + ValorSecreto, "$");
            dados.Add(ambiente, "[1,2]", "$");
            dados.Add(ambiente, "{\"email\":\"a@b.com\",\"password\":\"x\",\"campoDesconhecido\":\"" + ValorSecreto + "\"}", "campoDesconhecido");
            dados.Add(ambiente, "{\"email\":12345,\"password\":\"x\"}", "email");
            dados.Add(ambiente, "{\"email\":null,\"password\":\"x\"}", "email");
            dados.Add(ambiente, "{\"email\":\"a@b.com\",\"password\":null}", "password");
            dados.Add(ambiente, "{\"email\":\"a@b.com\",\"password\":\"x\",\"itens\":[{\"nome\":5}]}", "itens[0].nome");
            dados.Add(ambiente, "{\"email\":\"" + ValorSecreto + "\",\"password\":\"x\"}", "email");
            dados.Add(ambiente, "{\"email\":\"a@b.com\",\"password\":\"x\",\"itens\":[{\"nome\":\"\"}]}", "itens[0].nome");
        }

        return dados;
    }

    [Theory]
    [MemberData(nameof(CorposInvalidos))]
    public async Task EntradaInvalida_Retorna400ComErrosPorCampoSemEcoarValor(string ambiente, string corpo, string chaveEsperada)
    {
        ProvedorLogCaptura logs = new();
        await using WebApplication aplicacao = await CriarHostTesteAsync(ambiente, logs);
        using HttpClient cliente = aplicacao.GetTestClient();

        using StringContent conteudo = new(corpo, Encoding.UTF8, "application/json");
        using HttpResponseMessage resposta = await cliente.PostAsync("/api/v1/teste", conteudo, TestContext.Current.CancellationToken);
        string texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(texto);
        JsonElement problema = documento.RootElement;

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.True(resposta.Headers.CacheControl?.NoStore, "Cache-Control sem no-store");
        Assert.Equal(ErrosPlataforma.EntradaInvalida, problema.GetProperty("code").GetString());
        Assert.Matches("^[0-9a-f]{32}$", problema.GetProperty("traceId").GetString()!);
        JsonElement erros = problema.GetProperty("errors");
        Assert.True(erros.TryGetProperty(chaveEsperada, out JsonElement mensagens), $"errors sem a chave {chaveEsperada}: {texto}");
        Assert.True(mensagens.GetArrayLength() > 0);
        foreach (JsonProperty campo in erros.EnumerateObject())
        {
            Assert.False(campo.Name.StartsWith("$.", StringComparison.Ordinal));
        }

        Assert.DoesNotContain(ValorSecreto, texto, StringComparison.Ordinal);
        Assert.DoesNotContain("could not be", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Path:", texto, StringComparison.Ordinal);
        Assert.False(problema.TryGetProperty("detail", out _));
        Assert.All(logs.Mensagens, m => Assert.DoesNotContain(ValorSecreto, m, StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Niveis, n => n >= LogLevel.Error);
    }

    public static TheoryData<string, int, string> ErrosDeCatalogo() => new()
    {
        { "credenciais", 401, "AUTH_INVALID" },
        { "autenticacao", 401, "AUTH_REQUIRED" },
        { "acesso", 403, "TENANT_ACCESS_DENIED" },
        { "indisponivel", 409, "TENANT_UNAVAILABLE" },
        { "conflito", 409, "SESSION_CONFLICT" },
        { "regra", 422, "TESTE_REGRA_VIOLADA" },
        { "limite", 429, "RATE_LIMITED" },
        { "tenant", 400, "TENANT_REQUIRED" },
        { "csrf", 400, "CSRF_INVALID" },
    };

    [Theory]
    [MemberData(nameof(ErrosDeCatalogo))]
    public async Task ErrosDeCatalogo_TemFormatoProblemDetailsComNoStore(string tipo, int statusEsperado, string codigoEsperado)
    {
        await using WebApplication aplicacao = await CriarHostTesteAsync("Production", new ProvedorLogCaptura());
        using HttpClient cliente = aplicacao.GetTestClient();

        using HttpResponseMessage resposta = await cliente.GetAsync($"/api/v1/erro/{tipo}", TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        JsonElement problema = documento.RootElement;

        Assert.Equal(statusEsperado, (int)resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.True(resposta.Headers.CacheControl?.NoStore, "Cache-Control sem no-store");
        Assert.Equal(statusEsperado, problema.GetProperty("status").GetInt32());
        Assert.Equal(codigoEsperado, problema.GetProperty("code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(problema.GetProperty("title").GetString()));
        Assert.True(Uri.IsWellFormedUriString(problema.GetProperty("type").GetString(), UriKind.Absolute));
        Assert.Matches("^[0-9a-f]{32}$", problema.GetProperty("traceId").GetString()!);
        Assert.False(problema.TryGetProperty("errors", out _));

        if (statusEsperado == 429)
        {
            RetryConditionHeaderValue? retry = resposta.Headers.RetryAfter;
            Assert.NotNull(retry);
            Assert.True(retry.Delta > TimeSpan.Zero);
        }
    }

    [Fact]
    public async Task RequisicaoValida_ChegaAoHandler()
    {
        await using WebApplication aplicacao = await CriarHostTesteAsync("Production", new ProvedorLogCaptura());
        using HttpClient cliente = aplicacao.GetTestClient();

        using StringContent conteudo = new("{\"email\":\"a@b.com\",\"password\":\"x\",\"itens\":[{\"nome\":\"n\"}]}", Encoding.UTF8, "application/json");
        using HttpResponseMessage resposta = await cliente.PostAsync("/api/v1/teste", conteudo, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, resposta.StatusCode);
    }

    private static async Task<WebApplication> CriarHostTesteAsync(string ambiente, ProvedorLogCaptura logs)
    {
        WebApplicationBuilder construtor = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = ambiente });
        construtor.WebHost.UseTestServer();
        construtor.Logging.ClearProviders().AddProvider(logs).SetMinimumLevel(LogLevel.Trace);
        construtor.Services.AdicionarProblemDetailsSeguro();
        construtor.Services.AdicionarJsonEstrito();
        construtor.Services.AdicionarValidacaoRequisicoes();
        construtor.Services.AddValidatorsFromAssemblyContaining<ValidadorRequisicaoTesteContrato>(ServiceLifetime.Singleton);
        WebApplication aplicacao = construtor.Build();
        aplicacao.UsarProblemDetailsSeguro();

        RouteGroupBuilder grupo = aplicacao.MapGroup("/api/v1").AddEndpointFilter<FiltroValidacaoRequisicao>();
        grupo.MapPost("/teste", (RequisicaoTesteContrato requisicao) => Results.NoContent());
        grupo.MapGet("/erro/{tipo}", (string tipo) => tipo switch
        {
            "credenciais" => ConfiguracaoProblemDetails.Resultado(ErrosAcesso.CredenciaisInvalidas),
            "autenticacao" => ConfiguracaoProblemDetails.Resultado(ErrosAcesso.AutenticacaoNecessaria),
            "acesso" => ConfiguracaoProblemDetails.Resultado(ErrosAcesso.AcessoTenantNegado),
            "indisponivel" => ConfiguracaoProblemDetails.Resultado(ErrosAcesso.TenantIndisponivel),
            "conflito" => ConfiguracaoProblemDetails.Resultado(ErrosAcesso.ConflitoSessao),
            "regra" => ConfiguracaoProblemDetails.Resultado(ErroRegraTeste),
            "limite" => ConfiguracaoProblemDetails.Resultado(ConfiguracaoProblemDetails.LimiteExcedido(), 60),
            "tenant" => ConfiguracaoProblemDetails.Resultado(ConfiguracaoProblemDetails.TenantObrigatorio()),
            "csrf" => ConfiguracaoProblemDetails.Resultado(ConfiguracaoProblemDetails.CsrfInvalido()),
            _ => Results.NotFound()
        });

        await aplicacao.StartAsync(TestContext.Current.CancellationToken);
        return aplicacao;
    }

    private sealed class ProvedorLogCaptura : ILoggerProvider
    {
        public ConcurrentQueue<string> Mensagens { get; } = new();

        public ConcurrentQueue<LogLevel> Niveis { get; } = new();

        public ILogger CreateLogger(string nomeCategoria) => new RegistradorLogCaptura(Mensagens, Niveis);

        public void Dispose() { }

        private sealed class RegistradorLogCaptura(ConcurrentQueue<string> mensagens, ConcurrentQueue<LogLevel> niveis) : ILogger
        {
            public IDisposable? BeginScope<TEstado>(TEstado estado) where TEstado : notnull => null;
            public bool IsEnabled(LogLevel nivelLog) => true;

            public void Log<TEstado>(
                LogLevel nivelLog,
                EventId idEvento,
                TEstado estado,
                Exception? excecao,
                Func<TEstado, Exception?, string> formatador)
            {
                mensagens.Enqueue(formatador(estado, excecao) + excecao);
                niveis.Enqueue(nivelLog);
            }
        }
    }
}

/// <summary>
/// DTO de entrada exclusivo do teste de contrato de erros.
/// </summary>
public sealed class RequisicaoTesteContrato
{
    /// <summary>Email.</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>Senha.</summary>
    [JsonPropertyName("password")]
    public string Senha { get; init; } = string.Empty;

    /// <summary>Itens aninhados.</summary>
    public List<ItemTesteContrato> Itens { get; init; } = [];
}

/// <summary>
/// Item aninhado do DTO de teste.
/// </summary>
public sealed class ItemTesteContrato
{
    /// <summary>Nome.</summary>
    public string Nome { get; init; } = string.Empty;
}

/// <summary>
/// Validador do DTO de teste (formato, sem I/O).
/// </summary>
public sealed class ValidadorRequisicaoTesteContrato : AbstractValidator<RequisicaoTesteContrato>
{
    /// <summary>Regras.</summary>
    public ValidadorRequisicaoTesteContrato()
    {
        RuleFor(r => r.Email).NotEmpty().WithMessage("O email deve ser informado.")
            .EmailAddress().WithMessage("O email deve ter formato válido.");
        RuleForEach(r => r.Itens).ChildRules(item =>
            item.RuleFor(i => i.Nome).NotEmpty().WithMessage("O nome deve ser informado."));
    }
}
