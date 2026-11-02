using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using App.Api.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Xunit;

namespace Platform.ContractTests.Diagnostics;

public sealed class TestesConfiguracaoProblemDetails
{
    [Fact]
    public async Task EndpointsDeFasesPosterioresPermanecemAusentesRetornando404ProblemDetails()
    {
        await using WebApplicationFactory<Program> aplicacao = new();
        using HttpClient cliente = aplicacao.CreateClient();

        foreach (string caminho in new[] { "/api/v1/auth/login", "/api/v1/me", "/api/v1/context", "/api/v1/users" })
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
        const string segredoPrivado = "segredo-sintetico-nao-pode-vazar";
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
                    throw new InvalidOperationException(segredoPrivado);
                case "/isolamento":
                    throw new ExcecaoIsolamentoTenant();
                case "/configuracao":
                    throw new ExcecaoConfiguracaoInstalacao(["IdAplicacao é obrigatório."]);
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
        Assert.Equal((int)statusEsperado, problema.GetProperty("status").GetInt32());
        Assert.Equal(codigoEsperado, problema.GetProperty("code").GetString());
        Assert.True(Uri.IsWellFormedUriString(problema.GetProperty("type").GetString(), UriKind.Absolute));
        Assert.Matches("^[0-9a-f]{32}$", problema.GetProperty("traceId").GetString()!);
        Assert.DoesNotContain(segredoPrivado, corpo, StringComparison.Ordinal);
        Assert.False(problema.TryGetProperty("detail", out _));
        Assert.False(problema.TryGetProperty("exception", out _));
        Assert.All(logs.Mensagens, mensagem => Assert.DoesNotContain(segredoPrivado, mensagem, StringComparison.Ordinal));

        if (statusEsperado != HttpStatusCode.NotFound)
        {
            Assert.Contains(logs.Mensagens, mensagem => mensagem.Contains(codigoEsperado, StringComparison.Ordinal));
        }
    }

    private sealed class ProvedorLogCaptura : ILoggerProvider
    {
        public ConcurrentQueue<string> Mensagens { get; } = new();

        public ILogger CreateLogger(string nomeCategoria) => new RegistradorLogCaptura(Mensagens);

        public void Dispose() { }

        private sealed class RegistradorLogCaptura(ConcurrentQueue<string> mensagens) : ILogger
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
            }
        }
    }
}
