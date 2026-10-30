namespace Platform.Infrastructure.Persistence.DesignTime;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess;

/// <summary>
/// Fábrica de contexto em tempo de design (EF Core CLI) para instanciar o ContextoBancoAcessoInquilino.
/// </summary>
public sealed class FabricaContextoBancoAcessoInquilino : IDesignTimeDbContextFactory<ContextoBancoAcessoInquilino>
{
    public ContextoBancoAcessoInquilino CreateDbContext(string[] argumentos)
    {
        Guid? idInquilinoCli = null;
        Guid? idAmbienteCli = null;

        for (int i = 0; i < argumentos.Length; i++)
        {
            if ((string.Equals(argumentos[i], "--tenant-id", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(argumentos[i], "--id-inquilino", StringComparison.OrdinalIgnoreCase)) && i + 1 < argumentos.Length)
            {
                if (Guid.TryParse(argumentos[i + 1], out Guid tId))
                {
                    idInquilinoCli = tId;
                }
            }
            else if ((string.Equals(argumentos[i], "--environment-id", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(argumentos[i], "--id-ambiente", StringComparison.OrdinalIgnoreCase)) && i + 1 < argumentos.Length)
            {
                if (Guid.TryParse(argumentos[i + 1], out Guid eId))
                {
                    idAmbienteCli = eId;
                }
            }
        }

        string? idAplicacao = Environment.GetEnvironmentVariable("IdAplicacao")
            ?? Environment.GetEnvironmentVariable("ApplicationId")
            ?? "boilerplate-react-dotnet-sdd";

        string? idAmbienteBruto = Environment.GetEnvironmentVariable("IdAmbiente")
            ?? Environment.GetEnvironmentVariable("EnvironmentId");
        Guid idAmbiente = Guid.TryParse(idAmbienteBruto, out Guid eGuid)
            ? eGuid
            : idAmbienteCli ?? Guid.Parse("11111111-1111-4111-8111-111111111111");

        string? idInquilinoBruto = Environment.GetEnvironmentVariable("IdInquilino")
            ?? Environment.GetEnvironmentVariable("TenantId");
        Guid idInquilino = Guid.TryParse(idInquilinoBruto, out Guid tGuid)
            ? tGuid
            : idInquilinoCli ?? Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

        string? estadoBruto = Environment.GetEnvironmentVariable("EstadoInquilino")
            ?? Environment.GetEnvironmentVariable("TenantState");
        EstadoInquilino estadoInquilino = Enum.TryParse<EstadoInquilino>(estadoBruto, true, out EstadoInquilino estadoConvertido)
            ? estadoConvertido
            : EstadoInquilino.Inativo;

        string? permitirInicializarBruto = Environment.GetEnvironmentVariable("Migracoes__PermitirInicializarBancoVazio")
            ?? Environment.GetEnvironmentVariable("Migrations__AllowInitializeEmptyDatabase")
            ?? Environment.GetEnvironmentVariable("Migracoes:PermitirInicializarBancoVazio")
            ?? Environment.GetEnvironmentVariable("Migrations:AllowInitializeEmptyDatabase");

        string? stringConexao = Environment.GetEnvironmentVariable("StringsConexao__BancoApp")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__AppDb")
            ?? Environment.GetEnvironmentVariable("StringsConexao:BancoApp")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings:AppDb");

        bool ehFallbackTempoDesign = string.IsNullOrWhiteSpace(stringConexao);
        string stringConexaoEfetiva = ehFallbackTempoDesign
            ? "Host=localhost;Database=boilerplate_designtime;Username=postgres;******"
            : stringConexao!;

        if (!ehFallbackTempoDesign)
        {
            OpcoesPreValidacaoMigracao opcoesPreValidacao = new(
                IdAplicacao: idAplicacao,
                IdAmbiente: idAmbiente,
                IdInquilino: idInquilino,
                EstadoInquilino: estadoInquilino,
                StringConexao: stringConexaoEfetiva,
                PermissaoInicializarBancoVazioBruta: permitirInicializarBruto,
                IdInquilinoCli: idInquilinoCli,
                IdAmbienteCli: idAmbienteCli);

            PreValidacaoMigracaoInquilino.ExecutarPreValidacaoAsync(opcoesPreValidacao).GetAwaiter().GetResult();
        }

        DbContextOptionsBuilder<ContextoBancoAcessoInquilino> construtorOpcoes = new();
        construtorOpcoes.UseNpgsql(stringConexaoEfetiva, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
        });

        construtorOpcoes.UseSeeding((contexto, _) =>
        {
            if (contexto is ContextoBancoAcessoInquilino contextoInquilino)
            {
                PreValidacaoMigracaoInquilino.PopularIdentidadeBanco(contextoInquilino, idInquilino, idAmbiente);
            }
        });

        construtorOpcoes.UseAsyncSeeding(async (contexto, _, tokenCancelamento) =>
        {
            if (contexto is ContextoBancoAcessoInquilino contextoInquilino)
            {
                await PreValidacaoMigracaoInquilino.PopularIdentidadeBancoAsync(contextoInquilino, idInquilino, idAmbiente, tokenCancelamento);
            }
        });

        return new ContextoBancoAcessoInquilino(construtorOpcoes.Options);
    }
}
