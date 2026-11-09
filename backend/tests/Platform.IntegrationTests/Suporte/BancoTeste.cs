namespace Platform.IntegrationTests.Suporte;

using Microsoft.EntityFrameworkCore;
using Platform.Application.Auditoria;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.Auditoria;
using Platform.Infrastructure.Persistence.DesignTime;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.IntegrationTests.Fixtures;

/// <summary>
/// Preparação de bancos de teste pelo mesmo caminho do tooling: preflight do owner,
/// migration inicial (com GRANTs) e marcador, de forma idempotente.
/// </summary>
public static class BancoTeste
{
    /// <summary>
    /// Executa preflight e migration da instalação com o owner (idempotente).
    /// </summary>
    /// <param name="instalacao">Instalação alvo.</param>
    /// <param name="relogio">Relógio do marcador; padrão <see cref="TimeProvider.System"/>.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Tarefa da preparação.</returns>
    public static async Task MigrarAsync(
        InstalacaoTestePostgres instalacao,
        TimeProvider? relogio = null,
        CancellationToken tokenCancelamento = default)
    {
        OpcoesPreflightMigracao opcoes = new(
            CodigoAplicacao: "app-teste",
            IdAmbiente: instalacao.IdAmbiente,
            IdTenant: instalacao.IdTenant,
            EstadoTenant: EstadoTenant.Inativo,
            StringConexao: instalacao.StringConexaoProprietario,
            PermissaoInicializarBancoVazioBruta: "true",
            IdTenantCli: instalacao.IdTenant,
            IdAmbienteCli: instalacao.IdAmbiente);

        await PreflightMigracaoTenant.ExecutarPreflightAsync(opcoes, AcessoTenantDbContextFactory.CadeiaConhecida(), tokenCancelamento);

        await using AcessoTenantDbContext contexto = new(
            CriarOpcoesMigracao(instalacao.StringConexaoProprietario, instalacao.IdTenant, instalacao.IdAmbiente, relogio ?? TimeProvider.System));
        await contexto.Database.MigrateAsync(tokenCancelamento);
    }

    /// <summary>
    /// Opções do contexto para migration com seeding do marcador.
    /// </summary>
    /// <param name="stringConexao">Conexão (owner).</param>
    /// <param name="idTenant">Tenant.</param>
    /// <param name="idAmbiente">Ambiente.</param>
    /// <param name="relogio">Relógio do marcador.</param>
    /// <returns>Opções.</returns>
    public static DbContextOptions<AcessoTenantDbContext> CriarOpcoesMigracao(
        string stringConexao,
        Guid idTenant,
        Guid idAmbiente,
        TimeProvider relogio)
    {
        DbContextOptionsBuilder<AcessoTenantDbContext> construtor = new();
        construtor.UseNpgsql(stringConexao, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation"));
        construtor.UseSeeding((contexto, _) =>
        {
            if (contexto is AcessoTenantDbContext contextoTenant)
            {
                PreflightMigracaoTenant.PopularIdentidadeBanco(contextoTenant, idTenant, idAmbiente, relogio);
            }
        });
        construtor.UseAsyncSeeding(async (contexto, _, tokenCancelamento) =>
        {
            if (contexto is AcessoTenantDbContext contextoTenant)
            {
                await PreflightMigracaoTenant.PopularIdentidadeBancoAsync(contextoTenant, idTenant, idAmbiente, relogio, tokenCancelamento);
            }
        });
        return construtor.Options;
    }

    /// <summary>
    /// Cria um contexto com o <see cref="InterceptadorAuditoria"/> do contexto de operação informado.
    /// </summary>
    /// <param name="stringConexao">Conexão da identidade desejada.</param>
    /// <param name="contextoOperacao">Contexto de operação; nulo cria contexto sem interceptor (somente leitura).</param>
    /// <returns>Novo contexto.</returns>
    public static AcessoTenantDbContext CriarContexto(string stringConexao, IContextoOperacao? contextoOperacao = null)
    {
        DbContextOptionsBuilder<AcessoTenantDbContext> construtor = new();
        construtor.UseNpgsql(stringConexao, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation"));
        if (contextoOperacao is not null)
        {
            construtor.AddInterceptors(new InterceptadorAuditoria(contextoOperacao));
        }

        return new AcessoTenantDbContext(construtor.Options);
    }
}
