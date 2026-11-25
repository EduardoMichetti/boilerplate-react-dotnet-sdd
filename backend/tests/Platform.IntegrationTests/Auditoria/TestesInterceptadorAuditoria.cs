namespace Platform.IntegrationTests.Auditoria;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
/// T078: preenchimento central de auditoria (data-model §4), token por entidade e UUID v7 do
/// instante da operação, com relógio controlável e PostgreSQL real.
/// </summary>
public sealed class TestesInterceptadorAuditoria(FixtureInstalacaoPostgres fixture)
    : IClassFixture<FixtureInstalacaoPostgres>, IAsyncLifetime
{
    private readonly RelogioControlavel _relogio = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

    private InstalacaoTestePostgres Tenant => fixture.TenantA;

    public async ValueTask InitializeAsync() =>
        await BancoTeste.MigrarAsync(Tenant, _relogio, TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Criacao_PreencheMetadadosCompletosNosTresTiposAuditaveis()
    {
        ContextoOperacaoTeste operador = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(operador);
        await ConcederAsync(operador, conta.Id);

        _relogio.Avancar(TimeSpan.FromSeconds(10));
        ContextoOperacaoTeste usuario = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Usuario, conta.Id.ToString());
        SessaoAutenticacao sessao = await EmitirSessaoAsync(usuario, conta.Id);

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(Tenant.StringConexaoProprietario);
        UsuarioAplicacao contaLida = await leitura.Users.AsNoTracking().SingleAsync(u => u.Id == conta.Id, TestContext.Current.CancellationToken);
        PermissaoUsuario concessaoLida = await leitura.Permissoes.AsNoTracking().SingleAsync(p => p.FkIdUsuario == conta.Id, TestContext.Current.CancellationToken);
        SessaoAutenticacao sessaoLida = await leitura.Sessoes.AsNoTracking().SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);

        AssertCriacao(contaLida, operador);
        AssertCriacao(concessaoLida, operador);
        AssertCriacao(sessaoLida, usuario);
        Assert.Equal(1, sessaoLida.Versao);
    }

    [Fact]
    public async Task IdsV7_TemTimestampIgualAoInstanteDaOperacao()
    {
        ContextoOperacaoTeste operador = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(operador);
        SessaoAutenticacao sessao = await EmitirSessaoAsync(operador, conta.Id);

        foreach (Guid id in new[] { conta.Id, sessao.IdSessao, operador.IdOperacao })
        {
            Assert.Equal(7, id.Version);
            Assert.Equal(operador.InstanteOperacao.ToUnixTimeMilliseconds(), MilissegundosUnix(id));
        }
    }

    [Fact]
    public async Task AlteracaoEfetiva_PreencheUltimaAlteracaoEIncrementaVersaoSomenteNaSessao()
    {
        ContextoOperacaoTeste criacao = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(criacao);
        SessaoAutenticacao sessao = await EmitirSessaoAsync(criacao, conta.Id);

        _relogio.Avancar(TimeSpan.FromMinutes(1));
        ContextoOperacaoTeste alteracao = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorDois);
        await using (EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(Tenant.StringConexaoOperadorDois, alteracao))
        {
            UsuarioAplicacao carregada = (await escopo.GerenciadorUsuarios.FindByIdAsync(conta.Id.ToString()))!;
            carregada.EstaAtivo = false;
            Assert.True((await escopo.GerenciadorUsuarios.UpdateAsync(carregada)).Succeeded);

            SessaoAutenticacao sessaoCarregada = await escopo.Contexto.Sessoes.SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);
            sessaoCarregada.RevogadaEm = alteracao.InstanteOperacao;
            sessaoCarregada.TicketProtegido = null;
            await escopo.Contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(Tenant.StringConexaoProprietario);
        UsuarioAplicacao contaLida = await leitura.Users.AsNoTracking().SingleAsync(u => u.Id == conta.Id, TestContext.Current.CancellationToken);
        SessaoAutenticacao sessaoLida = await leitura.Sessoes.AsNoTracking().SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);

        AssertCriacao(contaLida, criacao);
        AssertAlteracao(contaLida, alteracao);
        AssertCriacao(sessaoLida, criacao);
        AssertAlteracao(sessaoLida, alteracao);
        Assert.Equal(2, sessaoLida.Versao);
        Assert.Equal(alteracao.InstanteOperacao, sessaoLida.RevogadaEm);
    }

    [Fact]
    public async Task NoOp_NaoAlteraMetadadosNemVersao()
    {
        ContextoOperacaoTeste criacao = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(criacao);
        SessaoAutenticacao sessao = await EmitirSessaoAsync(criacao, conta.Id);

        _relogio.Avancar(TimeSpan.FromMinutes(1));
        ContextoOperacaoTeste noop = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        await using (AcessoTenantDbContext contexto = BancoTeste.CriarContexto(Tenant.StringConexaoOperadorUm, noop))
        {
            SessaoAutenticacao carregada = await contexto.Sessoes.SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);
            carregada.TicketProtegido = [.. carregada.TicketProtegido!];
            contexto.Entry(carregada).State = EntityState.Modified;
            await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(Tenant.StringConexaoProprietario);
        SessaoAutenticacao lida = await leitura.Sessoes.AsNoTracking().SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);
        Assert.Null(lida.AlteradoEm);
        Assert.Equal(1, lida.Versao);
    }

    [Fact]
    public async Task SomenteConcurrencyStampDaConta_NaoContaComoAlteracaoEfetiva()
    {
        ContextoOperacaoTeste criacao = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(criacao);
        string carimboOriginal = conta.ConcurrencyStamp!;

        _relogio.Avancar(TimeSpan.FromMinutes(1));
        ContextoOperacaoTeste noop = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        await using (EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(Tenant.StringConexaoOperadorUm, noop))
        {
            UsuarioAplicacao carregada = (await escopo.GerenciadorUsuarios.FindByIdAsync(conta.Id.ToString()))!;
            Assert.True((await escopo.GerenciadorUsuarios.UpdateAsync(carregada)).Succeeded);
        }

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(Tenant.StringConexaoProprietario);
        UsuarioAplicacao lida = await leitura.Users.AsNoTracking().SingleAsync(u => u.Id == conta.Id, TestContext.Current.CancellationToken);
        Assert.NotEqual(carimboOriginal, lida.ConcurrencyStamp);
        Assert.Null(lida.AlteradoEm);
        Assert.Null(lida.IdOperacaoAlteracao);
    }

    [Fact]
    public async Task MetadadosDeCriacao_SaoImutaveis()
    {
        ContextoOperacaoTeste criacao = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(criacao);
        SessaoAutenticacao sessao = await EmitirSessaoAsync(criacao, conta.Id);

        await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(Tenant.StringConexaoOperadorUm, ContextoOperacaoTeste.Criar(_relogio));
        SessaoAutenticacao carregada = await contexto.Sessoes.SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);
        carregada.CriadoEm = carregada.CriadoEm.AddMinutes(-1);

        await Assert.ThrowsAsync<InvalidOperationException>(() => contexto.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SessaoCriada_GravaUltimoUsoIgualACriacaoETetoEJanela()
    {
        ContextoOperacaoTeste criacao = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(criacao);
        SessaoAutenticacao sessao = await EmitirSessaoAsync(criacao, conta.Id);

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(Tenant.StringConexaoProprietario);
        SessaoAutenticacao lida = await leitura.Sessoes.AsNoTracking().SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);

        Assert.Equal(lida.CriadoEm, lida.UltimoUsoEm);
        Assert.Equal(criacao.InstanteOperacao.AddMinutes(600), lida.TetoAbsolutoEm);
        Assert.Equal(30, lida.JanelaInatividadeMinutos);
        Assert.Null(lida.AlteradoEm);
        Assert.Equal(1, lida.Versao);
    }

    [Fact]
    public async Task RegistroDeUso_SoUltimoUsoEm_EhAlteracaoEfetiva()
    {
        ContextoOperacaoTeste criacao = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Usuario, Guid.NewGuid().ToString());
        ContextoOperacaoTeste operador = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(operador);
        SessaoAutenticacao sessao = await EmitirSessaoAsync(criacao, conta.Id);

        _relogio.Avancar(TimeSpan.FromMinutes(2));
        ContextoOperacaoTeste uso = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Usuario, conta.Id.ToString());
        await using (AcessoTenantDbContext contexto = BancoTeste.CriarContexto(Tenant.StringConexaoExecucao, uso))
        {
            SessaoAutenticacao carregada = await contexto.Sessoes.SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);
            carregada.RegistrarUso(uso.InstanteOperacao);
            await contexto.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(Tenant.StringConexaoProprietario);
        SessaoAutenticacao lida = await leitura.Sessoes.AsNoTracking().SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);
        Assert.Equal(uso.InstanteOperacao, lida.UltimoUsoEm);
        Assert.Equal(2, lida.Versao);
        AssertCriacao(lida, criacao);
        AssertAlteracao(lida, uso);
        Assert.Equal(sessao.TetoAbsolutoEm, lida.TetoAbsolutoEm);
        Assert.Equal(sessao.JanelaInatividadeMinutos, lida.JanelaInatividadeMinutos);
        Assert.Null(lida.RevogadaEm);
        Assert.Equal(sessao.TicketProtegido, lida.TicketProtegido);
    }

    [Fact]
    public async Task MudancaDeTetoOuJanelaAposCriacao_Rejeitada()
    {
        ContextoOperacaoTeste criacao = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(criacao);
        SessaoAutenticacao sessao = await EmitirSessaoAsync(criacao, conta.Id);

        _relogio.Avancar(TimeSpan.FromMinutes(1));
        foreach (Action<SessaoAutenticacao> mudanca in new Action<SessaoAutenticacao>[]
        {
            s => s.TetoAbsolutoEm = s.TetoAbsolutoEm.AddMinutes(10),
            s => s.JanelaInatividadeMinutos += 5,
        })
        {
            await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(Tenant.StringConexaoOperadorUm, ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm));
            SessaoAutenticacao carregada = await contexto.Sessoes.SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);
            mudanca(carregada);
            await Assert.ThrowsAsync<InvalidOperationException>(() => contexto.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(Tenant.StringConexaoProprietario);
        SessaoAutenticacao lida = await leitura.Sessoes.AsNoTracking().SingleAsync(s => s.IdSessao == sessao.IdSessao, TestContext.Current.CancellationToken);
        Assert.Equal(sessao.TetoAbsolutoEm, lida.TetoAbsolutoEm);
        Assert.Equal(sessao.JanelaInatividadeMinutos, lida.JanelaInatividadeMinutos);
        Assert.Equal(1, lida.Versao);
    }

    [Fact]
    public async Task ConcessaoModificada_EHistoricoAlteradoOuRemovido_SaoRejeitados()
    {
        ContextoOperacaoTeste operador = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(operador);
        await ConcederAsync(operador, conta.Id);

        await using (AcessoTenantDbContext contexto = BancoTeste.CriarContexto(Tenant.StringConexaoOperadorUm, operador))
        {
            PermissaoUsuario concessao = await contexto.Permissoes.SingleAsync(p => p.FkIdUsuario == conta.Id, TestContext.Current.CancellationToken);
            contexto.Entry(concessao).State = EntityState.Modified;
            await Assert.ThrowsAsync<InvalidOperationException>(() => contexto.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        RegistroOperacaoAcesso registro = NovoRegistro(operador);
        foreach (EntityState estado in new[] { EntityState.Modified, EntityState.Deleted })
        {
            await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(Tenant.StringConexaoOperadorUm, operador);
            contexto.Attach(registro);
            contexto.Entry(registro).State = estado;
            await Assert.ThrowsAsync<InvalidOperationException>(() => contexto.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task FalhaDeAuditoria_NaoConfirmaODado()
    {
        ContextoOperacaoTeste operador = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(operador);
        await ConcederAsync(operador, conta.Id);
        Guid idSessaoNova = IdentificadorV7.Gerar(operador.InstanteOperacao);

        await using (AcessoTenantDbContext contexto = BancoTeste.CriarContexto(Tenant.StringConexaoOperadorUm, operador))
        {
            contexto.Sessoes.Add(NovaSessao(idSessaoNova, conta.Id, operador));
            PermissaoUsuario concessao = await contexto.Permissoes.SingleAsync(p => p.FkIdUsuario == conta.Id, TestContext.Current.CancellationToken);
            contexto.Entry(concessao).State = EntityState.Modified;
            await Assert.ThrowsAsync<InvalidOperationException>(() => contexto.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(Tenant.StringConexaoProprietario);
        Assert.False(await leitura.Sessoes.AnyAsync(s => s.IdSessao == idSessaoNova, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AutorAusenteParaTipoNaoAnonimo_FalhaAntesDoBancoSemGravar()
    {
        // Regressão: host operacional gravava TipoAutor=Operador com Autor nulo e só a constraint
        // ck_*_auditoria_criacao barrava, mascarada como conflito pelo caso de uso.
        ContextoOperacaoTeste semAutor = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, autor: null);
        ContextoOperacaoTeste operador = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Operador, Tenant.LoginOperadorUm);
        UsuarioAplicacao conta = await CriarContaAsync(operador);

        await using (AcessoTenantDbContext contexto = BancoTeste.CriarContexto(Tenant.StringConexaoOperadorUm, semAutor))
        {
            contexto.Permissoes.Add(new PermissaoUsuario { FkIdUsuario = conta.Id, CodigoPermissao = CodigosPermissao.FundacaoContextoLer });
            InvalidOperationException erro = await Assert.ThrowsAsync<InvalidOperationException>(
                () => contexto.SaveChangesAsync(TestContext.Current.CancellationToken));
            Assert.Contains("exige autor verificado", erro.Message, StringComparison.Ordinal);
        }

        await using AcessoTenantDbContext leitura = BancoTeste.CriarContexto(Tenant.StringConexaoProprietario);
        Assert.False(await leitura.Permissoes.AnyAsync(p => p.FkIdUsuario == conta.Id, TestContext.Current.CancellationToken));
    }

    private static void AssertCriacao(IEntidadeAuditavel entidade, ContextoOperacaoTeste contexto)
    {
        Assert.Equal(contexto.InstanteOperacao, entidade.CriadoEm);
        Assert.Equal(TimeSpan.Zero, entidade.CriadoEm.Offset);
        Assert.Equal(contexto.TipoAutor, entidade.TipoAutorCriacao);
        Assert.Equal(contexto.Autor, entidade.AutorCriacao);
        Assert.Equal(contexto.IdOperacao, entidade.IdOperacaoCriacao);
    }

    private static void AssertAlteracao(IEntidadeAuditavel entidade, ContextoOperacaoTeste contexto)
    {
        Assert.Equal(contexto.InstanteOperacao, entidade.AlteradoEm);
        Assert.Equal(contexto.TipoAutor, entidade.TipoAutorAlteracao);
        Assert.Equal(contexto.Autor, entidade.AutorAlteracao);
        Assert.Equal(contexto.IdOperacao, entidade.IdOperacaoAlteracao);
    }

    private static long MilissegundosUnix(Guid id)
    {
        byte[] bytes = id.ToByteArray(bigEndian: true);
        long valor = 0;
        for (int i = 0; i < 6; i++)
        {
            valor = (valor << 8) | bytes[i];
        }

        return valor;
    }

    private RegistroOperacaoAcesso NovoRegistro(ContextoOperacaoTeste operador) => new()
    {
        IdOperacao = operador.IdOperacao,
        IdTenant = Tenant.IdTenant,
        IdAmbiente = Tenant.IdAmbiente,
        LoginOperador = Tenant.LoginOperadorUm,
        TipoOperacao = TipoOperacaoAcesso.RevogarSessoes,
        RegistradaEm = operador.InstanteOperacao,
        Resultado = ResultadoOperacaoAcesso.Sucesso,
        CodigoResultado = "SUCESSO",
    };

    private static SessaoAutenticacao NovaSessao(Guid idSessao, Guid idUsuario, ContextoOperacaoTeste contexto) => new()
    {
        IdSessao = idSessao,
        FkIdUsuario = idUsuario,
        EpocaAcesso = IdentificadorV7.Gerar(contexto.InstanteOperacao),
        TicketProtegido = [9, 8, 7],
        TetoAbsolutoEm = contexto.InstanteOperacao.AddMinutes(600),
        JanelaInatividadeMinutos = 30,
        UltimoUsoEm = contexto.InstanteOperacao,
    };

    private async Task<UsuarioAplicacao> CriarContaAsync(ContextoOperacaoTeste contexto)
    {
        await using EscopoIdentityTeste escopo = EscopoIdentityTeste.Criar(Tenant.StringConexaoOperadorUm, contexto);
        string email = $"auditoria_{IdentificadorV7.Gerar(contexto.InstanteOperacao):N}@tenant.local";
        UsuarioAplicacao conta = new()
        {
            Id = IdentificadorV7.Gerar(contexto.InstanteOperacao),
            FkIdTenant = Tenant.IdTenant,
            UserName = email,
            Email = email,
            NomeExibicao = "Conta auditada",
            LockoutEnabled = true,
        };
        IdentityResult resultado = await escopo.GerenciadorUsuarios.CreateAsync(conta, "SenhaSegura123!");
        Assert.True(resultado.Succeeded);
        return conta;
    }

    private async Task ConcederAsync(ContextoOperacaoTeste contexto, Guid idUsuario)
    {
        await using AcessoTenantDbContext banco = BancoTeste.CriarContexto(Tenant.StringConexaoOperadorUm, contexto);
        banco.Permissoes.Add(new PermissaoUsuario { FkIdUsuario = idUsuario, CodigoPermissao = CodigosPermissao.FundacaoContextoLer });
        await banco.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<SessaoAutenticacao> EmitirSessaoAsync(ContextoOperacaoTeste contexto, Guid idUsuario)
    {
        SessaoAutenticacao sessao = NovaSessao(IdentificadorV7.Gerar(contexto.InstanteOperacao), idUsuario, contexto);
        await using AcessoTenantDbContext banco = BancoTeste.CriarContexto(Tenant.StringConexaoExecucao, contexto);
        banco.Sessoes.Add(sessao);
        await banco.SaveChangesAsync(TestContext.Current.CancellationToken);
        return sessao;
    }
}
