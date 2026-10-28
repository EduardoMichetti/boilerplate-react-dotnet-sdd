using Npgsql;
using Xunit;

namespace Platform.IntegrationTests.Fixtures;

public sealed class PostgresInstallationFixtureTests(PostgresInstallationFixture fixture)
    : IClassFixture<PostgresInstallationFixture>
{
    [Fact]
    public async Task OwnerAndRuntimeHaveOnlyTheirExpectedPrivileges()
    {
        foreach (PostgresTestInstallation installation in new[] { fixture.ClientA, fixture.ClientB })
        {
            await using NpgsqlConnection owner = new(installation.OwnerConnectionString);
            await owner.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand ownerCheck = new(
                "SELECT NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls) " +
                "AND current_user = pg_get_userbyid(datdba) " +
                "FROM pg_roles CROSS JOIN pg_database WHERE rolname = current_user AND datname = current_database()", owner);
            Assert.Equal(true, await ownerCheck.ExecuteScalarAsync(TestContext.Current.CancellationToken));

            await using NpgsqlConnection runtime = new(installation.RuntimeConnectionString);
            await runtime.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand runtimeCheck = new(
                "SELECT NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls) " +
                "AND NOT has_database_privilege(current_user, current_database(), 'CREATE') " +
                "AND NOT has_database_privilege(current_user, current_database(), 'TEMPORARY') " +
                "FROM pg_roles WHERE rolname = current_user", runtime);
            Assert.Equal(true, await runtimeCheck.ExecuteScalarAsync(TestContext.Current.CancellationToken));

            await using NpgsqlCommand ddl = new("CREATE TABLE public.forbidden_runtime_ddl (id integer)", runtime);
            PostgresException denied = await Assert.ThrowsAsync<PostgresException>(
                () => ddl.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    [Fact]
    public async Task RuntimeCredentialsCannotConnectToTheOtherInstallation()
    {
        foreach ((PostgresTestInstallation origin, PostgresTestInstallation target) in new[]
        {
            (fixture.ClientA, fixture.ClientB),
            (fixture.ClientB, fixture.ClientA)
        })
        {
            NpgsqlConnectionStringBuilder connectionString = new(origin.RuntimeConnectionString)
            {
                Database = target.DatabaseName
            };
            await using NpgsqlConnection connection = new(connectionString.ConnectionString);

            PostgresException denied = await Assert.ThrowsAsync<PostgresException>(
                () => connection.OpenAsync(TestContext.Current.CancellationToken));

            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    [Fact]
    public async Task ProvisionedDatabasesRemainEmptyBeforeApplicationMigrations()
    {
        Assert.NotEqual(fixture.ClientA.DatabaseName, fixture.ClientB.DatabaseName);
        Assert.NotEqual(fixture.ClientA.TenantId, fixture.ClientB.TenantId);

        foreach (PostgresTestInstallation installation in new[] { fixture.ClientA, fixture.ClientB })
        {
            await using NpgsqlConnection connection = new(installation.OwnerConnectionString);
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using NpgsqlCommand command = new(
                "SELECT count(*) FROM information_schema.tables " +
                "WHERE table_schema NOT IN ('pg_catalog', 'information_schema')", connection);

            Assert.Equal(0L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        }
    }
}
