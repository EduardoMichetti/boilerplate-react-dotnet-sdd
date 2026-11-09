using System.Security.Cryptography;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Platform.IntegrationTests.Fixtures;

/// <summary>
/// Provisiona um contêiner PostgreSQL real com dois bancos isolados (instalações A e B), com as
/// mesmas identidades técnicas do <c>init-db.sh</c> (G6): <c>&lt;banco&gt;_owner</c>,
/// <c>&lt;banco&gt;_runtime</c>, grupo <c>&lt;banco&gt;_operador</c> (NOLOGIN) e dois logins
/// individuais de operador. GRANTs por tabela vêm da migration, não da fixture.
/// </summary>
public sealed class FixtureInstalacaoPostgres : IAsyncLifetime
{
    /// <summary>
    /// Imagem fixada por digest.
    /// </summary>
    public const string Imagem =
        "postgres:18.6@sha256:979bccfde35445db618c65107d27d08c6c21339d8dc3ad4c830c6633947be0e4";

    private readonly PostgreSqlContainer _conteiner = new PostgreSqlBuilder(Imagem)
        .WithDatabase("postgres")
        .WithUsername("postgres")
        .WithPassword(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)))
        .Build();

    /// <summary>
    /// Cria a fixture com as duas instalações ainda não provisionadas.
    /// </summary>
    public FixtureInstalacaoPostgres()
    {
        TenantA = new InstalacaoTestePostgres(_conteiner, "a");
        TenantB = new InstalacaoTestePostgres(_conteiner, "b");
    }

    /// <summary>
    /// Instalação A.
    /// </summary>
    public InstalacaoTestePostgres TenantA { get; }

    /// <summary>
    /// Instalação B.
    /// </summary>
    public InstalacaoTestePostgres TenantB { get; }

    /// <summary>
    /// String de conexão do superusuário do contêiner (somente para preparar cenários de teste).
    /// </summary>
    public string StringConexaoAdministrador => _conteiner.GetConnectionString();

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        CancellationToken tokenCancelamento = TestContext.Current.CancellationToken;
        await _conteiner.StartAsync(tokenCancelamento);
        await using NpgsqlConnection admin = new(_conteiner.GetConnectionString());
        await admin.OpenAsync(tokenCancelamento);

        foreach (InstalacaoTestePostgres instalacao in new[] { TenantA, TenantB })
        {
            await instalacao.ProvisionarAsync(admin, tokenCancelamento);
        }

        await using NpgsqlCommand restringirBancoAdmin = new("REVOKE ALL ON DATABASE postgres FROM PUBLIC", admin);
        await restringirBancoAdmin.ExecuteNonQueryAsync(tokenCancelamento);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _conteiner.DisposeAsync();
}

/// <summary>
/// Banco e identidades técnicas de uma instalação de teste, com senhas aleatórias por execução.
/// </summary>
public sealed class InstalacaoTestePostgres
{
    private readonly PostgreSqlContainer _conteiner;
    private readonly string _senhaProprietario = NovaSenha();
    private readonly string _senhaExecucao = NovaSenha();
    private readonly string _senhaOperadorUm = NovaSenha();
    private readonly string _senhaOperadorDois = NovaSenha();

    internal InstalacaoTestePostgres(PostgreSqlContainer conteiner, string rotulo)
    {
        _conteiner = conteiner;
        NomeBancoDados = $"instalacao_{rotulo}_{Guid.NewGuid():N}";
    }

    /// <summary>Tenant desta instalação.</summary>
    public Guid IdTenant { get; } = Guid.NewGuid();

    /// <summary>Ambiente desta instalação.</summary>
    public Guid IdAmbiente { get; } = Guid.NewGuid();

    /// <summary>Nome do banco exclusivo.</summary>
    public string NomeBancoDados { get; }

    /// <summary>Owner do banco e dos schemas (migrations).</summary>
    public string PapelProprietario => $"{NomeBancoDados}_owner";

