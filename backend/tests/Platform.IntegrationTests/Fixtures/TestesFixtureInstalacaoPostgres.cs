using Npgsql;
using Xunit;

namespace Platform.IntegrationTests.Fixtures;

public sealed class TestesFixtureInstalacaoPostgres(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>
{
    [Fact]
    public async Task ProprietarioEExecucaoPossuemApenasPrivilegiosEsperados()
    {
        foreach (InstalacaoTestePostgres instalacao in new[] { fixture.TenantA, fixture.TenantB })
        {
            await using NpgsqlConnection conexaoProprietario = new(instalacao.StringConexaoProprietario);
            await conexaoProprietario.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand checagemProprietario = new(
                "SELECT NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls) " +
                "AND current_user = pg_get_userbyid(datdba) " +
                "FROM pg_roles CROSS JOIN pg_database WHERE rolname = current_user AND datname = current_database()", conexaoProprietario);
            Assert.Equal(true, await checagemProprietario.ExecuteScalarAsync(TestContext.Current.CancellationToken));

            await using NpgsqlConnection conexaoExecucao = new(instalacao.StringConexaoExecucao);
            await conexaoExecucao.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand checagemExecucao = new(
                "SELECT NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls) " +
                "AND NOT has_database_privilege(current_user, current_database(), 'CREATE') " +
                "AND NOT has_database_privilege(current_user, current_database(), 'TEMPORARY') " +
                "FROM pg_roles WHERE rolname = current_user", conexaoExecucao);
            Assert.Equal(true, await checagemExecucao.ExecuteScalarAsync(TestContext.Current.CancellationToken));

            await using NpgsqlCommand ddlProibido = new("CREATE TABLE public.ddl_execucao_proibido (id integer)", conexaoExecucao);
            PostgresException negado = await Assert.ThrowsAsync<PostgresException>(
                () => ddlProibido.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, negado.SqlState);
        }
    }

    [Fact]
    public async Task CredenciaisExecucaoNaoConectamNaOutraInstalacao()
    {
        foreach ((InstalacaoTestePostgres origem, InstalacaoTestePostgres destino) in new[]
        {
            (fixture.TenantA, fixture.TenantB),
            (fixture.TenantB, fixture.TenantA)
        })
        {
            NpgsqlConnectionStringBuilder construtorConexao = new(origem.StringConexaoExecucao)
            {
                Database = destino.NomeBancoDados
            };
            await using NpgsqlConnection conexao = new(construtorConexao.ConnectionString);

            PostgresException negado = await Assert.ThrowsAsync<PostgresException>(
                () => conexao.OpenAsync(TestContext.Current.CancellationToken));

            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, negado.SqlState);
        }
    }

    [Fact]
    public async Task BancosProvisionadosPermanecemVaziosAntesDasMigrations()
    {
        Assert.NotEqual(fixture.TenantA.NomeBancoDados, fixture.TenantB.NomeBancoDados);
        Assert.NotEqual(fixture.TenantA.IdTenant, fixture.TenantB.IdTenant);

        foreach (InstalacaoTestePostgres instalacao in new[] { fixture.TenantA, fixture.TenantB })
        {
            await using NpgsqlConnection conexao = new(instalacao.StringConexaoProprietario);
            await conexao.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand comando = new(
                "SELECT count(*) FROM information_schema.tables " +
                "WHERE table_schema NOT IN ('pg_catalog', 'information_schema')", conexao);

            Assert.Equal(0L, await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
    }
}
