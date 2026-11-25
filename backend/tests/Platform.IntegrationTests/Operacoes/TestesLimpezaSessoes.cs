namespace Platform.IntegrationTests.Operacoes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Platform.Application.Access.Operacoes;
using Platform.Domain.Access;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T111/T164: Testes de integração do comando de limpeza em lote de sessões encerradas (FR-033: <c>sessao limpar</c>),
/// com elegibilidade pela expiração efetiva <c>min(UltimoUsoEm + janela, TetoAbsolutoEm)</c> ou pela <c>RevogadaEm</c> (L2.1, D31).
/// </summary>
public sealed class TestesLimpezaSessoes(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>, IAsyncLifetime
{
    private readonly RelogioControlavel _relogio = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            await BancoTeste.MigrarAsync(tenant, _relogio, cancelamento);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task SessoesAlemDaRetencao_PelaExpiracaoEfetivaOuRevogacao_SaoRemovidas_EAsDemaisMantidas()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;

        DateTimeOffset agora = _relogio.GetUtcNow();
        const int RetencaoDias = 30;
        DateTimeOffset corte = agora.AddDays(-RetencaoDias);
        TimeSpan umMicrossegundo = TimeSpan.FromTicks(10);

        Guid idUsuario = await CriarUsuarioAsync(tenant, cancelamento);

        // Inatividade (janela gravada na sessão, não a configuração): expiração efetiva = UltimoUsoEm + janela.
        // Teto bem depois do corte e CriadoEm anterior ao último uso: só a inatividade decide.
        Guid inatividadeAntesCorte = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddHours(-5), ultimoUsoEm: corte.AddMinutes(-30) - umMicrossegundo,
            tetoMinutos: 600, janelaMinutos: 30, revogadaEm: null, cancelamento);
        Guid inatividadeNoCorte = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddHours(-5), ultimoUsoEm: corte.AddMinutes(-30),
            tetoMinutos: 600, janelaMinutos: 30, revogadaEm: null, cancelamento);
        Guid inatividadeJanela5AntesCorte = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddHours(-1), ultimoUsoEm: corte.AddMinutes(-5) - umMicrossegundo,
            tetoMinutos: 600, janelaMinutos: 5, revogadaEm: null, cancelamento);
        Guid inatividadeJanela240DepoisCorte = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddHours(-1), ultimoUsoEm: corte.AddMinutes(-30) - umMicrossegundo,
            tetoMinutos: 600, janelaMinutos: 240, revogadaEm: null, cancelamento);
        Guid inatividadeDepoisCorte = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddHours(-1), ultimoUsoEm: corte.AddMinutes(-29),
            tetoMinutos: 600, janelaMinutos: 30, revogadaEm: null, cancelamento);

        // Teto: uso recente (UltimoUsoEm + janela ultrapassa o teto), expiração efetiva = TetoAbsolutoEm.
        Guid tetoAntesCorte = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddMinutes(-600) - umMicrossegundo, ultimoUsoEm: corte.AddMinutes(-10) - umMicrossegundo,
            tetoMinutos: 600, janelaMinutos: 30, revogadaEm: null, cancelamento);
        Guid tetoNoCorte = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddMinutes(-600), ultimoUsoEm: corte.AddMinutes(-10),
            tetoMinutos: 600, janelaMinutos: 30, revogadaEm: null, cancelamento);

        // Revogada: decide a RevogadaEm (expiração efetiva depois do corte nos dois primeiros casos;
        // no terceiro, expirada muito antes do corte mas revogada depois dele).
        Guid revogadaAntesCorte = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddMinutes(-10), ultimoUsoEm: corte.AddMinutes(-10),
            tetoMinutos: 600, janelaMinutos: 30, revogadaEm: corte - umMicrossegundo, cancelamento);
        Guid revogadaNoCorte = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddMinutes(-10), ultimoUsoEm: corte.AddMinutes(-10),
            tetoMinutos: 600, janelaMinutos: 30, revogadaEm: corte, cancelamento);
        Guid revogadaDepoisCorteExpiradaAntes = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: corte.AddDays(-2), ultimoUsoEm: corte.AddDays(-2),
            tetoMinutos: 600, janelaMinutos: 30, revogadaEm: corte.AddHours(1), cancelamento);

        // Ativas (uso recente), nunca elegíveis, mesmo com CriadoEm antigo (teto máximo de 720 min).
        Guid ativaCriadaHaQuaseDozeHoras = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: agora.AddMinutes(-719), ultimoUsoEm: agora.AddMinutes(-1),
            tetoMinutos: 720, janelaMinutos: 30, revogadaEm: null, cancelamento);
        Guid ativaRecente = await InserirSessaoAsync(
            tenant, idUsuario, criadoEm: agora.AddMinutes(-5), ultimoUsoEm: agora.AddMinutes(-5),
            tetoMinutos: 600, janelaMinutos: 30, revogadaEm: null, cancelamento);

        Guid[] esperadasRemovidas = [inatividadeAntesCorte, inatividadeJanela5AntesCorte, tetoAntesCorte, revogadaAntesCorte];
        Guid[] esperadasMantidas =
        [
            inatividadeNoCorte, inatividadeJanela240DepoisCorte, inatividadeDepoisCorte, tetoNoCorte,
            revogadaNoCorte, revogadaDepoisCorteExpiradaAntes, ativaCriadaHaQuaseDozeHoras, ativaRecente,
        ];

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, retencaoDiasBruto: RetencaoDias.ToString());

        CasoUsoLimparSessoes casoUso = escopo.Servicos.GetRequiredService<CasoUsoLimparSessoes>();
        ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, cancelamento);

        Assert.True(resultado.Sucesso, $"Limpeza falhou: {resultado.Codigo} - {resultado.MensagemDiagnostico}");
        Assert.Equal("SUCESSO", resultado.Codigo);
        Assert.Equal(esperadasRemovidas.Length, resultado.QuantidadeRemovida);
        Assert.Equal(RetencaoDias, resultado.RetencaoDias);
        Assert.Equal(corte, resultado.InstanteCorte);
        Assert.Null(resultado.IdUsuario);

        HashSet<Guid> sessoesRestantes = (await escopo.Contexto.Sessoes
            .AsNoTracking()
            .Where(s => s.FkIdUsuario == idUsuario)
            .Select(s => s.IdSessao)
            .ToListAsync(cancelamento))
            .ToHashSet();

        Assert.All(esperadasRemovidas, id => Assert.DoesNotContain(id, sessoesRestantes));
        Assert.All(esperadasMantidas, id => Assert.Contains(id, sessoesRestantes));

        // Um único registro por execução, com retenção, corte e quantidade.
        await using AcessoTenantDbContext contextoOwner = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
        List<RegistroOperacaoAcesso> registros = await contextoOwner.OperacoesAcesso
            .AsNoTracking()
            .Where(r => r.IdOperacao == resultado.IdOperacao)
            .ToListAsync(cancelamento);

        RegistroOperacaoAcesso registro = Assert.Single(registros);
        Assert.Equal(TipoOperacaoAcesso.LimparSessoes, registro.TipoOperacao);
        Assert.Equal(ResultadoOperacaoAcesso.Sucesso, registro.Resultado);
        Assert.Equal(tenant.LoginOperadorUm, registro.LoginOperador);
        Assert.Equal(RetencaoDias, registro.RetencaoDias);
        Assert.Equal(corte, registro.InstanteCorte);
        Assert.Equal(esperadasRemovidas.Length, registro.QuantidadeRemovida);
        Assert.Null(registro.IdUsuarioAlvo);
        Assert.Null(registro.CodigoPermissao);
    }

    [Fact]
    public async Task NenhumaElegivel_RetornaSucessoComZeroEUmRegistroAuditado()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio);

        CasoUsoLimparSessoes casoUso = escopo.Servicos.GetRequiredService<CasoUsoLimparSessoes>();
        ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, cancelamento);

        Assert.True(resultado.Sucesso);
        Assert.Equal(0, resultado.QuantidadeRemovida);

        await using AcessoTenantDbContext contextoOwner = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
        RegistroOperacaoAcesso? registro = await contextoOwner.OperacoesAcesso
            .FirstOrDefaultAsync(r => r.IdOperacao == resultado.IdOperacao, cancelamento);

        Assert.NotNull(registro);
        Assert.Equal(ResultadoOperacaoAcesso.Sucesso, registro.Resultado);
        Assert.Equal(0, registro.QuantidadeRemovida);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("366")]
    [InlineData("-1")]
    [InlineData("30.5")]
    [InlineData("invalido")]
    public async Task RetencaoInvalida_RetornaFalhaSemConectarNemRegistrar(string retencaoInvalida)
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, retencaoDiasBruto: retencaoInvalida);

        CasoUsoLimparSessoes casoUso = escopo.Servicos.GetRequiredService<CasoUsoLimparSessoes>();
        ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, cancelamento);

        Assert.False(resultado.Sucesso);
        Assert.Equal("RETENCAO_INVALIDA", resultado.Codigo);

        // Nenhuma gravação no banco de auditoria
        await using AcessoTenantDbContext contextoOwner = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
        bool temRegistro = await contextoOwner.OperacoesAcesso
            .AnyAsync(r => r.IdOperacao == resultado.IdOperacao, cancelamento);
        Assert.False(temRegistro);
    }

    [Theory]
    [InlineData("", 30)]
    [InlineData("1", 1)]
    [InlineData("365", 365)]
    public async Task RetencaoValidaOuPadrao_SaoAceitas(string retencaoBruta, int esperado)
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, retencaoDiasBruto: retencaoBruta);

        CasoUsoLimparSessoes casoUso = escopo.Servicos.GetRequiredService<CasoUsoLimparSessoes>();
        ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, cancelamento);

        Assert.True(resultado.Sucesso);
        Assert.Equal(esperado, resultado.RetencaoDias);
    }

    [Fact]
    public async Task Runtime_NaoConsegueExecutarDeleteEmSessoes()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;

        await using NpgsqlConnection conexaoRuntime = new(tenant.StringConexaoExecucao);
        await conexaoRuntime.OpenAsync(cancelamento);

        await using NpgsqlCommand comando = new("DELETE FROM access.sessoes_autenticacao", conexaoRuntime);
        PostgresException ex = await Assert.ThrowsAsync<PostgresException>(() => comando.ExecuteNonQueryAsync(cancelamento));
        Assert.Equal("42501", ex.SqlState);
    }

    private static async Task<Guid> InserirSessaoAsync(
        InstalacaoTestePostgres tenant,
        Guid idUsuario,
        DateTimeOffset criadoEm,
        DateTimeOffset ultimoUsoEm,
        int tetoMinutos,
        int janelaMinutos,
        DateTimeOffset? revogadaEm,
        CancellationToken cancelamento)
    {
        Guid idSessao = IdentificadorV7.Gerar(criadoEm);
        ContextoOperacaoTeste contexto = ContextoOperacaoTeste.Criar(
            new RelogioControlavel(criadoEm), TipoAutor.Sistema, "api_runtime");

        await using AcessoTenantDbContext banco = BancoTeste.CriarContexto(tenant.StringConexaoExecucao, contexto);
        SessaoAutenticacao sessao = new()
        {
            IdSessao = idSessao,
            FkIdUsuario = idUsuario,
            EpocaAcesso = Guid.NewGuid(),
            TetoAbsolutoEm = criadoEm.AddMinutes(tetoMinutos),
            JanelaInatividadeMinutos = janelaMinutos,
            UltimoUsoEm = ultimoUsoEm,
            RevogadaEm = revogadaEm,
            TicketProtegido = revogadaEm is null ? [1, 2, 3] : null,
        };
        banco.Sessoes.Add(sessao);
        await banco.SaveChangesAsync(cancelamento);
        return idSessao;
    }

    private async Task<Guid> CriarUsuarioAsync(InstalacaoTestePostgres tenant, CancellationToken cancelamento)
    {
        string email = $"user_{Guid.NewGuid():N}@tenant.local";
        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, "SenhaSegura123!");

        ResultadoOperacao res = await escopo.Servicos.GetRequiredService<CasoUsoCriarConta>()
            .ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, email, "Nome", cancelamento);
        Assert.True(res.Sucesso);
        return res.IdUsuario!.Value;
    }
}
