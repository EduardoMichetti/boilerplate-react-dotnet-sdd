namespace Platform.IntegrationTests.Tenancy;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.DesignTime;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.IntegrationTests.Fixtures;
using Xunit;

public sealed class TestesMigracaoEIsolamentoInquilino(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>
{
    [Fact]
    public async Task BancoVazio_RejeitaPreValidacaoMigracaoSemAprovacao()
    {
        OpcoesPreValidacaoMigracao opcoesNaoAprovadas = new(
            IdAplicacao: "app-teste",
            IdAmbiente: fixture.InquilinoA.IdAmbiente,
            IdInquilino: fixture.InquilinoA.IdInquilino,
            EstadoInquilino: EstadoInquilino.Inativo,
            StringConexao: fixture.InquilinoA.StringConexaoProprietario,
            PermissaoInicializarBancoVazioBruta: "false");

        InvalidOperationException excecao = await Assert.ThrowsAsync<InvalidOperationException>(
            () => PreValidacaoMigracaoInquilino.ExecutarPreValidacaoAsync(opcoesNaoAprovadas, TestContext.Current.CancellationToken));

        Assert.Contains("não autorizada", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MigracaoInicialEReaplicacao_SaoIdempotentesEPreservamDados()
    {
        // 1. Migração inicial no Inquilino A e Inquilino B com autorização
        foreach (InstalacaoTestePostgres inquilino in new[] { fixture.InquilinoA, fixture.InquilinoB })
        {
            OpcoesPreValidacaoMigracao opcoesAprovadas = new(
                IdAplicacao: "app-teste",
                IdAmbiente: inquilino.IdAmbiente,
                IdInquilino: inquilino.IdInquilino,
                EstadoInquilino: EstadoInquilino.Inativo,
                StringConexao: inquilino.StringConexaoProprietario,
                PermissaoInicializarBancoVazioBruta: "true",
                IdInquilinoCli: inquilino.IdInquilino,
                IdAmbienteCli: inquilino.IdAmbiente);

            await PreValidacaoMigracaoInquilino.ExecutarPreValidacaoAsync(opcoesAprovadas, TestContext.Current.CancellationToken);

            DbContextOptions<ContextoBancoAcessoInquilino> opcoes = CriarOpcoes(inquilino.StringConexaoProprietario, inquilino.IdInquilino, inquilino.IdAmbiente);
            await using (ContextoBancoAcessoInquilino contexto = new(opcoes))
            {
                await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }

            await inquilino.ConcederPermissoesExecucaoAsync(TestContext.Current.CancellationToken);

            // Verifica se o marcador de identidade existe e corresponde
            await using (ContextoBancoAcessoInquilino contexto = new(opcoes))
            {
                var identidade = await contexto.IdentidadeBanco.SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(inquilino.IdInquilino, identidade.IdInquilino);
                Assert.Equal(inquilino.IdAmbiente, identidade.IdAmbiente);
                Assert.NotEqual(Guid.Empty, identidade.EpocaAcesso);
            }
        }

        // 2. Reaplicação com autorização de banco vazio desligada - deve suceder e preservar dados
        foreach (InstalacaoTestePostgres inquilino in new[] { fixture.InquilinoA, fixture.InquilinoB })
        {
            OpcoesPreValidacaoMigracao reaplicar = new(
                IdAplicacao: "app-teste",
                IdAmbiente: inquilino.IdAmbiente,
                IdInquilino: inquilino.IdInquilino,
                EstadoInquilino: EstadoInquilino.Inativo,
                StringConexao: inquilino.StringConexaoProprietario,
                PermissaoInicializarBancoVazioBruta: "false", // Desligada após inicialização!
                IdInquilinoCli: inquilino.IdInquilino,
                IdAmbienteCli: inquilino.IdAmbiente);

            await PreValidacaoMigracaoInquilino.ExecutarPreValidacaoAsync(reaplicar, TestContext.Current.CancellationToken);

            DbContextOptions<ContextoBancoAcessoInquilino> opcoes = CriarOpcoes(inquilino.StringConexaoProprietario, inquilino.IdInquilino, inquilino.IdAmbiente);
            await using (ContextoBancoAcessoInquilino contexto = new(opcoes))
            {
                await contexto.Database.MigrateAsync(TestContext.Current.CancellationToken);
            }

            await inquilino.ConcederPermissoesExecucaoAsync(TestContext.Current.CancellationToken);

            await using (ContextoBancoAcessoInquilino contexto = new(opcoes))
            {
                var identidade = await contexto.IdentidadeBanco.SingleAsync(TestContext.Current.CancellationToken);
                Assert.Equal(inquilino.IdInquilino, identidade.IdInquilino);
                Assert.Equal(inquilino.IdAmbiente, identidade.IdAmbiente);
            }
        }
    }

    [Fact]
    public async Task PreValidacao_BloqueiaAntesDeDDLQuandoInquilinoConfiguradoDivergeDoBancoAlvo()
    {
        // Garante que o Inquilino B esteja migrado e com seu marcador gravado
        OpcoesPreValidacaoMigracao aprovadoB = new(
            IdAplicacao: "app-teste",
            IdAmbiente: fixture.InquilinoB.IdAmbiente,
            IdInquilino: fixture.InquilinoB.IdInquilino,
            EstadoInquilino: EstadoInquilino.Inativo,
            StringConexao: fixture.InquilinoB.StringConexaoProprietario,
            PermissaoInicializarBancoVazioBruta: "true",
            IdInquilinoCli: fixture.InquilinoB.IdInquilino,
            IdAmbienteCli: fixture.InquilinoB.IdAmbiente);

        await PreValidacaoMigracaoInquilino.ExecutarPreValidacaoAsync(aprovadoB, TestContext.Current.CancellationToken);

        DbContextOptions<ContextoBancoAcessoInquilino> opcoesB = CriarOpcoes(
            fixture.InquilinoB.StringConexaoProprietario, fixture.InquilinoB.IdInquilino, fixture.InquilinoB.IdAmbiente);
        await using (ContextoBancoAcessoInquilino contextoB = new(opcoesB))
        {
            await contextoB.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        // Tenta rodar a pré-validação com a identidade do Inquilino A apontando para a base do Inquilino B
        OpcoesPreValidacaoMigracao divergente = new(
            IdAplicacao: "app-teste",
            IdAmbiente: fixture.InquilinoA.IdAmbiente,
            IdInquilino: fixture.InquilinoA.IdInquilino,
            EstadoInquilino: EstadoInquilino.Inativo,
            StringConexao: fixture.InquilinoB.StringConexaoProprietario, // Base errada!
            PermissaoInicializarBancoVazioBruta: "true",
            IdInquilinoCli: fixture.InquilinoA.IdInquilino,
            IdAmbienteCli: fixture.InquilinoA.IdAmbiente);

        await Assert.ThrowsAsync<ExcecaoIsolamentoInquilino>(
            () => PreValidacaoMigracaoInquilino.ExecutarPreValidacaoAsync(divergente, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Sonda_LeIdentidadeBancoEVerificaStatusMigracao()
    {
        // Garante migração do Inquilino A
        OpcoesPreValidacaoMigracao aprovadoA = new(
            IdAplicacao: "app-teste",
            IdAmbiente: fixture.InquilinoA.IdAmbiente,
            IdInquilino: fixture.InquilinoA.IdInquilino,
            EstadoInquilino: EstadoInquilino.Inativo,
            StringConexao: fixture.InquilinoA.StringConexaoProprietario,
            PermissaoInicializarBancoVazioBruta: "true",
            IdInquilinoCli: fixture.InquilinoA.IdInquilino,
            IdAmbienteCli: fixture.InquilinoA.IdAmbiente);

        await PreValidacaoMigracaoInquilino.ExecutarPreValidacaoAsync(aprovadoA, TestContext.Current.CancellationToken);

        DbContextOptions<ContextoBancoAcessoInquilino> opcoesProprietario = CriarOpcoes(
            fixture.InquilinoA.StringConexaoProprietario,
            fixture.InquilinoA.IdInquilino,
            fixture.InquilinoA.IdAmbiente);

        await using (ContextoBancoAcessoInquilino contextoProprietario = new(opcoesProprietario))
        {
            await contextoProprietario.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        await fixture.InquilinoA.ConcederPermissoesExecucaoAsync(TestContext.Current.CancellationToken);

        DbContextOptions<ContextoBancoAcessoInquilino> opcoesExecucao = CriarOpcoes(
            fixture.InquilinoA.StringConexaoExecucao,
            fixture.InquilinoA.IdInquilino,
            fixture.InquilinoA.IdAmbiente);

        await using ContextoBancoAcessoInquilino contexto = new(opcoesExecucao);
        SondaIdentidadeBancoInquilino sonda = new(contexto);

        var snapshot = await sonda.LerAsync(TestContext.Current.CancellationToken);
        Assert.Equal(fixture.InquilinoA.IdInquilino, snapshot.IdInquilino);
        Assert.Equal(fixture.InquilinoA.IdAmbiente, snapshot.IdAmbiente);
        Assert.NotEqual(Guid.Empty, snapshot.EpocaAcesso);
        Assert.True(snapshot.PossuiVersaoMigracaoEsperada);
    }

    private static DbContextOptions<ContextoBancoAcessoInquilino> CriarOpcoes(
        string stringConexao, Guid idInquilino, Guid idAmbiente)
    {
        DbContextOptionsBuilder<ContextoBancoAcessoInquilino> construtor = new();
        construtor.UseNpgsql(stringConexao, npgsql =>
        {
            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation");
        });

        construtor.UseSeeding((contexto, _) =>
        {
            if (contexto is ContextoBancoAcessoInquilino contextoInquilino)
            {
                PreValidacaoMigracaoInquilino.PopularIdentidadeBanco(contextoInquilino, idInquilino, idAmbiente);
            }
        });

        construtor.UseAsyncSeeding(async (contexto, _, tokenCancelamento) =>
        {
            if (contexto is ContextoBancoAcessoInquilino contextoInquilino)
            {
                await PreValidacaoMigracaoInquilino.PopularIdentidadeBancoAsync(contextoInquilino, idInquilino, idAmbiente, tokenCancelamento);
            }
        });

        return construtor.Options;
    }
}
