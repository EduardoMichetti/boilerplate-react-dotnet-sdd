namespace Platform.IntegrationTests.Access;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Access;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T079 / SC-009: persistência e isolamento de contas e concessões sobre a migration inicial
/// única. Contas são criadas pelo operador individual (o runtime não tem INSERT em usuários).
/// </summary>
public sealed class TestesPersistenciaUsuarioTenant(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>, IAsyncLifetime
{
    private readonly RelogioControlavel _relogio = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    public async ValueTask InitializeAsync()
    {
        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            await BancoTeste.MigrarAsync(tenant, _relogio, TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task MesmoEmailEId_CriamContasIndependentesComSenhasDistintas()
    {
        Guid idUsuarioCompartilhado = IdentificadorV7.Gerar(_relogio.GetUtcNow());
        const string EmailCompartilhado = "comum@tenant.local";
        const string SenhaA = "SenhaTenantA123!";
        const string SenhaB = "SenhaTenantB456!";

        await CriarContaAsync(fixture.TenantA, idUsuarioCompartilhado, EmailCompartilhado, "Usuário no Tenant A", SenhaA);
        await CriarContaAsync(fixture.TenantB, idUsuarioCompartilhado, EmailCompartilhado, "Usuário no Tenant B", SenhaB);

        foreach ((InstalacaoTestePostgres tenant, string nome, string senhaCerta, string senhaOutra) in new[]
        {
            (fixture.TenantA, "Usuário no Tenant A", SenhaA, SenhaB),
            (fixture.TenantB, "Usuário no Tenant B", SenhaB, SenhaA),
        })
        {
            await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(tenant.StringConexaoExecucao, Contexto(TipoAutor.Anonimo));
            UsuarioAplicacao? carregado = await escopo.GerenciadorUsuarios.FindByIdAsync(idUsuarioCompartilhado.ToString());
            Assert.NotNull(carregado);
            Assert.Equal(nome, carregado.NomeExibicao);
            Assert.Equal(tenant.IdTenant, carregado.FkIdTenant);
            Assert.True(await escopo.GerenciadorUsuarios.CheckPasswordAsync(carregado, senhaCerta));
            Assert.False(await escopo.GerenciadorUsuarios.CheckPasswordAsync(carregado, senhaOutra));
        }
    }

    [Fact]
    public async Task PermissoesLocais_ConcedemERevogamIndependentemente()
    {
        Guid idUsuario = IdentificadorV7.Gerar(_relogio.GetUtcNow());
        const string Email = "teste_perm@tenant.local";

        foreach (InstalacaoTestePostgres tenant in new[] { fixture.TenantA, fixture.TenantB })
        {
            await CriarContaAsync(tenant, idUsuario, Email, "Usuário", "TesteSenha123!");
            await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(tenant.StringConexaoOperadorUm, Contexto(TipoAutor.Operador));
            escopo.Contexto.Permissoes.Add(new PermissaoUsuario { FkIdUsuario = idUsuario, CodigoPermissao = CodigosPermissao.FundacaoContextoLer });
            await escopo.Contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (EscopoIdentityTeste escopoA = EscopoIdentityTeste.Criar(fixture.TenantA.StringConexaoOperadorUm, Contexto(TipoAutor.Operador)))
        {
            PermissaoUsuario concessaoA = await escopoA.Contexto.Permissoes.SingleAsync(p => p.FkIdUsuario == idUsuario, TestContext.Current.CancellationToken);
            escopoA.Contexto.Permissoes.Remove(concessaoA);
            await escopoA.Contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using EscopoIdentityTeste leituraA = EscopoIdentityTeste.Criar(fixture.TenantA.StringConexaoExecucao, Contexto(TipoAutor.Anonimo));
        Assert.Empty(await leituraA.Contexto.Permissoes.Where(p => p.FkIdUsuario == idUsuario).ToListAsync(TestContext.Current.CancellationToken));

        await using EscopoIdentityTeste leituraB = EscopoIdentityTeste.Criar(fixture.TenantB.StringConexaoExecucao, Contexto(TipoAutor.Anonimo));
        PermissaoUsuario concessaoB = await leituraB.Contexto.Permissoes.SingleAsync(p => p.FkIdUsuario == idUsuario, TestContext.Current.CancellationToken);
        Assert.Equal(CodigosPermissao.FundacaoContextoLer, concessaoB.CodigoPermissao);
        Assert.Null(concessaoB.AlteradoEm);
        Assert.Equal(TipoAutor.Operador, concessaoB.TipoAutorCriacao);
    }

    [Fact]
    public async Task UnicidadeEmail_AplicadaLocalmentePorBanco()
    {
        const string EmailDuplicado = "duplicado@tenant.local";
        await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(fixture.TenantA.StringConexaoOperadorUm, Contexto(TipoAutor.Operador));

        IdentityResult resultado1 = await escopo.GerenciadorUsuarios.CreateAsync(NovaConta(fixture.TenantA, EmailDuplicado, "Usuário 1"), "Senha123!");
        Assert.True(resultado1.Succeeded);

        IdentityResult resultado2 = await escopo.GerenciadorUsuarios.CreateAsync(NovaConta(fixture.TenantA, EmailDuplicado, "Usuário 2"), "Senha123!");
        Assert.False(resultado2.Succeeded);
    }

    [Fact]
    public async Task RestricaoChaveEstrangeira_ImpedeContaComTenantDivergente()
    {
        await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(fixture.TenantA.StringConexaoOperadorUm, Contexto(TipoAutor.Operador));
        UsuarioAplicacao contaInvalida = NovaConta(fixture.TenantA, "divergente@tenant.local", "Usuário Tenant Inválido");
        contaInvalida.FkIdTenant = fixture.TenantB.IdTenant;

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => escopo.GerenciadorUsuarios.CreateAsync(contaInvalida, "Senha123!"));
    }

    [Fact]
    public async Task Transacoes_SuportamCommitERollback()
    {
        UsuarioAplicacao contaRollback = NovaConta(fixture.TenantA, "rollback@tenant.local", "Usuário Rollback");
        UsuarioAplicacao contaCommit = NovaConta(fixture.TenantA, "commit@tenant.local", "Usuário Commit");
        await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(fixture.TenantA.StringConexaoOperadorUm, Contexto(TipoAutor.Operador));

        await using (var transacao = await escopo.Contexto.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            escopo.Contexto.Users.Add(contaRollback);
            await escopo.Contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transacao.RollbackAsync(TestContext.Current.CancellationToken);
        }

        escopo.Contexto.ChangeTracker.Clear();
        Assert.Null(await escopo.Contexto.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == contaRollback.Id, TestContext.Current.CancellationToken));

        await using (var transacao = await escopo.Contexto.Database.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            escopo.Contexto.Users.Add(contaCommit);
            await escopo.Contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
            await transacao.CommitAsync(TestContext.Current.CancellationToken);
        }

        escopo.Contexto.ChangeTracker.Clear();
        Assert.NotNull(await escopo.Contexto.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == contaCommit.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BarreiraConcorrencia_OperacoesSobrepostasComFalhaEmADeixamBIntacto()
    {
        UsuarioAplicacao contaA = NovaConta(fixture.TenantA, "concorrente_a@tenant.local", "Concorrente A");
        UsuarioAplicacao contaB = NovaConta(fixture.TenantB, "concorrente_b@tenant.local", "Concorrente B");
        using Barrier barreira = new(2);
        int pico = 0;
        int ativos = 0;

        async Task ExecutarAsync(InstalacaoTestePostgres tenant, UsuarioAplicacao conta, bool confirmar)
        {
            await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(tenant.StringConexaoOperadorUm, Contexto(TipoAutor.Operador));
            await using var transacao = await escopo.Contexto.Database.BeginTransactionAsync(TestContext.Current.CancellationToken);
            int atual = Interlocked.Increment(ref ativos);
            InterlockedMaximo(ref pico, atual);
            escopo.Contexto.Users.Add(conta);
            await escopo.Contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
            barreira.SignalAndWait(TestContext.Current.CancellationToken);
            if (confirmar)
            {
                await transacao.CommitAsync(TestContext.Current.CancellationToken);
            }
            else
            {
                await transacao.RollbackAsync(TestContext.Current.CancellationToken);
            }

            Interlocked.Decrement(ref ativos);
        }

        await Task.WhenAll(
            Task.Run(() => ExecutarAsync(fixture.TenantA, contaA, confirmar: false), TestContext.Current.CancellationToken),
            Task.Run(() => ExecutarAsync(fixture.TenantB, contaB, confirmar: true), TestContext.Current.CancellationToken));

        Assert.True(pico >= 2, $"Pico de concorrência foi {pico}, esperado >= 2");

        await using EscopoIdentityTeste leituraA = EscopoIdentityTeste.Criar(fixture.TenantA.StringConexaoExecucao, Contexto(TipoAutor.Anonimo));
        Assert.Null(await leituraA.Contexto.Users.FindAsync([contaA.Id], TestContext.Current.CancellationToken));

        await using EscopoIdentityTeste leituraB = EscopoIdentityTeste.Criar(fixture.TenantB.StringConexaoExecucao, Contexto(TipoAutor.Anonimo));
        Assert.NotNull(await leituraB.Contexto.Users.FindAsync([contaB.Id], TestContext.Current.CancellationToken));
        Assert.Null(await leituraB.Contexto.Users.FindAsync([contaA.Id], TestContext.Current.CancellationToken));
    }

    private static void InterlockedMaximo(ref int alvo, int valor)
    {
        int atual;
        do
        {
            atual = Volatile.Read(ref alvo);
            if (valor <= atual)
            {
                return;
            }
        }
        while (Interlocked.CompareExchange(ref alvo, valor, atual) != atual);
    }

    private ContextoOperacaoTeste Contexto(TipoAutor tipo) => ContextoOperacaoTeste.Criar(_relogio, tipo);

    private UsuarioAplicacao NovaConta(InstalacaoTestePostgres tenant, string email, string nome) => new()
    {
        Id = IdentificadorV7.Gerar(_relogio.GetUtcNow()),
        FkIdTenant = tenant.IdTenant,
        UserName = email,
        Email = email,
        NomeExibicao = nome,
        EstaAtivo = true,
        LockoutEnabled = true,
    };

    private async Task CriarContaAsync(InstalacaoTestePostgres tenant, Guid id, string email, string nome, string senha)
    {
        await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(tenant.StringConexaoOperadorUm, Contexto(TipoAutor.Operador));
        UsuarioAplicacao conta = NovaConta(tenant, email, nome);
        conta.Id = id;
        IdentityResult resultado = await escopo.GerenciadorUsuarios.CreateAsync(conta, senha);
        Assert.True(resultado.Succeeded, string.Join("; ", resultado.Errors.Select(e => e.Code)));
    }
}
