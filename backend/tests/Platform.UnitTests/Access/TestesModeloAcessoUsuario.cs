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
        SessaoAutenticacao sessao = CriarSessao(600, 30);
        sessao.Validar();

        sessao.RevogadaEm = Instante;
        ArgumentException excecao = Assert.Throws<ArgumentException>(() => sessao.Validar());
        Assert.Equal("TicketProtegido", excecao.ParamName);
    }

    [Fact]
    public void SessaoAutenticacao_RejeitaTetoComOffset()
    {
        SessaoAutenticacao sessao = CriarSessao(600, 30);
        sessao.TetoAbsolutoEm = new DateTimeOffset(2026, 11, 8, 19, 0, 0, TimeSpan.FromHours(-3));

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => sessao.Validar());
        Assert.Equal("TetoAbsolutoEm", excecao.ParamName);
    }

    [Theory]
    [InlineData(30, 5)]
    [InlineData(30, 30)]
    [InlineData(720, 240)]
    [InlineData(600, 30)]
    public void SessaoAutenticacao_AceitaTetoEJanelaNosLimites(int tetoMinutos, int janelaMinutos)
    {
        CriarSessao(tetoMinutos, janelaMinutos).Validar();
    }

    [Theory]
    [InlineData(29)]
    [InlineData(721)]
    public void SessaoAutenticacao_RejeitaTetoForaDe30A720(int tetoMinutos)
    {
        SessaoAutenticacao sessao = CriarSessao(tetoMinutos, 5);

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => sessao.Validar());
        Assert.Equal("TetoAbsolutoEm", excecao.ParamName);
    }

    [Theory]
    [InlineData(600, 4)]
    [InlineData(600, 241)]
    [InlineData(60, 61)]
    public void SessaoAutenticacao_RejeitaJanelaForaDaFaixaOuMaiorQueTeto(int tetoMinutos, int janelaMinutos)
    {
        SessaoAutenticacao sessao = CriarSessao(tetoMinutos, janelaMinutos);

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => sessao.Validar());
        Assert.Equal("JanelaInatividadeMinutos", excecao.ParamName);
    }

    [Fact]
    public void SessaoAutenticacao_RejeitaUltimoUsoForaDeCriacaoAteTeto()
    {
        SessaoAutenticacao antes = CriarSessao(600, 30);
        antes.UltimoUsoEm = Instante.AddTicks(-10);
        Assert.Equal("UltimoUsoEm", Assert.Throws<ArgumentException>(() => antes.Validar()).ParamName);

        SessaoAutenticacao noTeto = CriarSessao(600, 30);
        noTeto.UltimoUsoEm = noTeto.TetoAbsolutoEm;
        Assert.Equal("UltimoUsoEm", Assert.Throws<ArgumentException>(() => noTeto.Validar()).ParamName);

        SessaoAutenticacao depois = CriarSessao(600, 30);
        depois.UltimoUsoEm = depois.TetoAbsolutoEm.AddMinutes(1);
        Assert.Equal("UltimoUsoEm", Assert.Throws<ArgumentException>(() => depois.Validar()).ParamName);
    }

    [Fact]
    public void SessaoAutenticacao_RegistrarUso_SoAvancaENaoAlteraOutrosCampos()
    {
        SessaoAutenticacao sessao = CriarSessao(600, 30);
        DateTimeOffset teto = sessao.TetoAbsolutoEm;
        byte[] ticket = sessao.TicketProtegido!;

        sessao.RegistrarUso(Instante.AddMinutes(10));
        Assert.Equal(Instante.AddMinutes(10), sessao.UltimoUsoEm);

        sessao.RegistrarUso(Instante.AddMinutes(10));
        Assert.Equal(Instante.AddMinutes(10), sessao.UltimoUsoEm);

        Assert.ThrowsAny<ArgumentException>(() => sessao.RegistrarUso(Instante.AddMinutes(9)));
        Assert.ThrowsAny<ArgumentException>(() => sessao.RegistrarUso(teto));
        Assert.ThrowsAny<ArgumentException>(() => sessao.RegistrarUso(teto.AddMinutes(1)));
        Assert.ThrowsAny<ArgumentException>(() => sessao.RegistrarUso(new DateTimeOffset(2026, 11, 8, 9, 20, 0, TimeSpan.FromHours(-3))));

        Assert.Equal(Instante.AddMinutes(10), sessao.UltimoUsoEm);
        Assert.Equal(teto, sessao.TetoAbsolutoEm);
        Assert.Equal(30, sessao.JanelaInatividadeMinutos);
        Assert.Same(ticket, sessao.TicketProtegido);
        Assert.Null(sessao.RevogadaEm);
        sessao.Validar();
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

    private static SessaoAutenticacao CriarSessao(int tetoMinutos, int janelaMinutos) => new()
    {
        IdSessao = IdentificadorV7.Gerar(Instante),
        FkIdUsuario = IdentificadorV7.Gerar(Instante),
        EpocaAcesso = IdentificadorV7.Gerar(Instante),
        TicketProtegido = [1, 2, 3],
        CriadoEm = Instante,
        TetoAbsolutoEm = Instante.AddMinutes(tetoMinutos),
        JanelaInatividadeMinutos = janelaMinutos,
        UltimoUsoEm = Instante,
    };

    private static PermissaoUsuario CriarPermissao() => new()
    {
        FkIdUsuario = IdentificadorV7.Gerar(Instante),
        CodigoPermissao = CodigosPermissao.FundacaoContextoLer,
    };
}
