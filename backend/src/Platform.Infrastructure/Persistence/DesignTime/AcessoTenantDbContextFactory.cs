namespace Platform.Infrastructure.Persistence.DesignTime;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.Auditoria;
using Platform.Infrastructure.Persistence.TenantAccess;

/// <summary>
/// Fábrica de DbContext em tempo de design (EF Core CLI) para instanciar o AcessoTenantDbContext.
/// </summary>
public sealed class AcessoTenantDbContextFactory : IDesignTimeDbContextFactory<AcessoTenantDbContext>
{
    public AcessoTenantDbContext CreateDbContext(string[] argumentos)
    {
        Guid? idTenantCli = null;
        Guid? idAmbienteCli = null;

        for (int i = 0; i < argumentos.Length; i++)
        {
            if ((string.Equals(argumentos[i], "--id-tenant", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(argumentos[i], "--tenant-id", StringComparison.OrdinalIgnoreCase)) && i + 1 < argumentos.Length)
            {
                if (Guid.TryParse(argumentos[i + 1], out Guid tId))
                {
                    idTenantCli = tId;
                }
            }
            else if ((string.Equals(argumentos[i], "--id-ambiente", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(argumentos[i], "--environment-id", StringComparison.OrdinalIgnoreCase)) && i + 1 < argumentos.Length)
            {
                if (Guid.TryParse(argumentos[i + 1], out Guid eId))
                {
                    idAmbienteCli = eId;
                }
            }
        }

        string? codigoAplicacao = Environment.GetEnvironmentVariable("CodigoAplicacao") ?? "boilerplate-react-dotnet-sdd";
        string? idAmbienteBruto = Environment.GetEnvironmentVariable("IdAmbiente");
        Guid idAmbiente = Guid.TryParse(idAmbienteBruto, out Guid eGuid)
            ? eGuid
            : idAmbienteCli ?? Guid.Parse("11111111-1111-4111-8111-111111111111");

        string? idTenantBruto = Environment.GetEnvironmentVariable("IdTenant");
        Guid idTenant = Guid.TryParse(idTenantBruto, out Guid tGuid)
            ? tGuid
            : idTenantCli ?? Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

        string? estadoBruto = Environment.GetEnvironmentVariable("EstadoTenant");
        EstadoTenant estadoTenant = Enum.TryParse<EstadoTenant>(estadoBruto, true, out EstadoTenant estadoConvertido)
            ? estadoConvertido
            : EstadoTenant.Inativo;

        string? permitirInicializarBruto = Environment.GetEnvironmentVariable("Migracoes__PermitirInicializarBancoVazio")
            ?? Environment.GetEnvironmentVariable("Migracoes:PermitirInicializarBancoVazio");

        string? stringConexao = Environment.GetEnvironmentVariable("StringsConexao__BancoApp")
            ?? Environment.GetEnvironmentVariable("StringsConexao:BancoApp");

        bool ehFallbackTempoDesign = string.IsNullOrWhiteSpace(stringConexao);
        string stringConexaoEfetiva = ehFallbackTempoDesign
            ? "Host=localhost;Database=boilerplate_designtime;Username=postgres"
            : stringConexao!;

        TimeProvider relogio = TimeProvider.System;

        if (!ehFallbackTempoDesign)
        {
            OpcoesPreflightMigracao opcoesPreflight = new(
                CodigoAplicacao: codigoAplicacao,
                IdAmbiente: idAmbiente,
                IdTenant: idTenant,
                EstadoTenant: estadoTenant,
                StringConexao: stringConexaoEfetiva,
                PermissaoInicializarBancoVazioBruta: permitirInicializarBruto,
                IdTenantCli: idTenantCli,
                IdAmbienteCli: idAmbienteCli);

            PreflightMigracaoTenant.ExecutarPreflightAsync(opcoesPreflight, CadeiaConhecida()).GetAwaiter().GetResult();
        }

        DbContextOptionsBuilder<AcessoTenantDbContext> construtorOpcoes = new();
        construtorOpcoes.UseNpgsql(stringConexaoEfetiva, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
        });
        construtorOpcoes.AddInterceptors(new InterceptadorAuditoria(new ContextoOperacaoFerramentaMigracao()));

        construtorOpcoes.UseSeeding((contexto, _) =>
        {
            if (contexto is AcessoTenantDbContext contextoTenant)
            {
                PreflightMigracaoTenant.PopularIdentidadeBanco(contextoTenant, idTenant, idAmbiente, relogio);
            }
        });

        construtorOpcoes.UseAsyncSeeding(async (contexto, _, tokenCancelamento) =>
        {
            if (contexto is AcessoTenantDbContext contextoTenant)
            {
                await PreflightMigracaoTenant.PopularIdentidadeBancoAsync(contextoTenant, idTenant, idAmbiente, relogio, tokenCancelamento);
            }
        });

        return new AcessoTenantDbContext(construtorOpcoes.Options);
    }

    /// <summary>
    /// IDs das migrations conhecidas por este binário, em ordem (sem abrir conexão).
    /// </summary>
    /// <returns>Cadeia conhecida.</returns>
    public static IReadOnlyList<string> CadeiaConhecida()
    {
        DbContextOptionsBuilder<AcessoTenantDbContext> construtor = new();
        construtor.UseNpgsql("Host=localhost", npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation"));
        using AcessoTenantDbContext contexto = new(construtor.Options);
        return contexto.Database.GetMigrations().ToList();
    }
}
