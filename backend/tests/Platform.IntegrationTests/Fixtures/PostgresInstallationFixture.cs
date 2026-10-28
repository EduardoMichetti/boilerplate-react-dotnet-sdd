using System.Security.Cryptography;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Platform.IntegrationTests.Fixtures;

public sealed class PostgresInstallationFixture : IAsyncLifetime
{
    public const string Image =
        "postgres:18.6@sha256:979bccfde35445db618c65107d27d08c6c21339d8dc3ad4c830c6633947be0e4";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
        .Build();

    public PostgresInstallationFixture()
    {
        ClientA = new PostgresTestInstallation(_container, "a");
        ClientB = new PostgresTestInstallation(_container, "b");
    }

    public PostgresTestInstallation ClientA { get; }
    public PostgresTestInstallation ClientB { get; }

    public async ValueTask InitializeAsync()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await _container.StartAsync(cancellationToken);
        await using NpgsqlConnection admin = new(_container.GetConnectionString());
        await admin.OpenAsync(cancellationToken);

        foreach (PostgresTestInstallation installation in new[] { ClientA, ClientB })
        {
            await installation.ProvisionAsync(admin, cancellationToken);
        }

        await using NpgsqlCommand restrictAdminDatabase = new("REVOKE ALL ON DATABASE postgres FROM PUBLIC", admin);
        await restrictAdminDatabase.ExecuteNonQueryAsync(cancellationToken);
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}

public sealed class PostgresTestInstallation
{
    private readonly PostgreSqlContainer _container;
    private readonly string _ownerPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly string _runtimePassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    internal PostgresTestInstallation(PostgreSqlContainer container, string label)
    {
        _container = container;
        string suffix = Guid.NewGuid().ToString("N");
        DatabaseName = $"installation_{label}_{suffix}";
        OwnerRole = $"owner_{label}_{suffix}";
        RuntimeRole = $"runtime_{label}_{suffix}";
    }

    public Guid TenantId { get; } = Guid.NewGuid();
    public Guid EnvironmentId { get; } = Guid.NewGuid();
    public string DatabaseName { get; }
    public string OwnerRole { get; }
    public string RuntimeRole { get; }
    public string OwnerConnectionString => BuildConnectionString(OwnerRole, _ownerPassword);
    public string RuntimeConnectionString => BuildConnectionString(RuntimeRole, _runtimePassword);

    internal async Task ProvisionAsync(NpgsqlConnection admin, CancellationToken cancellationToken)
    {
        using NpgsqlCommandBuilder quoting = new();
        string owner = quoting.QuoteIdentifier(OwnerRole);
        string runtime = quoting.QuoteIdentifier(RuntimeRole);
        string database = quoting.QuoteIdentifier(DatabaseName);

        // Passwords are generated hexadecimal values, never user input or logged command parameters.
        await using (NpgsqlCommand roles = new(
            $"CREATE ROLE {owner} LOGIN PASSWORD '{_ownerPassword}' " +
            "NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; " +
            $"CREATE ROLE {runtime} LOGIN PASSWORD '{_runtimePassword}' " +
            "NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS", admin))
        {
            await roles.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (NpgsqlCommand createDatabase = new($"CREATE DATABASE {database} OWNER {owner}", admin))
        {
            await createDatabase.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (NpgsqlCommand grants = new(
            $"REVOKE ALL ON DATABASE {database} FROM PUBLIC; GRANT CONNECT ON DATABASE {database} TO {runtime}", admin))
        {
            await grants.ExecuteNonQueryAsync(cancellationToken);
        }

        await using NpgsqlConnection ownerConnection = new(OwnerConnectionString);
        await ownerConnection.OpenAsync(cancellationToken);
        await using NpgsqlCommand restrictSchema = new("REVOKE ALL ON SCHEMA public FROM PUBLIC", ownerConnection);
        await restrictSchema.ExecuteNonQueryAsync(cancellationToken);
    }

    private string BuildConnectionString(string role, string password)
    {
        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Database = DatabaseName,
            Username = role,
            Password = password,
            Pooling = false,
            IncludeErrorDetail = false,
            LogParameters = false,
            PersistSecurityInfo = false
        }.ConnectionString;
    }
}
