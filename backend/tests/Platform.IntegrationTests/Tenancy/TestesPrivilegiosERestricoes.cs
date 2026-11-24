namespace Platform.IntegrationTests.Tenancy;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Domain.Tempo;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T077: GRANTs mínimos emitidos pela migration (G6), tentativas negadas e CHECKs do banco.
/// </summary>
public sealed class TestesPrivilegiosERestricoes(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>, IAsyncLifetime
{
    private readonly RelogioControlavel _relogio = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero).AddTicks(1234567));

    public async ValueTask InitializeAsync()
    {
        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            await BancoTeste.MigrarAsync(tenant, _relogio, TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public static TheoryData<string> ComandosNegadosAoRuntime() =>
    [
        "CREATE TABLE access.ddl_proibido (id integer)",
        "ALTER TABLE access.usuarios ADD COLUMN extra integer",
        "DELETE FROM access.sessoes_autenticacao",
        "INSERT INTO access.operacoes_acesso (\"IdOperacao\") VALUES (gen_random_uuid())",
        "SELECT count(*) FROM access.operacoes_acesso",
        "INSERT INTO access.reivindicacoes_usuario (\"FkIdUsuario\", \"ClaimType\") VALUES (gen_random_uuid(), 'x')",
        "INSERT INTO access.logins_usuario (\"LoginProvider\", \"ProviderKey\", \"FkIdUsuario\") VALUES ('p', 'k', gen_random_uuid())",
        "INSERT INTO access.tokens_usuario (\"FkIdUsuario\", \"LoginProvider\", \"Name\") VALUES (gen_random_uuid(), 'p', 'n')",
        "UPDATE foundation.identidade_banco SET \"EpocaAcesso\" = gen_random_uuid()",
        "INSERT INTO access.usuarios (\"IdUsuario\") VALUES (gen_random_uuid())",
        "DELETE FROM access.usuarios",
        "INSERT INTO access.permissoes_usuario (\"FkIdUsuario\", \"CodigoPermissao\") VALUES (gen_random_uuid(), 'a.b')",
    ];

    public static TheoryData<string> ComandosNegadosAoOperador() =>
    [
        "CREATE TABLE access.ddl_proibido (id integer)",
        "UPDATE access.operacoes_acesso SET \"CodigoResultado\" = 'X'",
        "DELETE FROM access.operacoes_acesso",
        "UPDATE foundation.identidade_banco SET \"EpocaAcesso\" = gen_random_uuid()",
        "DELETE FROM foundation.identidade_banco",
        "INSERT INTO access.reivindicacoes_usuario (\"FkIdUsuario\", \"ClaimType\") VALUES (gen_random_uuid(), 'x')",
        "DELETE FROM access.usuarios",
        "UPDATE access.permissoes_usuario SET \"CodigoPermissao\" = 'a.b'",
    ];

    [Theory]
    [MemberData(nameof(ComandosNegadosAoRuntime))]
    public async Task Runtime_NaoTemPrivilegioAlemDoNecessario(string sql)
    {
        PostgresException negado = await Assert.ThrowsAsync<PostgresException>(() => ExecutarAsync(fixture.TenantA.StringConexaoExecucao, sql));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, negado.SqlState);
    }

    [Theory]
    [MemberData(nameof(ComandosNegadosAoOperador))]
    public async Task Operador_NaoAlteraHistoricoMarcadorNemTabelasAuxiliares(string sql)
    {
        PostgresException negado = await Assert.ThrowsAsync<PostgresException>(() => ExecutarAsync(fixture.TenantA.StringConexaoOperadorUm, sql));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, negado.SqlState);
    }

    [Fact]
    public async Task Runtime_NaoConectaEmOutraInstalacao()
    {
        NpgsqlConnectionStringBuilder cruzada = new(fixture.TenantA.StringConexaoExecucao) { Database = fixture.TenantB.NomeBancoDados };
        await using NpgsqlConnection conexao = new(cruzada.ConnectionString);

        PostgresException negado = await Assert.ThrowsAsync<PostgresException>(() => conexao.OpenAsync(TestContext.Current.CancellationToken));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, negado.SqlState);
    }

    [Theory]
    [InlineData(29)]
    [InlineData(721)]
    public async Task CheckDeTeto_RejeitaSessaoForaDe30a720Minutos(int tetoMinutos)
    {
        Guid idUsuario = await CriarContaAsync(fixture.TenantA);

        PostgresException violacao = await Assert.ThrowsAsync<PostgresException>(
            () => InserirSessaoSqlAsync(idUsuario, tetoMinutos, janelaMinutos: 5, TimeSpan.Zero, revogadaComTicket: false));

        Assert.Equal(PostgresErrorCodes.CheckViolation, violacao.SqlState);
        Assert.Equal("ck_sessoes_autenticacao_teto", violacao.ConstraintName);
    }

    [Theory]
    [InlineData(600, 4)]
    [InlineData(600, 241)]
    [InlineData(30, 31)]
    [InlineData(60, 61)]
    public async Task CheckDeJanela_RejeitaForaDe5a240OuMaiorQueTeto(int tetoMinutos, int janelaMinutos)
    {
        Guid idUsuario = await CriarContaAsync(fixture.TenantA);

        PostgresException violacao = await Assert.ThrowsAsync<PostgresException>(
            () => InserirSessaoSqlAsync(idUsuario, tetoMinutos, janelaMinutos, TimeSpan.Zero, revogadaComTicket: false));

        Assert.Equal(PostgresErrorCodes.CheckViolation, violacao.SqlState);
        Assert.Equal("ck_sessoes_autenticacao_janela_inatividade", violacao.ConstraintName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(600 * 60 * 1_000_000L)]
    [InlineData(601 * 60 * 1_000_000L)]
    public async Task CheckDeUltimoUso_RejeitaForaDeCriacaoAteTeto(long microssegundosDesdeCriacao)
    {
        Guid idUsuario = await CriarContaAsync(fixture.TenantA);
        TimeSpan deslocamento = TimeSpan.FromMicroseconds(microssegundosDesdeCriacao);

        PostgresException violacao = await Assert.ThrowsAsync<PostgresException>(
            () => InserirSessaoSqlAsync(idUsuario, 600, 30, deslocamento, revogadaComTicket: false));

        Assert.Equal(PostgresErrorCodes.CheckViolation, violacao.SqlState);
        Assert.Equal("ck_sessoes_autenticacao_ultimo_uso", violacao.ConstraintName);
    }

    [Theory]
    [InlineData(30, 5)]
    [InlineData(720, 240)]
    [InlineData(30, 30)]
    [InlineData(600, 30)]
    public async Task ChecksDeSessao_AceitamLimites(int tetoMinutos, int janelaMinutos)
    {
        Guid idUsuario = await CriarContaAsync(fixture.TenantA);

        await InserirSessaoSqlAsync(idUsuario, tetoMinutos, janelaMinutos, TimeSpan.Zero, revogadaComTicket: false);
        await InserirSessaoSqlAsync(idUsuario, tetoMinutos, janelaMinutos, TimeSpan.FromMinutes(tetoMinutos) - TimeSpan.FromMicroseconds(1), revogadaComTicket: false);
    }

    [Fact]
    public async Task CheckDeSessaoRevogada_RejeitaTicketRemanescente()
    {
        Guid idUsuario = await CriarContaAsync(fixture.TenantA);

        PostgresException violacao = await Assert.ThrowsAsync<PostgresException>(
            () => InserirSessaoSqlAsync(idUsuario, 600, 30, TimeSpan.Zero, revogadaComTicket: true));

        Assert.Equal("ck_sessoes_autenticacao_revogada_sem_ticket", violacao.ConstraintName);
    }

    [Fact]
    public async Task CheckDeLimpeza_RejeitaCamposForaDeLimparSessoes()
    {
        string sql = "INSERT INTO access.operacoes_acesso (\"IdOperacao\", \"IdTenant\", \"IdAmbiente\", \"LoginOperador\", \"TipoOperacao\", " +
            "\"RegistradaEm\", \"Resultado\", \"CodigoResultado\", \"RetencaoDias\", \"InstanteCorte\", \"QuantidadeRemovida\") " +
            "VALUES (gen_random_uuid(), gen_random_uuid(), gen_random_uuid(), 'op', 'CriarConta', now(), 'Sucesso', 'SUCESSO', 30, now(), 0)";

        PostgresException violacao = await Assert.ThrowsAsync<PostgresException>(() => ExecutarAsync(fixture.TenantA.StringConexaoOperadorUm, sql));
        Assert.Equal("ck_operacoes_acesso_limpeza", violacao.ConstraintName);
    }

    [Fact]
    public async Task CheckDeCodigoPermissao_RejeitaForaDoFormato()
    {
        Guid idUsuario = await CriarContaAsync(fixture.TenantA);
        string sql = "INSERT INTO access.permissoes_usuario (\"FkIdUsuario\", \"CodigoPermissao\", \"CriadoEm\", \"TipoAutorCriacao\", \"AutorCriacao\", \"IdOperacaoCriacao\") " +
            $"VALUES ('{idUsuario}', 'Fundacao.Contexto', now(), 'Operador', 'op', gen_random_uuid())";

        PostgresException violacao = await Assert.ThrowsAsync<PostgresException>(() => ExecutarAsync(fixture.TenantA.StringConexaoOperadorUm, sql));
        Assert.Equal("ck_permissoes_usuario_codigo_permissao", violacao.ConstraintName);
    }

    [Fact]
    public async Task Instantes_GravadosELidosComOffsetZeroEMicrossegundosIguais()
    {
        Guid idUsuario = await CriarContaAsync(fixture.TenantA);
        ContextoOperacaoTeste contexto = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Usuario, idUsuario.ToString());
        SessaoAutenticacao sessao = new()
        {
            IdSessao = IdentificadorV7.Gerar(contexto.InstanteOperacao),
            FkIdUsuario = idUsuario,
            EpocaAcesso = IdentificadorV7.Gerar(contexto.InstanteOperacao),
            TicketProtegido = [1, 2, 3],
            TetoAbsolutoEm = contexto.InstanteOperacao.AddMinutes(600),
            JanelaInatividadeMinutos = 30,
            UltimoUsoEm = contexto.InstanteOperacao,
        };

        await using (AcessoTenantDbContext gravacao = BancoTeste.CriarContexto(fixture.TenantA.StringConexaoExecucao, contexto))
        {
            gravacao.Sessoes.Add(sessao);
            await gravacao.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(fixture.TenantA.StringConexaoExecucao);
        SessaoAutenticacao lida = await leitura.Sessoes.AsNoTracking().SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.Zero, lida.CriadoEm.Offset);
        Assert.Equal(contexto.InstanteOperacao, lida.CriadoEm);
        Assert.Equal(contexto.InstanteOperacao.UtcTicks, lida.CriadoEm.UtcTicks);
        Assert.Equal(sessao.TetoAbsolutoEm, lida.TetoAbsolutoEm);
        Assert.Equal(contexto.InstanteOperacao, lida.UltimoUsoEm);
        Assert.Equal(TimeSpan.Zero, lida.UltimoUsoEm.Offset);
        Assert.Equal(0, lida.CriadoEm.Ticks % 10);
    }

    [Fact]
    public async Task TabelasAuxiliaresDoIdentityPermanecemVazias()
    {
        await CriarContaAsync(fixture.TenantA);
        await using NpgsqlConnection conexao = new(fixture.TenantA.StringConexaoProprietario);
        await conexao.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand comando = new(
            "SELECT (SELECT count(*) FROM access.reivindicacoes_usuario) + (SELECT count(*) FROM access.logins_usuario) + (SELECT count(*) FROM access.tokens_usuario)",
            conexao);

        Assert.Equal(0L, await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    private static async Task ExecutarAsync(string conexaoTexto, string sql)
    {
        await using NpgsqlConnection conexao = new(conexaoTexto);
        await conexao.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> CriarContaAsync(InstalacaoTestePostgres tenant)
    {
        ContextoOperacaoTeste contexto = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, tenant.LoginOperadorUm);
        await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(tenant.StringConexaoOperadorUm, contexto);
        string email = $"conta_{Guid.CreateVersion7(contexto.InstanteOperacao):N}@tenant.local";
        UsuarioAplicacao conta = new()
        {
            Id = IdentificadorV7.Gerar(contexto.InstanteOperacao),
            FkIdTenant = tenant.IdTenant,
            UserName = email,
            Email = email,
            NomeExibicao = "Conta de privilégios",
            LockoutEnabled = true,
        };
        IdentityResult resultado = await escopo.GerenciadorUsuarios.CreateAsync(conta, "SenhaSegura123!");
        Assert.True(resultado.Succeeded);
        return conta.Id;
    }

    private async Task InserirSessaoSqlAsync(Guid idUsuario, int tetoMinutos, int janelaMinutos, TimeSpan ultimoUsoDesdeCriacao, bool revogadaComTicket)
    {
        string revogada = revogadaComTicket ? "@criado" : "NULL";
        string sql = "INSERT INTO access.sessoes_autenticacao (\"IdSessao\", \"FkIdUsuario\", \"EpocaAcesso\", \"TicketProtegido\", \"TetoAbsolutoEm\", " +
            "\"JanelaInatividadeMinutos\", \"UltimoUsoEm\", \"RevogadaEm\", \"Versao\", \"CriadoEm\", \"TipoAutorCriacao\", \"AutorCriacao\", \"IdOperacaoCriacao\") " +
            $"VALUES (gen_random_uuid(), '{idUsuario}', gen_random_uuid(), '\\x01'::bytea, @criado + @teto, @janela, @criado + @ultimoUso, {revogada}, 1, @criado, 'Usuario', 'u', gen_random_uuid())";
        await using NpgsqlConnection conexao = new(fixture.TenantA.StringConexaoExecucao);
        await conexao.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand comando = new(sql, conexao);
        comando.Parameters.AddWithValue("criado", InstanteUtc.Normalizar(_relogio.GetUtcNow()));
        comando.Parameters.AddWithValue("teto", TimeSpan.FromMinutes(tetoMinutos));
        comando.Parameters.AddWithValue("janela", janelaMinutos);
        comando.Parameters.AddWithValue("ultimoUso", ultimoUsoDesdeCriacao);
        await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
