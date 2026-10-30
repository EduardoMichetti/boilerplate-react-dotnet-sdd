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

construtor.Services.AddOptions<OpcoesInquilinoInstalacao>()
    .Configure<IConfiguration>((opcoes, config) =>
    {
        string? idAplicacao = config["IdAplicacao"] ?? config["ApplicationId"];
        string? idAmbienteStr = config["IdAmbiente"] ?? config["EnvironmentId"];
        _ = Guid.TryParse(idAmbienteStr, out Guid idAmbiente);

        string? idInquilinoStr = config["IdInquilino"] ?? config["TenantId"];
        _ = Guid.TryParse(idInquilinoStr, out Guid idInquilino);

        string? nomeExibicao = config["NomeExibicaoInquilino"] ?? config["TenantDisplayName"];

        string? estadoStr = config["EstadoInquilino"] ?? config["TenantState"];
        EstadoInquilino? estado = Enum.TryParse<EstadoInquilino>(estadoStr, true, out EstadoInquilino est) ? est : null;

        string? stringConexao = config["StringsConexao:BancoApp"]
            ?? config["ConnectionStrings:AppDb"]
            ?? config["StringsConexao__BancoApp"]
            ?? config["ConnectionStrings__AppDb"]
            ?? config.GetConnectionString("BancoApp")
            ?? config.GetConnectionString("AppDb");

        opcoes.IdAplicacao = idAplicacao;
        opcoes.IdAmbiente = idAmbiente;
        opcoes.IdInquilino = idInquilino;
        opcoes.NomeExibicaoInquilino = nomeExibicao;
        opcoes.EstadoInquilino = estado;
        opcoes.StringsConexao = new StringsConexaoInstalacao { BancoApp = stringConexao };
    });

construtor.Services.AddDbContext<ContextoBancoAcessoInquilino>((provedor, opcoes) =>
{
    OpcoesInquilinoInstalacao config = provedor.GetRequiredService<IOptions<OpcoesInquilinoInstalacao>>().Value;
    if (!string.IsNullOrWhiteSpace(config.StringsConexao?.BancoApp))
    {
        opcoes.UseNpgsql(config.StringsConexao.BancoApp, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
        });
    }
});

construtor.Services.AddScoped<ISondaIdentidadeBancoInquilino, SondaIdentidadeBancoInquilino>();
construtor.Services.AddScoped<VerificacaoSaudeProntidaoInstalacao>();
construtor.Services.AddScoped<InstallationReadinessHealthCheck>();

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
.WithDescription("Confere configuração runtime da instalação e identidade/versão do banco configurado (TenantDatabaseIdentity e histórico de migrations). Não executa migrations, autentica cookies ou consulta contas. Sem exigência de key ring enquanto não existe autenticação HTTP.")
.Produces<RespostaReadiness>(StatusCodes.Status200OK, "application/json")
.ProducesProblem(StatusCodes.Status503ServiceUnavailable);

aplicacao.Run();

public partial class Program;


