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
/// T111: Testes de integração do comando de limpeza em lote de sessões encerradas (FR-033: <c>sessao limpar</c>).
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
    public async Task SessoesAnterioresAoCorte_SaoRemovidas_EMantemAtivasOuNoCorte()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;

        DateTimeOffset agora = _relogio.GetUtcNow();
        const int RetencaoDias = 30;
        DateTimeOffset corte = agora.AddDays(-RetencaoDias);
        TimeSpan umMicrossegundo = TimeSpan.FromTicks(10);

        Guid idUsuario = await CriarUsuarioAsync(tenant, cancelamento);

        Guid idSessaoAtiva = IdentificadorV7.Gerar(agora);
        Guid idSessaoExpiradaAnterior = IdentificadorV7.Gerar(agora);
        Guid idSessaoRevogadaAnterior = IdentificadorV7.Gerar(agora);
        Guid idSessaoExpiradaNoCorte = IdentificadorV7.Gerar(agora);
        Guid idSessaoRevogadaNoCorte = IdentificadorV7.Gerar(agora);
        Guid idSessaoExpiradaDepoisCorte = IdentificadorV7.Gerar(agora);
        Guid idSessaoRevogadaDepoisCorte = IdentificadorV7.Gerar(agora);

        // Insere as sessões via conexão de runtime com seus respectivos instantes de emissão
        // 1. Ativa: TetoAbsolutoEm no futuro, não revogada
        await InserirSessaoAsync(tenant, idSessaoAtiva, idUsuario, agora.AddHours(1), null, cancelamento);
        // 2. Expirada anterior ao corte: elegível
        await InserirSessaoAsync(tenant, idSessaoExpiradaAnterior, idUsuario, corte - umMicrossegundo, null, cancelamento);
        // 3. Revogada anterior ao corte: elegível
        await InserirSessaoAsync(tenant, idSessaoRevogadaAnterior, idUsuario, agora.AddHours(1), corte - umMicrossegundo, cancelamento);
        // 4. Expirada exatamente no corte: mantida
        await InserirSessaoAsync(tenant, idSessaoExpiradaNoCorte, idUsuario, corte, null, cancelamento);
        // 5. Revogada exatamente no corte: mantida
        await InserirSessaoAsync(tenant, idSessaoRevogadaNoCorte, idUsuario, agora.AddHours(1), corte, cancelamento);
        // 6. Expirada depois do corte: mantida
        await InserirSessaoAsync(tenant, idSessaoExpiradaDepoisCorte, idUsuario, corte.AddHours(1), null, cancelamento);
        // 7. Revogada depois do corte: mantida
        await InserirSessaoAsync(tenant, idSessaoRevogadaDepoisCorte, idUsuario, agora.AddHours(1), corte.AddHours(1), cancelamento);

        // Executa a limpeza com o operador
        await using (EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, retencaoDiasBruto: RetencaoDias.ToString()))
        {
            CasoUsoLimparSessoes casoUso = escopo.Servicos.GetRequiredService<CasoUsoLimparSessoes>();
            ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, cancelamento);

            Assert.True(resultado.Sucesso, $"Limpeza falhou: {resultado.Codigo} - {resultado.MensagemDiagnostico}");
            Assert.Equal("SUCESSO", resultado.Codigo);
            Assert.Equal(2, resultado.QuantidadeRemovida);
            Assert.Equal(RetencaoDias, resultado.RetencaoDias);
            Assert.Equal(corte, resultado.InstanteCorte);
            Assert.Null(resultado.IdUsuario);

            // Confere estado das sessões no banco
            HashSet<Guid> sessoesRestantes = (await escopo.Contexto.Sessoes
                .AsNoTracking()
                .Where(s => s.FkIdUsuario == idUsuario)
                .Select(s => s.IdSessao)
                .ToListAsync(cancelamento))
                .ToHashSet();

            // Apenas as duas anteriores foram removidas
            Assert.DoesNotContain(idSessaoExpiradaAnterior, sessoesRestantes);
            Assert.DoesNotContain(idSessaoRevogadaAnterior, sessoesRestantes);

            // As demais foram mantidas
            Assert.Contains(idSessaoAtiva, sessoesRestantes);
            Assert.Contains(idSessaoExpiradaNoCorte, sessoesRestantes);
            Assert.Contains(idSessaoRevogadaNoCorte, sessoesRestantes);
            Assert.Contains(idSessaoExpiradaDepoisCorte, sessoesRestantes);
            Assert.Contains(idSessaoRevogadaDepoisCorte, sessoesRestantes);

            // Confere o registro de auditoria via owner
            await using AcessoTenantDbContext contextoOwner = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
            RegistroOperacaoAcesso? registro = await contextoOwner.OperacoesAcesso
                .FirstOrDefaultAsync(r => r.IdOperacao == resultado.IdOperacao, cancelamento);

            Assert.NotNull(registro);
            Assert.Equal(TipoOperacaoAcesso.LimparSessoes, registro.TipoOperacao);
            Assert.Equal(ResultadoOperacaoAcesso.Sucesso, registro.Resultado);
            Assert.Equal(tenant.LoginOperadorUm, registro.LoginOperador);
            Assert.Equal(RetencaoDias, registro.RetencaoDias);
            Assert.Equal(corte, registro.InstanteCorte);
            Assert.Equal(2, registro.QuantidadeRemovida);
            Assert.Null(registro.IdUsuarioAlvo);
            Assert.Null(registro.CodigoPermissao);
        }
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

    private static async Task InserirSessaoAsync(
        InstalacaoTestePostgres tenant,
        Guid idSessao,
        Guid idUsuario,
        DateTimeOffset tetoAbsolutoEm,
        DateTimeOffset? revogadaEm,
        CancellationToken cancelamento)
    {
        DateTimeOffset criadoEm = tetoAbsolutoEm.AddMinutes(-30);
        ContextoOperacaoTeste contexto = ContextoOperacaoTeste.Criar(
            new RelogioControlavel(criadoEm), TipoAutor.Sistema, "api_runtime");

        await using AcessoTenantDbContext banco = BancoTeste.CriarContexto(tenant.StringConexaoExecucao, contexto);
        SessaoAutenticacao sessao = new()
        {
            IdSessao = idSessao,
            FkIdUsuario = idUsuario,
            EpocaAcesso = Guid.NewGuid(),
            TetoAbsolutoEm = tetoAbsolutoEm,
            JanelaInatividadeMinutos = 30,
            UltimoUsoEm = contexto.InstanteOperacao,
            RevogadaEm = revogadaEm,
            TicketProtegido = revogadaEm is null ? [1, 2, 3] : null,
        };
        banco.Sessoes.Add(sessao);
        await banco.SaveChangesAsync(cancelamento);
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
