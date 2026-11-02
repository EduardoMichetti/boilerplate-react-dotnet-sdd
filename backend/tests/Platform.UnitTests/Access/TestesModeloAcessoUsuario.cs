namespace Platform.UnitTests.Access;

using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Xunit;

public sealed class TestesModeloAcessoUsuario
{
    [Fact]
    public void UsuarioAplicacaoValidaCamposObrigatoriosELimites()
    {
        UsuarioAplicacao usuarioValido = new()
        {
            Id = Guid.NewGuid(),
            IdTenant = Guid.NewGuid(),
            Email = "usuario@tenant.local",
            UserName = "usuario@tenant.local",
            NomeExibicao = "Usuário Válido",
            EstaAtivo = true
        };

        usuarioValido.Validar();
    }

    [Fact]
    public void UsuarioAplicacaoRejeitaIdVazio()
    {
        UsuarioAplicacao usuario = new()
        {
            Id = Guid.Empty,
            IdTenant = Guid.NewGuid(),
            Email = "usuario@tenant.local",
            NomeExibicao = "Usuário"
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("Id", excecao.ParamName);
    }

    [Fact]
    public void UsuarioAplicacaoRejeitaIdTenantVazio()
    {
        UsuarioAplicacao usuario = new()
        {
            Id = Guid.NewGuid(),
            IdTenant = Guid.Empty,
            Email = "usuario@tenant.local",
            NomeExibicao = "Usuário"
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("IdTenant", excecao.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UsuarioAplicacaoRejeitaNomeExibicaoVazioOuEmBranco(string nomeExibicao)
    {
        UsuarioAplicacao usuario = new()
        {
            Id = Guid.NewGuid(),
            IdTenant = Guid.NewGuid(),
            Email = "usuario@tenant.local",
            NomeExibicao = nomeExibicao
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("NomeExibicao", excecao.ParamName);
    }

    [Fact]
    public void UsuarioAplicacaoRejeitaNomeExibicaoQueExcedeLimiteMaximo()
    {
        UsuarioAplicacao usuario = new()
        {
            Id = Guid.NewGuid(),
            IdTenant = Guid.NewGuid(),
            Email = "usuario@tenant.local",
            NomeExibicao = new string('x', 121)
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("NomeExibicao", excecao.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UsuarioAplicacaoRejeitaEmailVazioOuEmBranco(string email)
    {
        UsuarioAplicacao usuario = new()
        {
            Id = Guid.NewGuid(),
            IdTenant = Guid.NewGuid(),
            Email = email,
            NomeExibicao = "Usuário"
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("Email", excecao.ParamName);
    }

    [Fact]
    public void UsuarioAplicacaoRejeitaEmailQueExcedeLimiteMaximo()
    {
        UsuarioAplicacao usuario = new()
        {
            Id = Guid.NewGuid(),
            IdTenant = Guid.NewGuid(),
            Email = new string('a', 246) + "@exemplo.com", // 258 caracteres
            NomeExibicao = "Usuário"
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("Email", excecao.ParamName);
    }

    [Fact]
    public void PermissaoUsuarioValidaCamposObrigatoriosELimites()
    {
        PermissaoUsuario permissao = new()
        {
            IdUsuario = Guid.NewGuid(),
            CodigoPermissao = "fundacao.contexto.ler",
            ConcedidaEm = DateTimeOffset.UtcNow,
            Versao = 1
        };

        permissao.Validar();
    }

    [Fact]
    public void PermissaoUsuarioRejeitaIdUsuarioVazio()
    {
        PermissaoUsuario permissao = new()
        {
            IdUsuario = Guid.Empty,
            CodigoPermissao = "fundacao.contexto.ler",
            ConcedidaEm = DateTimeOffset.UtcNow
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => permissao.Validar());
        Assert.Equal("IdUsuario", excecao.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void PermissaoUsuarioRejeitaCodigoPermissaoVazioOuEmBranco(string codigo)
    {
        PermissaoUsuario permissao = new()
        {
            IdUsuario = Guid.NewGuid(),
            CodigoPermissao = codigo,
            ConcedidaEm = DateTimeOffset.UtcNow
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => permissao.Validar());
        Assert.Equal("CodigoPermissao", excecao.ParamName);
    }

    [Fact]
    public void PermissaoUsuarioRejeitaCodigoPermissaoQueExcedeLimiteMaximo()
    {
        PermissaoUsuario permissao = new()
        {
            IdUsuario = Guid.NewGuid(),
            CodigoPermissao = new string('p', 129),
            ConcedidaEm = DateTimeOffset.UtcNow
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => permissao.Validar());
        Assert.Equal("CodigoPermissao", excecao.ParamName);
    }

    [Fact]
    public void PermissaoUsuarioRejeitaConcedidaEmPadraoNaoDefinido()
    {
        PermissaoUsuario permissao = new()
        {
            IdUsuario = Guid.NewGuid(),
            CodigoPermissao = "fundacao.contexto.ler",
            ConcedidaEm = default
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => permissao.Validar());
        Assert.Equal("ConcedidaEm", excecao.ParamName);
    }
}
