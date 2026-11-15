namespace Platform.IntegrationTests.Operacoes;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Application.Access.Operacoes;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Domain.Access;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T133 (G29): somente conflito concorrente real resulta em <c>CONFLITO_CONCORRENTE</c>;
/// email duplicado tem código próprio e falha inesperada é <c>FALHA_INTERNA</c>, com detalhe só no diagnóstico.
/// </summary>
public sealed class TestesClassificacaoFalhasOperacoes(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>, IAsyncLifetime
{
    private const string SenhaValida = "SenhaValida123!";
    private readonly RelogioControlavel _relogio = new(new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync() =>
        await BancoTeste.MigrarAsync(fixture.TenantA, _relogio, TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task CriarConta_ComEmailJaCadastrado_FalhaComEmailJaCadastradoEAuditoria()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        string email = $"duplicado_{Guid.NewGuid():N}@tenant.local";

        await using (EscopoOperacoesTeste primeiro = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, SenhaValida))
        {
            ResultadoOperacao criado = await primeiro.Servicos.GetRequiredService<CasoUsoCriarConta>()
                .ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, email, "Primeira", cancelamento);
            Assert.True(criado.Sucesso, $"{criado.Codigo} - {criado.MensagemDiagnostico}");
        }

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, SenhaValida);
        ResultadoOperacao resultado = await escopo.Servicos.GetRequiredService<CasoUsoCriarConta>()
            .ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, email, "Segunda", cancelamento);

        Assert.False(resultado.Sucesso);
        Assert.Equal(ResultadoOperacaoAcesso.Falha, resultado.Resultado);
        Assert.Equal("EMAIL_JA_CADASTRADO", resultado.Codigo);
        Assert.Equal(1, await escopo.Contexto.Users.CountAsync(u => u.Email == email, cancelamento));

        RegistroOperacaoAcesso registro = await LerRegistroAsync(tenant, resultado.IdOperacao, cancelamento);
        Assert.Equal(ResultadoOperacaoAcesso.Falha, registro.Resultado);
        Assert.Equal("EMAIL_JA_CADASTRADO", registro.CodigoResultado);
    }

    [Fact]
    public async Task CriarConta_ComExcecaoInesperada_FalhaInternaSemMascararComoConflito()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        const string Detalhe = "detalhe técnico sintético";

        await using EscopoOperacoesTeste escopo = CriarEscopoComFalha(tenant, new InvalidOperationException(Detalhe));
        ResultadoOperacao resultado = await escopo.Servicos.GetRequiredService<CasoUsoCriarConta>()
            .ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, $"interna_{Guid.NewGuid():N}@tenant.local", "Nome", cancelamento);

        Assert.False(resultado.Sucesso);
        Assert.Equal("FALHA_INTERNA", resultado.Codigo);
        Assert.Equal(tenant.LoginOperadorUm, resultado.LoginOperador);

        // O detalhe técnico fica só no diagnóstico (stderr), nunca no código publicado/auditado.
        Assert.Contains(nameof(InvalidOperationException), resultado.MensagemDiagnostico, StringComparison.Ordinal);
        Assert.Contains(Detalhe, resultado.MensagemDiagnostico, StringComparison.Ordinal);

        RegistroOperacaoAcesso registro = await LerRegistroAsync(tenant, resultado.IdOperacao, cancelamento);
        Assert.Equal(ResultadoOperacaoAcesso.Falha, registro.Resultado);
        Assert.Equal("FALHA_INTERNA", registro.CodigoResultado);
    }

    [Fact]
    public async Task CriarConta_ComConflitoDeConcorrencia_MantemConflitoConcorrente()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;

        await using EscopoOperacoesTeste escopo = CriarEscopoComFalha(
            tenant, new DbUpdateConcurrencyException("Concorrência sintética."));
        ResultadoOperacao resultado = await escopo.Servicos.GetRequiredService<CasoUsoCriarConta>()
            .ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, $"conflito_{Guid.NewGuid():N}@tenant.local", "Nome", cancelamento);

        Assert.False(resultado.Sucesso);
        Assert.Equal("CONFLITO_CONCORRENTE", resultado.Codigo);

        RegistroOperacaoAcesso registro = await LerRegistroAsync(tenant, resultado.IdOperacao, cancelamento);
        Assert.Equal("CONFLITO_CONCORRENTE", registro.CodigoResultado);
    }

    private EscopoOperacoesTeste CriarEscopoComFalha(InstalacaoTestePostgres tenant, Exception falha) =>
        EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, SenhaValida,
            substituirServicos: servicos => servicos.Replace(
                ServiceDescriptor.Scoped<IManutencaoContas>(_ => new ManutencaoContasComFalha(falha))));

    private static async Task<RegistroOperacaoAcesso> LerRegistroAsync(
        InstalacaoTestePostgres tenant, Guid idOperacao, CancellationToken cancelamento)
    {
        // Operador só tem INSERT em operacoes_acesso: leitura via owner.
        await using AcessoTenantDbContext contextoOwner = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
        RegistroOperacaoAcesso? registro = await contextoOwner.OperacoesAcesso
            .FirstOrDefaultAsync(r => r.IdOperacao == idOperacao, cancelamento);
        Assert.NotNull(registro);
        return registro;
    }

    /// <summary>Manutenção sintética que lança a exceção configurada na criação de conta.</summary>
    private sealed class ManutencaoContasComFalha(Exception falha) : IManutencaoContas
    {
        public Task<ResultadoAcaoConta> CriarContaAsync(Guid idUsuario, string email, string nomeExibicao, string senha, CancellationToken tokenCancelamento = default) =>
            Task.FromException<ResultadoAcaoConta>(falha);

        public Task<ResultadoAcaoConta> DesativarContaAsync(Guid idUsuario, CancellationToken tokenCancelamento = default) =>
            throw new NotSupportedException();

        public Task<ResultadoAcaoConta> ReativarContaAsync(Guid idUsuario, CancellationToken tokenCancelamento = default) =>
            throw new NotSupportedException();

        public Task<ResultadoAcaoConta> RedefinirSenhaAsync(Guid idUsuario, string novaSenha, CancellationToken tokenCancelamento = default) =>
            throw new NotSupportedException();

        public Task<ResultadoAcaoConta> RevogarSessoesAsync(Guid idUsuario, CancellationToken tokenCancelamento = default) =>
            throw new NotSupportedException();

        public Task<ResultadoAcaoConta> ConcederPermissaoAsync(Guid idUsuario, string codigoPermissao, CancellationToken tokenCancelamento = default) =>
            throw new NotSupportedException();

        public Task<ResultadoAcaoConta> RevogarPermissaoAsync(Guid idUsuario, string codigoPermissao, CancellationToken tokenCancelamento = default) =>
            throw new NotSupportedException();
    }
}
