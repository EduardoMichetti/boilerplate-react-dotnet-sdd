using App.Api.Diagnostics;
using App.Api.Health;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Platform.Application.Configuration;
using Platform.Application.Tenancy;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess;

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

construtor.Services.AddDbContext<AcessoTenantDbContext>((provedor, opcoes) =>
{
    OpcoesInstalacaoTenant config = provedor.GetRequiredService<IOptions<OpcoesInstalacaoTenant>>().Value;
    if (!string.IsNullOrWhiteSpace(config.StringsConexao?.BancoApp))
    {
        opcoes.UseNpgsql(config.StringsConexao.BancoApp, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
        });
    }
});

construtor.Services.AddScoped<IVerificadorIdentidadeBancoTenant, VerificadorIdentidadeBancoTenant>();
construtor.Services.AddScoped<VerificacaoSaudeProntidaoInstalacao>();

construtor.Services.AddOpenApi("v1", opcoes =>
{
    opcoes.OpenApiVersion = Microsoft.OpenApi.OpenApiSpecVersion.OpenApi3_0;
    opcoes.AddDocumentTransformer((documento, _, _) =>
    {
        documento.Info.Title = "boilerplate-react-dotnet-sdd - Backend I1";
        documento.Info.Version = "1.0.0";
        documento.Info.Description = "Contrato de design do primeiro incremento: backend executável e migration de usuários/acesso. Somente saúde é exposta. Sem endpoint de usuário, login, concessão ou painel; contratos de acesso HTTP pertencem a future-openapi.json. Não afirma implementação concluída.";
        return Task.CompletedTask;
    });
});

WebApplication aplicacao = construtor.Build();

aplicacao.UsarProblemDetailsSeguro();

aplicacao.MapOpenApi("/openapi/v1.json");

// Grupo das rotas de acesso L2: valida todo argumento Requisicao* antes do handler (ADR 0003 D2).
RouteGroupBuilder grupoApiV1 = aplicacao.MapGroup("/api/v1")
    .AddEndpointFilter<FiltroValidacaoRequisicao>();

aplicacao.MapGet("/health/live", () => Results.Ok(new RespostaLiveness()))
    .WithName("getLiveness")
    .WithSummary("Verificar processo vivo")
    .WithDescription("Não acessa catálogo, banco de tenant ou sessão, mesmo com cookie enviado. Não retorna conexão, secret ou detalhes internos.")
    .Produces<RespostaLiveness>(StatusCodes.Status200OK, "application/json");

aplicacao.MapGet("/health/ready", async (
    [FromServices] VerificacaoSaudeProntidaoInstalacao verificacao,
    HttpContext contexto,
    CancellationToken ct) =>
{
    HealthCheckResult resultado = await verificacao.CheckHealthAsync(new HealthCheckContext(), ct);
    if (resultado.Status == HealthStatus.Healthy)
    {
        contexto.Response.StatusCode = StatusCodes.Status200OK;
        await contexto.Response.WriteAsJsonAsync(new RespostaReadiness(), cancellationToken: ct);
        return;
    }

    await ConfiguracaoProblemDetails.EscreverAsync(contexto, StatusCodes.Status503ServiceUnavailable);
})
.WithName("getReadiness")
.WithSummary("Verificar backend pronto no escopo I1")
.WithDescription("Confere configuração runtime da instalação e identidade/versão do banco configurado (IdentidadeBancoTenant e histórico de migrations). Não executa migrations, autentica cookies ou consulta contas. Sem exigência de key ring enquanto não existe autenticação HTTP.")
.Produces<RespostaReadiness>(StatusCodes.Status200OK, "application/json")
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

aplicacao.Run();

public partial class Program;


