namespace Platform.UnitTests.Access;

using Platform.Domain.Access;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;
using Xunit;

/// <summary>
/// Invariantes de negócio das entidades de acesso (nomes L2: <c>FkIdTenant</c>, <c>FkIdUsuario</c>,
/// base auditável, concessão sem <c>ConcedidaEm</c>/<c>Versao</c>).
/// </summary>
public sealed class TestesModeloAcessoUsuario
{
    private static readonly DateTimeOffset Instante = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void UsuarioAplicacaoValidaCamposObrigatoriosELimites()
    {
        CriarUsuario().Validar();
    }

    [Fact]
    public void UsuarioAplicacao_ImplementaContratoAuditavel()
    {
        Assert.IsAssignableFrom<IEntidadeAuditavel>(CriarUsuario());
    }

    [Fact]
    public void UsuarioAplicacaoRejeitaIdVazio()
    {
        UsuarioAplicacao usuario = CriarUsuario();
        usuario.Id = Guid.Empty;

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("Id", excecao.ParamName);
    }

    [Fact]
    public void UsuarioAplicacaoRejeitaFkIdTenantVazio()
    {
        UsuarioAplicacao usuario = CriarUsuario();
        usuario.FkIdTenant = Guid.Empty;

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("FkIdTenant", excecao.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UsuarioAplicacaoRejeitaNomeExibicaoVazioOuEmBranco(string nomeExibicao)
    {
        UsuarioAplicacao usuario = CriarUsuario();
        usuario.NomeExibicao = nomeExibicao;

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("NomeExibicao", excecao.ParamName);
    }

    [Fact]
    public void UsuarioAplicacaoRejeitaNomeExibicaoQueExcedeLimiteMaximo()
    {
        UsuarioAplicacao usuario = CriarUsuario();
        usuario.NomeExibicao = new string('x', 121);

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("NomeExibicao", excecao.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void UsuarioAplicacaoRejeitaEmailVazioOuEmBranco(string email)
    {
        UsuarioAplicacao usuario = CriarUsuario();
        usuario.Email = email;

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("Email", excecao.ParamName);
    }

    [Fact]
    public void UsuarioAplicacaoRejeitaEmailQueExcedeLimiteMaximo()
    {
        UsuarioAplicacao usuario = CriarUsuario();
        usuario.Email = new string('a', 246) + "@exemplo.com";

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => usuario.Validar());
        Assert.Equal("Email", excecao.ParamName);
    }

    [Fact]
    public void PermissaoUsuarioValidaCamposObrigatorios()
    {
        CriarPermissao().Validar();
    }

    [Fact]
    public void PermissaoUsuario_HerdaBaseAuditavelSemVersao()
    {
        PermissaoUsuario permissao = CriarPermissao();

        Assert.IsAssignableFrom<EntidadeAuditavel>(permissao);
        Assert.IsNotAssignableFrom<IEntidadeVersionada>(permissao);
        Assert.Null(typeof(PermissaoUsuario).GetProperty("ConcedidaEm"));
        Assert.Null(typeof(PermissaoUsuario).GetProperty("Versao"));
    }

    [Fact]
    public void PermissaoUsuarioRejeitaFkIdUsuarioVazio()
    {
        PermissaoUsuario permissao = CriarPermissao();
        permissao.FkIdUsuario = Guid.Empty;

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => permissao.Validar());
        Assert.Equal("FkIdUsuario", excecao.ParamName);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("fundacao.contexto.ler")]
    [InlineData("Foundation.Context.Read")]
    public void PermissaoUsuarioRejeitaCodigoVazioForaDoFormatoOuDesconhecido(string codigo)
    {
        PermissaoUsuario permissao = CriarPermissao();
        permissao.CodigoPermissao = codigo;

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => permissao.Validar());
        Assert.Equal("CodigoPermissao", excecao.ParamName);
    }

    [Fact]
    public void PermissaoUsuarioRejeitaCodigoPermissaoQueExcedeLimiteMaximo()
    {
        PermissaoUsuario permissao = CriarPermissao();
        permissao.CodigoPermissao = "a." + new string('p', 127);

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => permissao.Validar());
        Assert.Equal("CodigoPermissao", excecao.ParamName);
    }

    [Fact]
    public void SessaoAutenticacao_ValidaERejeitaRevogadaComTicket()
    {
        SessaoAutenticacao sessao = new()
        {
            IdSessao = IdentificadorV7.Gerar(Instante),
            FkIdUsuario = IdentificadorV7.Gerar(Instante),
            EpocaAcesso = IdentificadorV7.Gerar(Instante),
            TicketProtegido = [1, 2, 3],
            ExpiraEm = Instante.AddMinutes(30),
        };
        sessao.Validar();
        Assert.True(sessao.EstaAtivaEm(Instante.AddMinutes(29).AddSeconds(59)));
        Assert.False(sessao.EstaAtivaEm(Instante.AddMinutes(30)));

        sessao.RevogadaEm = Instante;
        ArgumentException excecao = Assert.Throws<ArgumentException>(() => sessao.Validar());
        Assert.Equal("TicketProtegido", excecao.ParamName);
    }

    [Fact]
    public void SessaoAutenticacao_RejeitaExpiraEmComOffset()
    {
        SessaoAutenticacao sessao = new()
        {
            IdSessao = IdentificadorV7.Gerar(Instante),
            FkIdUsuario = IdentificadorV7.Gerar(Instante),
            EpocaAcesso = IdentificadorV7.Gerar(Instante),
            ExpiraEm = new DateTimeOffset(2026, 11, 8, 9, 30, 0, TimeSpan.FromHours(-3)),
        };

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => sessao.Validar());
        Assert.Equal("ExpiraEm", excecao.ParamName);
    }

    [Fact]
    public void RegistroOperacaoAcesso_CamposDeLimpezaSoEmLimparSessoes()
    {
        RegistroOperacaoAcesso registro = new()
        {
            IdOperacao = IdentificadorV7.Gerar(Instante),
            IdTenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            IdAmbiente = Guid.Parse("11111111-1111-4111-8111-111111111111"),
            LoginOperador = "appdb_a_op_um",
            TipoOperacao = TipoOperacaoAcesso.LimparSessoes,
            RegistradaEm = Instante,
            Resultado = ResultadoOperacaoAcesso.Sucesso,
            CodigoResultado = "SUCESSO",
            RetencaoDias = 30,
            InstanteCorte = Instante.AddDays(-30),
            QuantidadeRemovida = 0,
        };
        registro.Validar();

        registro.TipoOperacao = TipoOperacaoAcesso.CriarConta;
        Assert.Throws<ArgumentException>(registro.Validar);
    }

    private static UsuarioAplicacao CriarUsuario() => new()
    {
        Id = IdentificadorV7.Gerar(Instante),
        FkIdTenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
        Email = "usuario@tenant.local",
        UserName = "usuario@tenant.local",
        NomeExibicao = "Usuário Válido",
        EstaAtivo = true,
    };

    private static PermissaoUsuario CriarPermissao() => new()
    {
        FkIdUsuario = IdentificadorV7.Gerar(Instante),
        CodigoPermissao = CodigosPermissao.FundacaoContextoLer,
    };
}
