namespace Platform.IntegrationTests.Tenancy;

using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Application.Tenancy;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.DesignTime;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Suporte;
using Xunit;

/// <summary>
/// T077: migration inicial única em bancos A/B novos, reaplicação, histórico desconhecido
/// rejeitado pelo tooling e pelo runtime, alvo errado bloqueado antes de DDL e roles ausentes.
/// </summary>
public sealed class TestesMigracaoEIsolamentoTenant(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>
{
    private const string IdMigracaoFutura = "29991231000000_MigracaoDeVersaoFutura";

    [Fact]
    public async Task BancoVazio_RejeitaPreflightSemAprovacao()
    {
        (string conexaoOwner, _) = await CriarBancoAvulsoAsync(comRolesDaInstalacao: true);

        OpcoesPreflightMigracao naoAprovado = Opcoes(conexaoOwner, Guid.NewGuid(), Guid.NewGuid(), "false");

        InvalidOperationException excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PreflightMigracaoTenant.ExecutarPreflightAsync(naoAprovado, AcessoTenantDbContextFactory.CadeiaConhecida(), TestContext.Current.CancellationToken));
        Assert.Contains("não autorizada", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Migracao_FalhaComMensagemClaraQuandoRolesDaInstalacaoNaoExistem()
    {
        (string conexaoOwner, string banco) = await CriarBancoAvulsoAsync(comRolesDaInstalacao: false);
        Guid idTenant = Guid.NewGuid();

        await using AcessoTenantDbContext contexto = new(BancoTeste.CriarOpcoesMigracao(conexaoOwner, idTenant, Guid.NewGuid(), TimeProvider.System));
        PostgresException excecao = await Assert.ThrowsAsync<PostgresException>(() => contexto.Database.MigrateAsync(TestContext.Current.CancellationToken));

        Assert.Contains($"{banco}_runtime", excecao.MessageText, StringComparison.Ordinal);
        await using AcessoTenantDbContext verificacao = new(BancoTeste.CriarOpcoesMigracao(conexaoOwner, idTenant, Guid.NewGuid(), TimeProvider.System));
        Assert.Empty(await verificacao.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MigracaoInicialEReaplicacao_SaoIdempotentesEPreservamDados()
    {
        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            await BancoTeste.MigrarAsync(tenant, tokenCancelamento: TestContext.Current.CancellationToken);
        }

        Dictionary<Guid, Guid> epocas = [];
        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
            IdentidadeBancoTenant marcador = await contexto.IdentidadeBanco.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(tenant.IdTenant, marcador.IdTenant);
            Assert.Equal(tenant.IdAmbiente, marcador.IdAmbiente);
            Assert.Equal(7, marcador.EpocaAcesso.Version);
            Assert.Equal(TimeSpan.Zero, marcador.InicializadoEm.Offset);
            Assert.Equal(
                AcessoTenantDbContextFactory.CadeiaConhecida(),
                (await contexto.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).ToList());
            epocas[tenant.IdTenant] = marcador.EpocaAcesso;
        }

        // Reaplicação com aprovação de banco vazio desligada: no-op sem duplicar nem perder dados.
        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            OpcoesPreflightMigracao reaplicar = Opcoes(tenant.StringConexaoProprietario, tenant.IdTenant, tenant.IdAmbiente, "false");
            await PreflightMigracaoTenant.ExecutarPreflightAsync(reaplicar, AcessoTenantDbContextFactory.CadeiaConhecida(), TestContext.Current.CancellationToken);
            await BancoTeste.MigrarAsync(tenant, tokenCancelamento: TestContext.Current.CancellationToken);

            await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
            IdentidadeBancoTenant marcador = await contexto.IdentidadeBanco.SingleAsync(TestContext.Current.CancellationToken);
            Assert.Equal(epocas[tenant.IdTenant], marcador.EpocaAcesso);
        }
    }

    [Fact]
    public async Task HistoricoComIdDesconhecido_BloqueiaToolingERuntimeMesmoSemPendentes()
    {
        await BancoTeste.MigrarAsync(fixture.TenantA, tokenCancelamento: TestContext.Current.CancellationToken);
        await ExecutarOwnerAsync(
            fixture.TenantA,
            $"INSERT INTO foundation.\"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ('{IdMigracaoFutura}', '10.0.12')");
        try
        {
            OpcoesPreflightMigracao opcoes = Opcoes(fixture.TenantA.StringConexaoProprietario, fixture.TenantA.IdTenant, fixture.TenantA.IdAmbiente, "false");
            InvalidOperationException excecao = await Assert.ThrowsAsync<InvalidOperationException>(
                () => PreflightMigracaoTenant.ExecutarPreflightAsync(opcoes, AcessoTenantDbContextFactory.CadeiaConhecida(), TestContext.Current.CancellationToken));
            Assert.Contains("desconhecidos", excecao.Message, StringComparison.Ordinal);

            await using AcessoTenantDbContext contextoRuntime = BancoTeste.CriarContexto(fixture.TenantA.StringConexaoExecucao);
            Assert.Empty(await contextoRuntime.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
            IdentidadeBancoTenantSnapshot snapshot = await new VerificadorIdentidadeBancoTenant(contextoRuntime).LerAsync(TestContext.Current.CancellationToken);
            Assert.False(snapshot.PossuiVersaoMigracaoEsperada);
        }
        finally
        {
            await ExecutarOwnerAsync(
                fixture.TenantA,
                $"DELETE FROM foundation.\"__EFMigrationsHistory\" WHERE \"MigrationId\" = '{IdMigracaoFutura}'");
        }
    }

    [Fact]
    public async Task Preflight_BloqueiaAntesDeDDLQuandoTenantConfiguradoDivergeDoBancoAlvo()
    {
        await BancoTeste.MigrarAsync(fixture.TenantB, tokenCancelamento: TestContext.Current.CancellationToken);

        OpcoesPreflightMigracao divergente = Opcoes(
            fixture.TenantB.StringConexaoProprietario, fixture.TenantA.IdTenant, fixture.TenantA.IdAmbiente, "true");

        await Assert.ThrowsAsync<ExcecaoIsolamentoTenant>(
            () => PreflightMigracaoTenant.ExecutarPreflightAsync(divergente, AcessoTenantDbContextFactory.CadeiaConhecida(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Verificador_RuntimeLeIdentidadeECadeiaCompleta()
    {
        await BancoTeste.MigrarAsync(fixture.TenantA, tokenCancelamento: TestContext.Current.CancellationToken);

        await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(fixture.TenantA.StringConexaoExecucao);
        IdentidadeBancoTenantSnapshot snapshot = await new VerificadorIdentidadeBancoTenant(contexto).LerAsync(TestContext.Current.CancellationToken);

        Assert.Equal(fixture.TenantA.IdTenant, snapshot.IdTenant);
        Assert.Equal(fixture.TenantA.IdAmbiente, snapshot.IdAmbiente);
        Assert.NotEqual(Guid.Empty, snapshot.EpocaAcesso);
        Assert.True(snapshot.PossuiVersaoMigracaoEsperada);
    }

    private static OpcoesPreflightMigracao Opcoes(string conexao, Guid idTenant, Guid idAmbiente, string permitirVazio) => new(
        CodigoAplicacao: "app-teste",
        IdAmbiente: idAmbiente,
        IdTenant: idTenant,
        EstadoTenant: EstadoTenant.Inativo,
        StringConexao: conexao,
        PermissaoInicializarBancoVazioBruta: permitirVazio,
        IdTenantCli: idTenant,
        IdAmbienteCli: idAmbiente);

    private static async Task ExecutarOwnerAsync(InstalacaoTestePostgres tenant, string sql)
    {
        await using NpgsqlConnection conexao = new(tenant.StringConexaoProprietario);
        await conexao.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Cria um banco avulso vazio com owner próprio; opcionalmente também as roles runtime/operador.
    /// </summary>
    private async Task<(string ConexaoOwner, string Banco)> CriarBancoAvulsoAsync(bool comRolesDaInstalacao)
    {
        string banco = $"avulso_{Guid.NewGuid():N}";
        string senha = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await using NpgsqlConnection admin = new(fixture.StringConexaoAdministrador);
        await admin.OpenAsync(TestContext.Current.CancellationToken);

        string sql = $"CREATE ROLE {banco}_owner LOGIN PASSWORD '{senha}' NOSUPERUSER NOCREATEDB NOCREATEROLE; " +
            (comRolesDaInstalacao ? $"CREATE ROLE {banco}_runtime NOLOGIN; CREATE ROLE {banco}_operador NOLOGIN; " : string.Empty);
        await using (NpgsqlCommand roles = new(sql, admin))
        {
            await roles.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using (NpgsqlCommand criar = new($"CREATE DATABASE {banco} OWNER {banco}_owner", admin))
        {
            await criar.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        string conexaoOwner = new NpgsqlConnectionStringBuilder(fixture.StringConexaoAdministrador)
        {
            Database = banco,
            Username = $"{banco}_owner",
            Password = senha,
            Pooling = false,
        }.ConnectionString;
        return (conexaoOwner, banco);
    }
}
