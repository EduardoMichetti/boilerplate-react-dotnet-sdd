using Npgsql;
using Xunit;

namespace Platform.IntegrationTests.Fixtures;

/// <summary>
/// Confere que a fixture reproduz as identidades técnicas do <c>init-db.sh</c> (G6) com menor privilégio.
/// </summary>
public sealed class TestesFixtureInstalacaoPostgres(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>
{
    [Fact]
    public async Task ProprietarioERuntimePossuemApenasPrivilegiosEsperados()
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
    public async Task OperadoresSaoLoginsIndividuaisMembrosDoGrupoNologin()
    {
        foreach (InstalacaoTestePostgres instalacao in new[] { fixture.TenantA, fixture.TenantB })
        {
            foreach (string conexaoOperador in new[] { instalacao.StringConexaoOperadorUm, instalacao.StringConexaoOperadorDois })
            {
                await using NpgsqlConnection conexao = new(conexaoOperador);
                await conexao.OpenAsync(TestContext.Current.CancellationToken);
                await using NpgsqlCommand comando = new(
                    "SELECT session_user = current_user " +
                    "AND pg_has_role(current_user, @grupo, 'MEMBER') " +
                    "AND NOT (SELECT rolcanlogin FROM pg_roles WHERE rolname = @grupo) " +
                    "AND NOT (SELECT rolsuper OR rolcreatedb OR rolcreaterole FROM pg_roles WHERE rolname = current_user)", conexao);
                comando.Parameters.AddWithValue("grupo", instalacao.PapelOperador);
                Assert.Equal(true, await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
            }

            Assert.NotEqual(instalacao.LoginOperadorUm, instalacao.LoginOperadorDois);
        }
    }

    [Fact]
    public async Task CredenciaisNaoConectamNaOutraInstalacao()
    {
        foreach ((InstalacaoTestePostgres origem, InstalacaoTestePostgres destino) in new[]
        {
            (fixture.TenantA, fixture.TenantB),
            (fixture.TenantB, fixture.TenantA)
        })
        {
            foreach (string conexaoOrigem in new[] { origem.StringConexaoExecucao, origem.StringConexaoOperadorUm })
            {
                NpgsqlConnectionStringBuilder construtorConexao = new(conexaoOrigem) { Database = destino.NomeBancoDados };
                await using NpgsqlConnection conexao = new(construtorConexao.ConnectionString);

                PostgresException negado = await Assert.ThrowsAsync<PostgresException>(
                    () => conexao.OpenAsync(TestContext.Current.CancellationToken));

                Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, negado.SqlState);
            }
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
