namespace Platform.IntegrationTests.Tenancy;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.DesignTime;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.IntegrationTests.Fixtures;
using Xunit;

public sealed class TestesMigracaoEIsolamentoTenant(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>
{
    [Fact]
    public async Task BancoVazio_RejeitaPreflightMigracaoSemAprovacao()
    {
        OpcoesPreflightMigracao opcoesNaoAprovadas = new(
            IdAplicacao: "app-teste",
            IdAmbiente: fixture.TenantA.IdAmbiente,
            IdTenant: fixture.TenantA.IdTenant,
            EstadoTenant: EstadoTenant.Inativo,
            StringConexao: fixture.TenantA.StringConexaoProprietario,
            PermissaoInicializarBancoVazioBruta: "false");

        InvalidOperationException excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PreflightMigracaoTenant.ExecutarPreflightAsync(opcoesNaoAprovadas, TestContext.Current.CancellationToken));

        Assert.Contains("não autorizada", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigracaoInicialEReaplicacao_SaoIdempotentesEPreservamDados()
    {
        // 1. Migração inicial no Tenant A e Tenant B com autorização
        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            OpcoesPreflightMigracao opcoesAprovadas = new(
                IdAplicacao: "app-teste",
                IdAmbiente: tenant.IdAmbiente,
                IdTenant: tenant.IdTenant,
                EstadoTenant: EstadoTenant.Inativo,
                StringConexao: tenant.StringConexaoProprietario,
                PermissaoInicializarBancoVazioBruta: "true",
                IdTenantCli: tenant.IdTenant,
                IdAmbienteCli: tenant.IdAmbiente);

            await PreflightMigracaoTenant.ExecutarPreflightAsync(opcoesAprovadas, TestContext.Current.CancellationToken);

            DbContextOptions<AcessoTenantDbContext> opcoes = CriarOpcoes(tenant.StringConexaoProprietario, tenant.IdTenant, tenant.IdAmbiente);
            await using (AcessoTenantDbContext contexto = new(opcoes))
            {
                await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }

            await tenant.ConcederPermissoesExecucaoAsync(TestContext.Current.CancellationToken);

            // Verifica se o marcador de identidade existe e corresponde
            await using (AcessoTenantDbContext contexto = new(opcoes))
            {
                var identidade = await contexto.IdentidadeBanco.SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(tenant.IdTenant, identidade.IdTenant);
                Assert.Equal(tenant.IdAmbiente, identidade.IdAmbiente);
                Assert.NotEqual(Guid.Empty, identidade.EpocaAcesso);
            }
        }

        // 2. Reaplicação com autorização de banco vazio desligada - deve suceder e preservar dados
        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            OpcoesPreflightMigracao reaplicar = new(
                IdAplicacao: "app-teste",
                IdAmbiente: tenant.IdAmbiente,
                IdTenant: tenant.IdTenant,
                EstadoTenant: EstadoTenant.Inativo,
                StringConexao: tenant.StringConexaoProprietario,
                PermissaoInicializarBancoVazioBruta: "false", // Desligada após inicialização!
                IdTenantCli: tenant.IdTenant,
                IdAmbienteCli: tenant.IdAmbiente);

            await PreflightMigracaoTenant.ExecutarPreflightAsync(reaplicar, TestContext.Current.CancellationToken);

            DbContextOptions<AcessoTenantDbContext> opcoes = CriarOpcoes(tenant.StringConexaoProprietario, tenant.IdTenant, tenant.IdAmbiente);
            await using (AcessoTenantDbContext contexto = new(opcoes))
            {
                await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }

            await tenant.ConcederPermissoesExecucaoAsync(TestContext.Current.CancellationToken);

            await using (AcessoTenantDbContext contexto = new(opcoes))
            {
                var identidade = await contexto.IdentidadeBanco.SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(tenant.IdTenant, identidade.IdTenant);
                Assert.Equal(tenant.IdAmbiente, identidade.IdAmbiente);
            }
        }
    }

    [Fact]
    public async Task Preflight_BloqueiaAntesDeDDLQuandoTenantConfiguradoDivergeDoBancoAlvo()
    {
        // Garante que o Tenant B esteja migrado e com seu marcador gravado
        OpcoesPreflightMigracao aprovadoB = new(
            IdAplicacao: "app-teste",
            IdAmbiente: fixture.TenantB.IdAmbiente,
            IdTenant: fixture.TenantB.IdTenant,
            EstadoTenant: EstadoTenant.Inativo,
            StringConexao: fixture.TenantB.StringConexaoProprietario,
            PermissaoInicializarBancoVazioBruta: "true",
            IdTenantCli: fixture.TenantB.IdTenant,
            IdAmbienteCli: fixture.TenantB.IdAmbiente);

        await PreflightMigracaoTenant.ExecutarPreflightAsync(aprovadoB, TestContext.Current.CancellationToken);

        DbContextOptions<AcessoTenantDbContext> opcoesB = CriarOpcoes(
            fixture.TenantB.StringConexaoProprietario, fixture.TenantB.IdTenant, fixture.TenantB.IdAmbiente);
        await using (AcessoTenantDbContext contextoB = new(opcoesB))
        {
            await contextoB.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        // Tenta rodar o preflight com a identidade do Tenant A apontando para a base do Tenant B
        OpcoesPreflightMigracao divergente = new(
            IdAplicacao: "app-teste",
            IdAmbiente: fixture.TenantA.IdAmbiente,
            IdTenant: fixture.TenantA.IdTenant,
            EstadoTenant: EstadoTenant.Inativo,
            StringConexao: fixture.TenantB.StringConexaoProprietario, // Base errada!
            PermissaoInicializarBancoVazioBruta: "true",
            IdTenantCli: fixture.TenantA.IdTenant,
            IdAmbienteCli: fixture.TenantA.IdAmbiente);

        await Assert.ThrowsAsync<ExcecaoIsolamentoTenant>(
            () => PreflightMigracaoTenant.ExecutarPreflightAsync(divergente, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Verificador_LeIdentidadeBancoEVerificaStatusMigracao()
    {
        // Garante migração do Tenant A
        OpcoesPreflightMigracao aprovadoA = new(
            IdAplicacao: "app-teste",
            IdAmbiente: fixture.TenantA.IdAmbiente,
            IdTenant: fixture.TenantA.IdTenant,
            EstadoTenant: EstadoTenant.Inativo,
            StringConexao: fixture.TenantA.StringConexaoProprietario,
            PermissaoInicializarBancoVazioBruta: "true",
            IdTenantCli: fixture.TenantA.IdTenant,
            IdAmbienteCli: fixture.TenantA.IdAmbiente);

        await PreflightMigracaoTenant.ExecutarPreflightAsync(aprovadoA, TestContext.Current.CancellationToken);

        DbContextOptions<AcessoTenantDbContext> opcoesProprietario = CriarOpcoes(
            fixture.TenantA.StringConexaoProprietario,
            fixture.TenantA.IdTenant,
            fixture.TenantA.IdAmbiente);

        await using (AcessoTenantDbContext contextoProprietario = new(opcoesProprietario))
        {
            await contextoProprietario.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await fixture.TenantA.ConcederPermissoesExecucaoAsync(TestContext.Current.CancellationToken);

        DbContextOptions<AcessoTenantDbContext> opcoesExecucao = CriarOpcoes(
            fixture.TenantA.StringConexaoExecucao,
            fixture.TenantA.IdTenant,
            fixture.TenantA.IdAmbiente);

        await using AcessoTenantDbContext contexto = new(opcoesExecucao);
        VerificadorIdentidadeBancoTenant verificador = new(contexto);

        var snapshot = await verificador.LerAsync(TestContext.Current.CancellationToken);
        Assert.Equal(fixture.TenantA.IdTenant, snapshot.IdTenant);
        Assert.Equal(fixture.TenantA.IdAmbiente, snapshot.IdAmbiente);
        Assert.NotEqual(Guid.Empty, snapshot.EpocaAcesso);
        Assert.True(snapshot.PossuiVersaoMigracaoEsperada);
    }

    private static DbContextOptions<AcessoTenantDbContext> CriarOpcoes(
        string stringConexao, Guid idTenant, Guid idAmbiente)
    {
        DbContextOptionsBuilder<AcessoTenantDbContext> construtor = new();
        construtor.UseNpgsql(stringConexao, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
        });

        construtor.UseSeeding((contexto, _) =>
        {
            if (contexto is AcessoTenantDbContext contextoTenant)
            {
                PreflightMigracaoTenant.PopularIdentidadeBanco(contextoTenant, idTenant, idAmbiente);
            }
        });

        construtor.UseAsyncSeeding(async (contexto, _, tokenCancelamento) =>
        {
            if (contexto is AcessoTenantDbContext contextoTenant)
            {
                await PreflightMigracaoTenant.PopularIdentidadeBancoAsync(contextoTenant, idTenant, idAmbiente, tokenCancelamento);
            }
        });

        return construtor.Options;
    }
}