    /// <summary>Login runtime da API (sem DDL).</summary>
    public string PapelExecucao => $"{NomeBancoDados}_runtime";

    /// <summary>Grupo NOLOGIN dos operadores de acesso.</summary>
    public string PapelOperador => $"{NomeBancoDados}_operador";

    /// <summary>Primeiro login individual de operador.</summary>
    public string LoginOperadorUm => $"{NomeBancoDados}_op_um";

    /// <summary>Segundo login individual de operador.</summary>
    public string LoginOperadorDois => $"{NomeBancoDados}_op_dois";

    /// <summary>Conexão do owner.</summary>
    public string StringConexaoProprietario => ConstruirStringConexao(PapelProprietario, _senhaProprietario);

    /// <summary>Conexão do runtime.</summary>
    public string StringConexaoExecucao => ConstruirStringConexao(PapelExecucao, _senhaExecucao);

    /// <summary>Conexão do primeiro operador individual.</summary>
    public string StringConexaoOperadorUm => ConstruirStringConexao(LoginOperadorUm, _senhaOperadorUm);

    /// <summary>Conexão do segundo operador individual.</summary>
    public string StringConexaoOperadorDois => ConstruirStringConexao(LoginOperadorDois, _senhaOperadorDois);

    /// <summary>Senha do runtime (somente para testes que verificam ausência em logs).</summary>
    public string SenhaExecucao => _senhaExecucao;

    internal async Task ProvisionarAsync(NpgsqlConnection admin, CancellationToken tokenCancelamento)
    {
        using NpgsqlCommandBuilder escapador = new();
        string proprietario = escapador.QuoteIdentifier(PapelProprietario);
        string execucao = escapador.QuoteIdentifier(PapelExecucao);
        string operador = escapador.QuoteIdentifier(PapelOperador);
        string operadorUm = escapador.QuoteIdentifier(LoginOperadorUm);
        string operadorDois = escapador.QuoteIdentifier(LoginOperadorDois);
        string banco = escapador.QuoteIdentifier(NomeBancoDados);
        const string Restricoes = "NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS";

        await ExecutarAsync(
            admin,
            $"CREATE ROLE {proprietario} LOGIN PASSWORD '{_senhaProprietario}' {Restricoes}; " +
            $"CREATE ROLE {execucao} LOGIN PASSWORD '{_senhaExecucao}' {Restricoes}; " +
            $"CREATE ROLE {operador} NOLOGIN {Restricoes}; " +
            $"CREATE ROLE {operadorUm} LOGIN PASSWORD '{_senhaOperadorUm}' {Restricoes} IN ROLE {operador}; " +
            $"CREATE ROLE {operadorDois} LOGIN PASSWORD '{_senhaOperadorDois}' {Restricoes} IN ROLE {operador}",
            tokenCancelamento);
        await ExecutarAsync(admin, $"CREATE DATABASE {banco} OWNER {proprietario}", tokenCancelamento);
        await ExecutarAsync(
            admin,
            $"REVOKE ALL ON DATABASE {banco} FROM PUBLIC; " +
            $"GRANT CONNECT ON DATABASE {banco} TO {proprietario}, {execucao}, {operador}",
            tokenCancelamento);

        await using NpgsqlConnection conexaoProprietario = new(StringConexaoProprietario);
        await conexaoProprietario.OpenAsync(tokenCancelamento);
        await ExecutarAsync(
            conexaoProprietario,
            "REVOKE CREATE ON SCHEMA public FROM PUBLIC; " +
            "CREATE SCHEMA foundation; " +
            "CREATE SCHEMA access; " +
            $"GRANT USAGE ON SCHEMA foundation, access TO {execucao}, {operador}",
            tokenCancelamento);
    }

    private static async Task ExecutarAsync(NpgsqlConnection conexao, string sql, CancellationToken tokenCancelamento)
    {
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync(tokenCancelamento);
    }

    private static string NovaSenha() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

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
