using System.Security.Cryptography;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Platform.IntegrationTests.Fixtures;

/// <summary>
/// Fixture de teste que provisiona um contêiner real de PostgreSQL com dois bancos isolados (Inquilino A e Inquilino B).
/// </summary>
public sealed class FixtureInstalacaoPostgres : IAsyncLifetime
{
    public const string Imagem =
        "postgres:18.6@sha256:979bccfde35445db618c65107d27d08c6c21339d8dc3ad4c830c6633947be0e4";

    private readonly PostgreSqlContainer _conteiner = new PostgreSqlBuilder(Imagem)
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
        .Build();

    public FixtureInstalacaoPostgres()
    {
        InquilinoA = new InstalacaoTestePostgres(_conteiner, "a");
        InquilinoB = new InstalacaoTestePostgres(_conteiner, "b");
    }

    public InstalacaoTestePostgres InquilinoA { get; }
    public InstalacaoTestePostgres InquilinoB { get; }

    public async ValueTask InitializeAsync()
    {
        CancellationToken tokenCancelamento = TestContext.Current.CancellationToken;
        await _conteiner.StartAsync(tokenCancelamento);
        await using NpgsqlConnection admin = new(_conteiner.GetConnectionString());
        await admin.OpenAsync(tokenCancelamento);

        foreach (InstalacaoTestePostgres instalacao in new[] { InquilinoA, InquilinoB })
        {
            await instalacao.ProvisionarAsync(admin, tokenCancelamento);
        }

        await using NpgsqlCommand restringirBancoAdmin = new("REVOKE ALL ON DATABASE postgres FROM PUBLIC", admin);
        await restringirBancoAdmin.ExecuteNonQueryAsync(tokenCancelamento);
    }

    public ValueTask DisposeAsync() => _conteiner.DisposeAsync();
}

/// <summary>
/// Representa a infraestrutura de banco de dados e credenciais provisionada para um inquilino de teste.
/// </summary>
public sealed class InstalacaoTestePostgres
{
    private readonly PostgreSqlContainer _conteiner;
    private readonly string _senhaProprietario = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    private readonly string _senhaExecucao = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    internal InstalacaoTestePostgres(PostgreSqlContainer conteiner, string rotulo)
    {
        _conteiner = conteiner;
        string sufixo = Guid.NewGuid().ToString("N");
        NomeBancoDados = $"instalacao_{rotulo}_{sufixo}";
        PapelProprietario = $"proprietario_{rotulo}_{sufixo}";
        PapelExecucao = $"execucao_{rotulo}_{sufixo}";
    }

    public Guid IdInquilino { get; } = Guid.NewGuid();
    public Guid IdAmbiente { get; } = Guid.NewGuid();
    public string NomeBancoDados { get; }
    public string PapelProprietario { get; }
    public string PapelExecucao { get; }
    public string StringConexaoProprietario => ConstruirStringConexao(PapelProprietario, _senhaProprietario);
    public string StringConexaoExecucao => ConstruirStringConexao(PapelExecucao, _senhaExecucao);

    internal async Task ProvisionarAsync(NpgsqlConnection admin, CancellationToken tokenCancelamento)
    {
        using NpgsqlCommandBuilder escapador = new();
        string proprietario = escapador.QuoteIdentifier(PapelProprietario);
        string execucao = escapador.QuoteIdentifier(PapelExecucao);
        string banco = escapador.QuoteIdentifier(NomeBancoDados);

        await using (NpgsqlCommand papeis = new(
            $"CREATE ROLE {proprietario} LOGIN PASSWORD '{_senhaProprietario}' " +
            "NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS; " +
            $"CREATE ROLE {execucao} LOGIN PASSWORD '{_senhaExecucao}' " +
            "NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS", admin))
        {
            await papeis.ExecuteNonQueryAsync(tokenCancelamento);
        }

        await using (NpgsqlCommand criarBanco = new($"CREATE DATABASE {banco} OWNER {proprietario}", admin))
        {
            await criarBanco.ExecuteNonQueryAsync(tokenCancelamento);
        }

        await using (NpgsqlCommand concessoes = new(
            $"REVOKE ALL ON DATABASE {banco} FROM PUBLIC; GRANT CONNECT ON DATABASE {banco} TO {execucao}", admin))
        {
            await concessoes.ExecuteNonQueryAsync(tokenCancelamento);
        }

        await using NpgsqlConnection conexaoProprietario = new(StringConexaoProprietario);
        await conexaoProprietario.OpenAsync(tokenCancelamento);
        await using NpgsqlCommand configurarSchemas = new(
            $"REVOKE ALL ON SCHEMA public FROM PUBLIC; " +
            $"CREATE SCHEMA IF NOT EXISTS foundation; " +
            $"CREATE SCHEMA IF NOT EXISTS access; " +
            $"GRANT USAGE ON SCHEMA foundation, access TO {execucao}; " +
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA foundation GRANT SELECT ON TABLES TO {execucao}; " +
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA access GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO {execucao}; " +
            $"ALTER DEFAULT PRIVILEGES IN SCHEMA access GRANT USAGE, SELECT ON SEQUENCES TO {execucao}", conexaoProprietario);
        await configurarSchemas.ExecuteNonQueryAsync(tokenCancelamento);
    }

    public async Task ConcederPermissoesExecucaoAsync(CancellationToken tokenCancelamento = default)
    {
        using NpgsqlCommandBuilder escapador = new();
        string execucao = escapador.QuoteIdentifier(PapelExecucao);

        await using NpgsqlConnection conexaoProprietario = new(StringConexaoProprietario);
        await conexaoProprietario.OpenAsync(tokenCancelamento);
        await using NpgsqlCommand comandoConcessao = new(
            $"GRANT USAGE ON SCHEMA foundation, access TO {execucao}; " +
            $"GRANT SELECT ON ALL TABLES IN SCHEMA foundation TO {execucao}; " +
            $"GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA access TO {execucao}; " +
            $"GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA access TO {execucao}", conexaoProprietario);
        await comandoConcessao.ExecuteNonQueryAsync(tokenCancelamento);
    }

    private string ConstruirStringConexao(string papel, string senha)
    {
        return new NpgsqlConnectionStringBuilder(_conteiner.GetConnectionString())
        {
            Database = NomeBancoDados,
            Username = papel,
            Password = senha,
            Pooling = false,
            IncludeErrorDetail = false,
            LogParameters = false,
            PersistSecurityInfo = false
        }.ConnectionString;
    }
}
