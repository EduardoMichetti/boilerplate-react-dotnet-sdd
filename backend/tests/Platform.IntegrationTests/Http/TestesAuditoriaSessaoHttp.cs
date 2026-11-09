namespace Platform.IntegrationTests.Http;

using System.Net;
using Microsoft.EntityFrameworkCore;
using Platform.Domain.Auditoria;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Platform.IntegrationTests.Suporte;
using Xunit;

/// <summary>
/// T094: autoria das gravações feitas pela API — sessão criada pelo usuário só após a credencial
/// verificada, falha de senha anônima (email nunca vira autor), revogação com <c>AlteradoEm</c> =
/// <c>RevogadaEm</c> e nenhum registro com autoria <c>Sistema</c>.
/// </summary>
public sealed class TestesAuditoriaSessaoHttp(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    [Fact]
    public async Task SessaoCriadaPeloUsuario_FalhaAnonima_RevogacaoComMesmoInstante()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("auditoria");
        Guid id = await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);

        Relogio.Avancar(TimeSpan.FromMinutes(1));
        DateTimeOffset instanteFalha = Relogio.GetUtcNow();
        using (HttpResponseMessage falha = await cliente.LoginAsync(email, "SenhaErrada#2026x"))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, falha.StatusCode);
        }

        UsuarioAplicacao aposFalha = await ContasSinteticas.ContaAsync(A, id);
        Assert.Equal(1, aposFalha.AccessFailedCount);
        Assert.Equal(TipoAutor.Anonimo, aposFalha.TipoAutorAlteracao);
        Assert.Null(aposFalha.AutorAlteracao);
        Assert.Equal(instanteFalha, aposFalha.AlteradoEm);
        Assert.NotNull(aposFalha.IdOperacaoAlteracao);

        Relogio.Avancar(TimeSpan.FromMinutes(1));
        DateTimeOffset instanteLogin = Relogio.GetUtcNow();
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);

        SessaoAutenticacao criada = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(TipoAutor.Usuario, criada.TipoAutorCriacao);
        Assert.Equal(id.ToString(), criada.AutorCriacao);
        Assert.Equal(instanteLogin, criada.CriadoEm);
        Assert.NotEqual(Guid.Empty, criada.IdOperacaoCriacao);
        Assert.Null(criada.AlteradoEm);

        // Login válido zerou a contagem já com autoria do usuário verificado.
        UsuarioAplicacao aposLogin = await ContasSinteticas.ContaAsync(A, id);
        Assert.Equal(0, aposLogin.AccessFailedCount);
        Assert.Equal(TipoAutor.Usuario, aposLogin.TipoAutorAlteracao);
        Assert.Equal(id.ToString(), aposLogin.AutorAlteracao);

        Relogio.Avancar(TimeSpan.FromMinutes(1));
        DateTimeOffset instanteLogout = Relogio.GetUtcNow();
        using (HttpResponseMessage logout = await cliente.LogoutAsync(novoToken: false))
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        SessaoAutenticacao revogada = Assert.Single(await ContasSinteticas.SessoesAsync(A, id));
        Assert.Equal(instanteLogout, revogada.RevogadaEm);
        Assert.Equal(revogada.RevogadaEm, revogada.AlteradoEm);
        Assert.Equal(TipoAutor.Usuario, revogada.TipoAutorAlteracao);
        Assert.Equal(id.ToString(), revogada.AutorAlteracao);
        Assert.Equal(criada.CriadoEm, revogada.CriadoEm);
        Assert.Equal(criada.IdOperacaoCriacao, revogada.IdOperacaoCriacao);
        Assert.Equal(2, revogada.Versao);
    }

    [Fact]
    public async Task NenhumRegistroComAutoriaSistema()
    {
        FabricaApiInstalacao api = CriarApi(A);
        string email = ContasSinteticas.NovoEmail("sistema");
        await ContasSinteticas.CriarContaAsync(A, Relogio, email, ContasSinteticas.SenhaPadraoA);
        ClienteAcessoHttp cliente = CriarCliente(api);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        await cliente.EntrarAsync(email, ContasSinteticas.SenhaPadraoA);
        using (HttpResponseMessage logout = await cliente.LogoutAsync())
        {
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        }

        await using AcessoTenantDbContext contexto = BancoTeste.CriarContexto(A.StringConexaoProprietario);
        Assert.False(await contexto.Sessoes.AnyAsync(
            s => s.TipoAutorCriacao == TipoAutor.Sistema || s.TipoAutorAlteracao == TipoAutor.Sistema,
            TestContext.Current.CancellationToken));
        Assert.False(await contexto.Users.AnyAsync(
            u => u.TipoAutorCriacao == TipoAutor.Sistema || u.TipoAutorAlteracao == TipoAutor.Sistema,
            TestContext.Current.CancellationToken));
    }
}
