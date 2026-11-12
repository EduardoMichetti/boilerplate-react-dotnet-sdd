using System.Net;
using App.Api.Access;
using App.Api.Diagnostics;
using App.Api.Health;
using App.Api.OpenApi;
using App.Api.Testing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Platform.Application.Auditoria;
using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Platform.Infrastructure;

WebApplicationBuilder construtor = WebApplication.CreateBuilder(args);

construtor.Services.AdicionarProblemDetailsSeguro();
construtor.Services.AdicionarJsonEstrito();
construtor.Services.AdicionarValidacaoRequisicoes();

// Fonte única do instante atual em todo o código de produção (Constituição IX).
construtor.Services.AddSingleton(TimeProvider.System);

construtor.Services.AddOptions<OpcoesInstalacaoTenant>()
    .Configure<IConfiguration>((opcoes, config) =>
    {
        opcoes.CodigoAplicacao = config["CodigoAplicacao"];
        _ = Guid.TryParse(config["IdAmbiente"], out Guid idAmbiente);
        _ = Guid.TryParse(config["IdTenant"], out Guid idTenant);
        opcoes.IdAmbiente = idAmbiente;
        opcoes.IdTenant = idTenant;
        opcoes.NomeExibicaoTenant = config["NomeExibicaoTenant"];
        opcoes.EstadoTenant = Enum.TryParse<EstadoTenant>(config["EstadoTenant"], true, out EstadoTenant est) ? est : null;
        opcoes.StringsConexao = new StringsConexaoInstalacao
        {
            BancoApp = config["StringsConexao:BancoApp"] ?? config["StringsConexao__BancoApp"]
        };
    });

// Contexto confiável por requisição: instante único, IdOperacao v7 e autoria anônima até a verificação.
construtor.Services.AddScoped<ContextoOperacaoHttp>();
construtor.Services.AddScoped<IContextoOperacao>(p => p.GetRequiredService<ContextoOperacaoHttp>());
construtor.Services.AddScoped<IRegistroUsuarioVerificado>(p => p.GetRequiredService<ContextoOperacaoHttp>());

construtor.Services.AdicionarInfraestruturaAcesso();
construtor.Services.AdicionarAutenticacaoSessao(construtor.Configuration);
construtor.Services.AdicionarCasosUsoAcesso();
construtor.Services.AdicionarLimiteLogin();

IReadOnlyList<IPAddress> proxiesConfiaveis = ConfiguracaoLimiteLogin.LerProxiesConfiaveis(construtor.Configuration);
construtor.Services.AdicionarCabecalhosEncaminhados(proxiesConfiaveis);

construtor.Services.AddScoped<VerificacaoSaudeProntidaoInstalacao>();
construtor.Services.AddScoped<VerificacaoProtecaoChaves>();

construtor.Services.AdicionarOpenApiAcesso();

WebApplication aplicacao = construtor.Build();

aplicacao.UsarProblemDetailsSeguro();

// Forwarded headers somente com proxies explicitamente confiáveis (G7); padrão L2: desligado.
if (proxiesConfiaveis.Count > 0)
{
    aplicacao.UseForwardedHeaders();
}

aplicacao.UseRouting();

// Ordem G2: limiter (só login) → X-Tenant-Id → marcador/versão → sessão → autorização → corpo → CSRF.
aplicacao.UseRateLimiter();
aplicacao.UseMiddleware<MiddlewareCabecalhoTenant>();
aplicacao.UseMiddleware<MiddlewareVerificacaoInstalacao>();
aplicacao.UseMiddleware<MiddlewareAutenticacaoSessao>();
aplicacao.UseAuthorization();

aplicacao.MapOpenApi("/openapi/v1.json");

// Grupo das rotas de acesso L2: valida todo argumento Requisicao* antes do handler (ADR 0003 D2).
RouteGroupBuilder grupoV1 = aplicacao.MapGroup("/api/v1")
    .AddEndpointFilter<FiltroValidacaoRequisicao>();

grupoV1.MapearEndpointsAcesso();

foreach (IConfiguradorRotasTeste configurador in aplicacao.Services.GetServices<IConfiguradorRotasTeste>())
{
    configurador.Configurar(grupoV1);
}

aplicacao.MapGet("/health/live", () => Results.Ok(new RespostaLiveness()))
    .WithName("getLiveness")
    .WithTags("Health")
    .WithSummary("Verificar que o processo está vivo")
    .WithDescription("Fora do pipeline de autenticação, ignora cookies e header tenant. Não consulta banco, sessão ou key ring.")
    .Produces<RespostaLiveness>(StatusCodes.Status200OK, "application/json");

aplicacao.MapGet("/health/ready", async (
    [FromServices] VerificacaoSaudeProntidaoInstalacao verificacaoInstalacao,
    [FromServices] VerificacaoProtecaoChaves verificacaoChaves,
    HttpContext contexto,
    CancellationToken ct) =>
{
    HealthCheckResult instalacao = await verificacaoInstalacao.CheckHealthAsync(new HealthCheckContext(), ct);
    HealthCheckResult chaves = instalacao.Status == HealthStatus.Healthy
        ? await verificacaoChaves.CheckHealthAsync(new HealthCheckContext(), ct)
        : instalacao;
    if (chaves.Status == HealthStatus.Healthy)
    {
        contexto.Response.StatusCode = StatusCodes.Status200OK;
        await contexto.Response.WriteAsJsonAsync(new RespostaReadiness(), cancellationToken: ct);
        return;
    }

    await ConfiguracaoProblemDetails.EscreverAsync(contexto, StatusCodes.Status503ServiceUnavailable);
})
.WithName("getReadiness")
.WithTags("Health")
.WithSummary("Verificar disponibilidade da fundação")
.WithDescription("Confere configuração, marcador, cadeia de migrations completa e round-trip do key ring. Não executa migrations, não autentica cookies nem consulta contas.")
.Produces<RespostaReadiness>(StatusCodes.Status200OK, "application/json")
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

aplicacao.Run();

public partial class Program;
