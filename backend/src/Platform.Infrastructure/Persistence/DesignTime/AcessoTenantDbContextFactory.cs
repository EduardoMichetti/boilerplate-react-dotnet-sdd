namespace Platform.Infrastructure.Persistence.DesignTime;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Domain.Tenancy;
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

        string? idAplicacao = Environment.GetEnvironmentVariable("IdAplicacao") ?? "boilerplate-react-dotnet-sdd";
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

        if (!ehFallbackTempoDesign)
        {
            OpcoesPreflightMigracao opcoesPreflight = new(
                IdAplicacao: idAplicacao,
                IdAmbiente: idAmbiente,
                IdTenant: idTenant,
                EstadoTenant: estadoTenant,
                StringConexao: stringConexaoEfetiva,
                PermissaoInicializarBancoVazioBruta: permitirInicializarBruto,
                IdTenantCli: idTenantCli,
                IdAmbienteCli: idAmbienteCli);

            PreflightMigracaoTenant.ExecutarPreflightAsync(opcoesPreflight).GetAwaiter().GetResult();
        }

        DbContextOptionsBuilder<AcessoTenantDbContext> construtorOpcoes = new();
        construtorOpcoes.UseNpgsql(stringConexaoEfetiva, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
        });

        construtorOpcoes.UseSeeding((contexto, _) =>
        {
            if (contexto is AcessoTenantDbContext contextoTenant)
            {
                PreflightMigracaoTenant.PopularIdentidadeBanco(contextoTenant, idTenant, idAmbiente);
            }
        });

        construtorOpcoes.UseAsyncSeeding(async (contexto, _, tokenCancelamento) =>
        {
            if (contexto is AcessoTenantDbContext contextoTenant)
            {
                await PreflightMigracaoTenant.PopularIdentidadeBancoAsync(contextoTenant, idTenant, idAmbiente, tokenCancelamento);
            }
        });

        return new AcessoTenantDbContext(construtorOpcoes.Options);
    }
}
