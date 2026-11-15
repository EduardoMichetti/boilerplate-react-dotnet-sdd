namespace Platform.IntegrationTests.Operacoes;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Access.Operacoes;
using Platform.Application.Auditoria;
using Platform.Domain.Access;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Infrastructure.Access;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T110: Testes de integração dos casos de uso de contas e permissões do host operacional (FR-033).
/// </summary>
public sealed class TestesOperacoesContaPermissao(FixtureInstalacaoPostgres fixture)
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
    public async Task CriarConta_ComParametrosValidos_CriaContaSemPermissoesEComAuditoria()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        const string Email = "novo_usuario@tenant.local";
        const string Nome = "Usuário Novo";
        const string SenhaValida = "SenhaValida123!";

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, SenhaValida);

        CasoUsoCriarConta casoUso = escopo.Servicos.GetRequiredService<CasoUsoCriarConta>();
        ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, Email, Nome, cancelamento);

        Assert.True(resultado.Sucesso, $"Criar falhou: {resultado.Codigo} - {resultado.MensagemDiagnostico}");
        Assert.Equal("SUCESSO", resultado.Codigo);
        Assert.NotNull(resultado.IdUsuario);
        Assert.Equal(TipoOperacaoAcesso.CriarConta, resultado.Operacao);
        Assert.Equal(tenant.LoginOperadorUm, resultado.LoginOperador);

        // Verifica que o ID gerado é UUID v7
        Assert.Equal(7, resultado.IdUsuario!.Value.Version);

        // Verifica que a conta existe e não possui permissões automáticas
        UsuarioAplicacao? usuario = await escopo.Contexto.Users
            .Include(u => u.Permissoes)
            .FirstOrDefaultAsync(u => u.Id == resultado.IdUsuario.Value, cancelamento);
        Assert.NotNull(usuario);
        Assert.Equal(Email, usuario.Email);
        Assert.Equal(Nome, usuario.NomeExibicao);
        Assert.Empty(usuario.Permissoes);

        // Autoria vem do login verificado na conexão, não da fixture
        Assert.Equal(TipoAutor.Operador, usuario.TipoAutorCriacao);
        Assert.Equal(tenant.LoginOperadorUm, usuario.AutorCriacao);
        Assert.Equal(resultado.IdOperacao, usuario.IdOperacaoCriacao);

        // Verifica registro de auditoria persistido com Sucesso (leitura via owner pois operador só tem INSERT em operacoes_acesso)
        await using AcessoTenantDbContext contextoOwner = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
        RegistroOperacaoAcesso? registro = await contextoOwner.OperacoesAcesso
            .FirstOrDefaultAsync(r => r.IdOperacao == resultado.IdOperacao, cancelamento);
        Assert.NotNull(registro);
        Assert.Equal(ResultadoOperacaoAcesso.Sucesso, registro.Resultado);
        Assert.Equal(tenant.LoginOperadorUm, registro.LoginOperador);
        Assert.Equal(resultado.IdUsuario, registro.IdUsuarioAlvo);
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("semnumerooumaiuscula!")]
    [InlineData("SEMNUMEROMINUSCULA!")]
    [InlineData("SemNaoAlfanumerico123")]
    public async Task CriarConta_ComSenhaForaDaPolitica_FalhaAntesDePersistir(string senhaInvalida)
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        const string Email = "senha_fraca@tenant.local";

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, senhaInvalida);

        CasoUsoCriarConta casoUso = escopo.Servicos.GetRequiredService<CasoUsoCriarConta>();
        ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, Email, "Nome", cancelamento);

        Assert.False(resultado.Sucesso);
        Assert.Equal("SENHA_FORA_DA_POLITICA", resultado.Codigo);

        // Nenhuma conta foi criada
        bool existeConta = await escopo.Contexto.Users.AnyAsync(u => u.Email == Email, cancelamento);
        Assert.False(existeConta);

        // Registro de falha auditado em transação separada (lido via owner)
        await using AcessoTenantDbContext contextoOwner = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
        RegistroOperacaoAcesso? registro = await contextoOwner.OperacoesAcesso
            .FirstOrDefaultAsync(r => r.IdOperacao == resultado.IdOperacao, cancelamento);
        Assert.NotNull(registro);
        Assert.Equal(ResultadoOperacaoAcesso.Falha, registro.Resultado);
        Assert.Equal("SENHA_FORA_DA_POLITICA", registro.CodigoResultado);
    }

    [Fact]
    public async Task DesativarConta_RevogaTodasSessoesEAtualizaSecurityStamp()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        (Guid idUsuario, string stampOriginal) = await CriarUsuarioESessaoAsync(tenant, cancelamento);

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorDois, tenant.LoginOperadorDois, _relogio);

        CasoUsoDesativarConta casoUso = escopo.Servicos.GetRequiredService<CasoUsoDesativarConta>();
        ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, cancelamento);

        Assert.True(resultado.Sucesso, $"Desativar falhou: {resultado.Codigo} - {resultado.MensagemDiagnostico}");
        Assert.Equal(tenant.LoginOperadorDois, resultado.LoginOperador);

        UsuarioAplicacao usuario = await escopo.Contexto.Users.AsNoTracking().SingleAsync(u => u.Id == idUsuario, cancelamento);
        Assert.False(usuario.EstaAtivo);
        Assert.NotEqual(stampOriginal, usuario.SecurityStamp);
        Assert.Equal(TipoAutor.Operador, usuario.TipoAutorAlteracao);
        Assert.Equal(tenant.LoginOperadorDois, usuario.AutorAlteracao);
        Assert.Equal(resultado.IdOperacao, usuario.IdOperacaoAlteracao);

        // Todas as sessões devem estar revogadas
        List<SessaoAutenticacao> sessoes = await escopo.Contexto.Sessoes.Where(s => s.FkIdUsuario == idUsuario).ToListAsync(cancelamento);
        Assert.NotEmpty(sessoes);
        Assert.All(sessoes, s => Assert.NotNull(s.RevogadaEm));
    }

    [Fact]
    public async Task ReativarConta_AtivaContaSemRessuscitarSessoes()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        (Guid idUsuario, _) = await CriarUsuarioESessaoAsync(tenant, cancelamento);

        // Primeiro desativa
        await using (EscopoOperacoesTeste escopoDesativar = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio))
        {
            ResultadoOperacao resDesat = await escopoDesativar.Servicos.GetRequiredService<CasoUsoDesativarConta>()
                .ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, cancelamento);
            Assert.True(resDesat.Sucesso, $"Desativar preparatório falhou: {resDesat.Codigo} - {resDesat.MensagemDiagnostico}");
        }

        // Agora reativa
        await using (EscopoOperacoesTeste escopoReativar = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio))
        {
            CasoUsoReativarConta casoUso = escopoReativar.Servicos.GetRequiredService<CasoUsoReativarConta>();
            ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, cancelamento);

            Assert.True(resultado.Sucesso, $"Reativar falhou: {resultado.Codigo} - {resultado.MensagemDiagnostico}");

            UsuarioAplicacao usuario = await escopoReativar.Contexto.Users.AsNoTracking().SingleAsync(u => u.Id == idUsuario, cancelamento);
            Assert.True(usuario.EstaAtivo);

            // Sessões anteriores continuam revogadas
            List<SessaoAutenticacao> sessoes = await escopoReativar.Contexto.Sessoes.Where(s => s.FkIdUsuario == idUsuario).ToListAsync(cancelamento);
            Assert.All(sessoes, s => Assert.NotNull(s.RevogadaEm));
        }
    }

    [Fact]
    public async Task RedefinirSenha_RemoveLockoutRevogaSessoesEAtualizaStamp()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        (Guid idUsuario, string stampOriginal) = await CriarUsuarioESessaoAsync(tenant, cancelamento);

        // Provoca lockout na conta
        await using (EscopoOperacoesTeste escopoLock = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio))
        {
            // Preparo direto pelo UserManager, fora de um caso de uso: declara o operador como o
            // verificador faria, pois gravação auditável sem autor verificado é rejeitada.
            escopoLock.Servicos.GetRequiredService<IRegistroOperadorVerificado>().DefinirOperadorVerificado(tenant.LoginOperadorUm);
            UsuarioAplicacao usuarioLock = await escopoLock.Contexto.Users.SingleAsync(u => u.Id == idUsuario, cancelamento);
            GerenciadorUsuariosAplicacao mgr = escopoLock.Servicos.GetRequiredService<GerenciadorUsuariosAplicacao>();
            for (int i = 0; i < 5; i++)
            {
                await mgr.AccessFailedAsync(usuarioLock);
            }

            Assert.True(await mgr.IsLockedOutAsync(usuarioLock));
        }

        const string NovaSenha = "NovaSenhaValida456!";
        await using (EscopoOperacoesTeste escopoReset = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, NovaSenha))
        {
            CasoUsoRedefinirSenha casoUso = escopoReset.Servicos.GetRequiredService<CasoUsoRedefinirSenha>();
            ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, cancelamento);

            Assert.True(resultado.Sucesso, $"Redefinir falhou: {resultado.Codigo} - {resultado.MensagemDiagnostico}");

            UsuarioAplicacao usuarioAtual = await escopoReset.Contexto.Users.AsNoTracking().SingleAsync(u => u.Id == idUsuario, cancelamento);
            Assert.Equal(0, usuarioAtual.AccessFailedCount);
            Assert.Null(usuarioAtual.LockoutEnd);
            Assert.NotEqual(stampOriginal, usuarioAtual.SecurityStamp);

            GerenciadorUsuariosAplicacao mgr = escopoReset.Servicos.GetRequiredService<GerenciadorUsuariosAplicacao>();
            Assert.False(await mgr.IsLockedOutAsync(usuarioAtual));
            Assert.True(await mgr.CheckPasswordAsync(usuarioAtual, NovaSenha));

            List<SessaoAutenticacao> sessoes = await escopoReset.Contexto.Sessoes.Where(s => s.FkIdUsuario == idUsuario).ToListAsync(cancelamento);
            Assert.All(sessoes, s => Assert.NotNull(s.RevogadaEm));
        }
    }

    [Fact]
    public async Task RevogarSessoes_RevogaTodasSessoesEAtualizaStamp()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        (Guid idUsuario, string stampOriginal) = await CriarUsuarioESessaoAsync(tenant, cancelamento);

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio);

        CasoUsoRevogarSessoes casoUso = escopo.Servicos.GetRequiredService<CasoUsoRevogarSessoes>();
        ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, cancelamento);

        Assert.True(resultado.Sucesso, $"Revogar falhou: {resultado.Codigo} - {resultado.MensagemDiagnostico}");

        UsuarioAplicacao usuario = await escopo.Contexto.Users.AsNoTracking().SingleAsync(u => u.Id == idUsuario, cancelamento);
        Assert.NotEqual(stampOriginal, usuario.SecurityStamp);

        List<SessaoAutenticacao> sessoes = await escopo.Contexto.Sessoes.Where(s => s.FkIdUsuario == idUsuario).ToListAsync(cancelamento);
        Assert.All(sessoes, s => Assert.NotNull(s.RevogadaEm));
    }

    [Fact]
    public async Task ConcederPermissao_GravaAutoriaDoOperadorVerificado()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        (Guid idUsuario, _) = await CriarUsuarioESessaoAsync(tenant, cancelamento);

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorDois, tenant.LoginOperadorDois, _relogio);
        ResultadoOperacao resultado = await escopo.Servicos.GetRequiredService<CasoUsoConcederPermissao>()
            .ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, CodigosPermissao.FundacaoContextoLer, cancelamento);
        Assert.True(resultado.Sucesso, $"Conceder falhou: {resultado.Codigo} - {resultado.MensagemDiagnostico}");

        await using AcessoTenantDbContext contextoOwner = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
        PermissaoUsuario concessao = await contextoOwner.Permissoes.AsNoTracking()
            .SingleAsync(p => p.FkIdUsuario == idUsuario, cancelamento);
        Assert.Equal(TipoAutor.Operador, concessao.TipoAutorCriacao);
        Assert.Equal(tenant.LoginOperadorDois, concessao.AutorCriacao);
        Assert.Equal(resultado.IdOperacao, concessao.IdOperacaoCriacao);
    }

    [Fact]
    public async Task ConcessaoDuplicada_ERevogacaoAusente_SaoNoOpAuditadoComSucesso()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;
        (Guid idUsuario, _) = await CriarUsuarioESessaoAsync(tenant, cancelamento);

        // Primeira concessão
        await using (EscopoOperacoesTeste escopo1 = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio))
        {
            CasoUsoConcederPermissao conceder = escopo1.Servicos.GetRequiredService<CasoUsoConcederPermissao>();
            ResultadoOperacao r1 = await conceder.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, CodigosPermissao.FundacaoContextoLer, cancelamento);
            Assert.True(r1.Sucesso, $"r1 falhou: {r1.Codigo} - {r1.MensagemDiagnostico}");
        }

        // Concessão duplicada (no-op auditado com sucesso)
        await using (EscopoOperacoesTeste escopo2 = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio))
        {
            CasoUsoConcederPermissao conceder = escopo2.Servicos.GetRequiredService<CasoUsoConcederPermissao>();
            ResultadoOperacao r2 = await conceder.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, CodigosPermissao.FundacaoContextoLer, cancelamento);
            Assert.True(r2.Sucesso, $"r2 falhou: {r2.Codigo} - {r2.MensagemDiagnostico}");
        }

        // Primeira revogação
        await using (EscopoOperacoesTeste escopo3 = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio))
        {
            CasoUsoRevogarPermissao revogar = escopo3.Servicos.GetRequiredService<CasoUsoRevogarPermissao>();
            ResultadoOperacao r3 = await revogar.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, CodigosPermissao.FundacaoContextoLer, cancelamento);
            Assert.True(r3.Sucesso, $"r3 falhou: {r3.Codigo} - {r3.MensagemDiagnostico}");
        }

        // Revogação de permissão que não possui (no-op auditado com sucesso)
        await using (EscopoOperacoesTeste escopo4 = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio))
        {
            CasoUsoRevogarPermissao revogar = escopo4.Servicos.GetRequiredService<CasoUsoRevogarPermissao>();
            ResultadoOperacao r4 = await revogar.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, idUsuario, CodigosPermissao.FundacaoContextoLer, cancelamento);
            Assert.True(r4.Sucesso, $"r4 falhou: {r4.Codigo} - {r4.MensagemDiagnostico}");
        }

        // Os quatro eventos foram auditados (lidos via owner)
        await using AcessoTenantDbContext contextoOwner = BancoTeste.CriarContexto(tenant.StringConexaoProprietario);
        int eventos = await contextoOwner.OperacoesAcesso
            .CountAsync(r => r.IdUsuarioAlvo == idUsuario && r.CodigoPermissao == CodigosPermissao.FundacaoContextoLer, cancelamento);
        Assert.Equal(4, eventos);
    }

    [Fact]
    public async Task ConexaoRuntimeOuOwner_RetornaNegadoAntesDeMutacao()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;

        // Testa com conexão de runtime
        await using (EscopoOperacoesTeste escopoRuntime = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoExecucao, tenant.PapelExecucao, _relogio))
        {
            CasoUsoCriarConta casoUso = escopoRuntime.Servicos.GetRequiredService<CasoUsoCriarConta>();
            ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, "teste@runtime.local", "Nome", cancelamento);

            Assert.False(resultado.Sucesso);
            Assert.Equal(ResultadoOperacaoAcesso.Negado, resultado.Resultado);
            Assert.Equal("AUTORIZACAO_NEGADA", resultado.Codigo);
        }

        // Testa com conexão de owner
        await using (EscopoOperacoesTeste escopoOwner = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoProprietario, tenant.PapelProprietario, _relogio))
        {
            CasoUsoCriarConta casoUso = escopoOwner.Servicos.GetRequiredService<CasoUsoCriarConta>();
            ResultadoOperacao resultado = await casoUso.ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, "teste@owner.local", "Nome", cancelamento);

            Assert.False(resultado.Sucesso);
            Assert.Equal(ResultadoOperacaoAcesso.Negado, resultado.Resultado);
            Assert.Equal("AUTORIZACAO_NEGADA", resultado.Codigo);
        }
    }

    [Fact]
    public async Task AlvoDivergente_RetornaNegado()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenant = fixture.TenantA;

        await using EscopoOperacoesTeste escopo = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio);

        CasoUsoCriarConta casoUso = escopo.Servicos.GetRequiredService<CasoUsoCriarConta>();

        // IdTenant divergente (passando o do tenant B no banco de A)
        ResultadoOperacao resultado = await casoUso.ExecutarAsync(fixture.TenantB.IdTenant, tenant.IdAmbiente, "teste@divergente.local", "Nome", cancelamento);

        Assert.False(resultado.Sucesso);
        Assert.Equal(ResultadoOperacaoAcesso.Negado, resultado.Resultado);
        Assert.Equal("ALVO_DIVERGENTE", resultado.Codigo);
    }

    [Fact]
    public async Task OperacoesEmTenantA_NaoAfetamContasOuPermissoesEmTenantB()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        InstalacaoTestePostgres tenantA = fixture.TenantA;
        InstalacaoTestePostgres tenantB = fixture.TenantB;
        const string EmailComum = "isolamento@tenant.local";

        Guid idA;
        Guid idB;

        // Cria conta em A
        await using (EscopoOperacoesTeste escopoA = EscopoOperacoesTeste.Criar(
            tenantA, tenantA.StringConexaoOperadorUm, tenantA.LoginOperadorUm, _relogio, "SenhaTenantA123!"))
        {
            ResultadoOperacao resA = await escopoA.Servicos.GetRequiredService<CasoUsoCriarConta>()
                .ExecutarAsync(tenantA.IdTenant, tenantA.IdAmbiente, EmailComum, "Usuário A", cancelamento);
            Assert.True(resA.Sucesso, $"Criar A falhou: {resA.Codigo} - {resA.MensagemDiagnostico}");
            idA = resA.IdUsuario!.Value;
        }

        // Cria conta em B
        await using (EscopoOperacoesTeste escopoB = EscopoOperacoesTeste.Criar(
            tenantB, tenantB.StringConexaoOperadorUm, tenantB.LoginOperadorUm, _relogio, "SenhaTenantB456!"))
        {
            ResultadoOperacao resB = await escopoB.Servicos.GetRequiredService<CasoUsoCriarConta>()
                .ExecutarAsync(tenantB.IdTenant, tenantB.IdAmbiente, EmailComum, "Usuário B", cancelamento);
            Assert.True(resB.Sucesso, $"Criar B falhou: {resB.Codigo} - {resB.MensagemDiagnostico}");
            idB = resB.IdUsuario!.Value;
        }

        // Desativa conta em A
        await using (EscopoOperacoesTeste escopoA = EscopoOperacoesTeste.Criar(
            tenantA, tenantA.StringConexaoOperadorUm, tenantA.LoginOperadorUm, _relogio))
        {
            ResultadoOperacao resDesativar = await escopoA.Servicos.GetRequiredService<CasoUsoDesativarConta>()
                .ExecutarAsync(tenantA.IdTenant, tenantA.IdAmbiente, idA, cancelamento);
            Assert.True(resDesativar.Sucesso, $"Desativar A falhou: {resDesativar.Codigo} - {resDesativar.MensagemDiagnostico}");
        }

        // Conta em B continua ativa e intacta
        await using (EscopoOperacoesTeste escopoB = EscopoOperacoesTeste.Criar(
            tenantB, tenantB.StringConexaoOperadorUm, tenantB.LoginOperadorUm, _relogio))
        {
            UsuarioAplicacao usuarioB = await escopoB.Contexto.Users.AsNoTracking().SingleAsync(u => u.Id == idB, cancelamento);
            Assert.True(usuarioB.EstaAtivo);
            Assert.Equal("Usuário B", usuarioB.NomeExibicao);
        }
    }

    private async Task<(Guid IdUsuario, string SecurityStamp)> CriarUsuarioESessaoAsync(InstalacaoTestePostgres tenant, CancellationToken cancelamento)
    {
        string email = $"user_{Guid.NewGuid():N}@tenant.local";
        Guid idUsuario;

        await using (EscopoOperacoesTeste escopoCriar = EscopoOperacoesTeste.Criar(
            tenant, tenant.StringConexaoOperadorUm, tenant.LoginOperadorUm, _relogio, "SenhaSegura123!"))
        {
            ResultadoOperacao res = await escopoCriar.Servicos.GetRequiredService<CasoUsoCriarConta>()
                .ExecutarAsync(tenant.IdTenant, tenant.IdAmbiente, email, "Nome", cancelamento);
            Assert.True(res.Sucesso, $"CriarUsuarioESessao falhou: {res.Codigo} - {res.MensagemDiagnostico}");
            idUsuario = res.IdUsuario!.Value;
        }

        string securityStamp;
        await using (AcessoTenantDbContext contextoLeitura = BancoTeste.CriarContexto(tenant.StringConexaoOperadorUm))
        {
            UsuarioAplicacao usuario = await contextoLeitura.Users.SingleAsync(u => u.Id == idUsuario, cancelamento);
            securityStamp = usuario.SecurityStamp!;
        }

        // Sessão inserida com conexão de runtime (única com permissão de INSERT em sessoes_autenticacao)
        ContextoOperacaoTeste contextoRuntime = ContextoOperacaoTeste.Criar(_relogio, TipoAutor.Sistema, "api_runtime");
        await using (AcessoTenantDbContext contextoSessao = BancoTeste.CriarContexto(tenant.StringConexaoExecucao, contextoRuntime))
        {
            SessaoAutenticacao sessao = new()
            {
                IdSessao = IdentificadorV7.Gerar(_relogio.GetUtcNow()),
                FkIdUsuario = idUsuario,
                EpocaAcesso = Guid.NewGuid(),
                ExpiraEm = _relogio.GetUtcNow().AddHours(1),
                TicketProtegido = [1, 2, 3],
            };
            contextoSessao.Sessoes.Add(sessao);
            await contextoSessao.SaveChangesAsync(cancelamento);

            return (idUsuario, securityStamp);
        }
    }
}
