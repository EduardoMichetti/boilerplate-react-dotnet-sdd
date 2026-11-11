namespace Platform.UnitTests.Operations;

using Platform.Domain.Access;
using Platform.Operations;
using Xunit;

/// <summary>
/// Testes unitários para o interpretador manual de comandos do host Platform.Operations (T109).
/// </summary>
public sealed class TestesInterpretadorComandos
{
    private static readonly Guid TenantValido = Guid.Parse("11111111-1111-7111-8111-111111111111");
    private static readonly Guid AmbienteValido = Guid.Parse("22222222-2222-7222-8222-222222222222");
    private static readonly Guid UsuarioValido = Guid.Parse("33333333-3333-7333-8333-333333333333");

    [Fact]
    public void ContaCriar_ComArgumentosValidos_InterpretaCorretamente()
    {
        string[] args =
        [
            "conta", "criar",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--email", "operador@empresa.com",
            "--nome-exibicao", "Operador Um"
        ];

        RequisicaoComando comando = InterpretadorComandos.Interpretar(args);

        Assert.Equal(TipoComandoOperacao.ContaCriar, comando.Tipo);
        Assert.Equal(TenantValido, comando.IdTenant);
        Assert.Equal(AmbienteValido, comando.IdAmbiente);
        Assert.Equal("operador@empresa.com", comando.Email);
        Assert.Equal("Operador Um", comando.NomeExibicao);
        Assert.Null(comando.IdUsuario);
        Assert.Null(comando.CodigoPermissao);
    }

    [Fact]
    public void ContaDesativar_ComArgumentosValidos_InterpretaCorretamente()
    {
        string[] args =
        [
            "conta", "desativar",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--id-usuario", UsuarioValido.ToString()
        ];

        RequisicaoComando comando = InterpretadorComandos.Interpretar(args);

        Assert.Equal(TipoComandoOperacao.ContaDesativar, comando.Tipo);
        Assert.Equal(TenantValido, comando.IdTenant);
        Assert.Equal(AmbienteValido, comando.IdAmbiente);
        Assert.Equal(UsuarioValido, comando.IdUsuario);
    }

    [Fact]
    public void ContaReativar_ComArgumentosValidos_InterpretaCorretamente()
    {
        string[] args =
        [
            "conta", "reativar",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--id-usuario", UsuarioValido.ToString()
        ];

        RequisicaoComando comando = InterpretadorComandos.Interpretar(args);

        Assert.Equal(TipoComandoOperacao.ContaReativar, comando.Tipo);
        Assert.Equal(TenantValido, comando.IdTenant);
        Assert.Equal(AmbienteValido, comando.IdAmbiente);
        Assert.Equal(UsuarioValido, comando.IdUsuario);
    }

    [Fact]
    public void ContaRedefinirSenha_ComArgumentosValidos_InterpretaCorretamente()
    {
        string[] args =
        [
            "conta", "redefinir-senha",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--id-usuario", UsuarioValido.ToString()
        ];

        RequisicaoComando comando = InterpretadorComandos.Interpretar(args);

        Assert.Equal(TipoComandoOperacao.ContaRedefinirSenha, comando.Tipo);
        Assert.Equal(TenantValido, comando.IdTenant);
        Assert.Equal(AmbienteValido, comando.IdAmbiente);
        Assert.Equal(UsuarioValido, comando.IdUsuario);
    }

    [Fact]
    public void ContaRevogarSessoes_ComArgumentosValidos_InterpretaCorretamente()
    {
        string[] args =
        [
            "conta", "revogar-sessoes",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--id-usuario", UsuarioValido.ToString()
        ];

        RequisicaoComando comando = InterpretadorComandos.Interpretar(args);

        Assert.Equal(TipoComandoOperacao.ContaRevogarSessoes, comando.Tipo);
        Assert.Equal(TenantValido, comando.IdTenant);
        Assert.Equal(AmbienteValido, comando.IdAmbiente);
        Assert.Equal(UsuarioValido, comando.IdUsuario);
    }

    [Fact]
    public void PermissaoConceder_ComArgumentosValidos_InterpretaCorretamente()
    {
        string[] args =
        [
            "permissao", "conceder",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--id-usuario", UsuarioValido.ToString(),
            "--codigo", CodigosPermissao.FundacaoContextoLer
        ];

        RequisicaoComando comando = InterpretadorComandos.Interpretar(args);

        Assert.Equal(TipoComandoOperacao.PermissaoConceder, comando.Tipo);
        Assert.Equal(TenantValido, comando.IdTenant);
        Assert.Equal(AmbienteValido, comando.IdAmbiente);
        Assert.Equal(UsuarioValido, comando.IdUsuario);
        Assert.Equal(CodigosPermissao.FundacaoContextoLer, comando.CodigoPermissao);
    }

    [Fact]
    public void PermissaoRevogar_ComArgumentosValidos_InterpretaCorretamente()
    {
        string[] args =
        [
            "permissao", "revogar",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--id-usuario", UsuarioValido.ToString(),
            "--codigo", CodigosPermissao.FundacaoContextoLer
        ];

        RequisicaoComando comando = InterpretadorComandos.Interpretar(args);

        Assert.Equal(TipoComandoOperacao.PermissaoRevogar, comando.Tipo);
        Assert.Equal(TenantValido, comando.IdTenant);
        Assert.Equal(AmbienteValido, comando.IdAmbiente);
        Assert.Equal(UsuarioValido, comando.IdUsuario);
        Assert.Equal(CodigosPermissao.FundacaoContextoLer, comando.CodigoPermissao);
    }

    [Fact]
    public void SessaoLimpar_ComArgumentosValidos_InterpretaCorretamente()
    {
        string[] args =
        [
            "sessao", "limpar",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString()
        ];

        RequisicaoComando comando = InterpretadorComandos.Interpretar(args);

        Assert.Equal(TipoComandoOperacao.SessaoLimpar, comando.Tipo);
        Assert.Equal(TenantValido, comando.IdTenant);
        Assert.Equal(AmbienteValido, comando.IdAmbiente);
        Assert.Null(comando.IdUsuario);
    }

    [Theory]
    [InlineData("--actor")]
    [InlineData("--senha")]
    [InlineData("--password")]
    [InlineData("--retencao-dias")]
    public void ParametrosProibidos_LancamExcecao(string parametroProibido)
    {
        string[] args =
        [
            "sessao", "limpar",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            parametroProibido, "valor"
        ];

        var ex = Assert.Throws<ArgumentException>(() => InterpretadorComandos.Interpretar(args));
        Assert.Contains(parametroProibido, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CodigoPermissaoDesconhecido_LancaExcecao()
    {
        string[] args =
        [
            "permissao", "conceder",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--id-usuario", UsuarioValido.ToString(),
            "--codigo", "modulo.permissao.desconhecida"
        ];

        var ex = Assert.Throws<ArgumentException>(() => InterpretadorComandos.Interpretar(args));
        Assert.Contains("não pertence ao catálogo conhecido", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParametroDesconhecido_LancaExcecao()
    {
        string[] args =
        [
            "sessao", "limpar",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--parametro-invalido", "teste"
        ];

        var ex = Assert.Throws<ArgumentException>(() => InterpretadorComandos.Interpretar(args));
        Assert.Contains("Parâmetro não reconhecido", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParametroDuplicado_LancaExcecao()
    {
        string[] args =
        [
            "sessao", "limpar",
            "--id-tenant", TenantValido.ToString(),
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString()
        ];

        var ex = Assert.Throws<ArgumentException>(() => InterpretadorComandos.Interpretar(args));
        Assert.Contains("mais de uma vez", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParametroObrigatorioAusente_LancaExcecao()
    {
        string[] args =
        [
            "conta", "desativar",
            "--id-tenant", TenantValido.ToString(),
            "--id-ambiente", AmbienteValido.ToString()
            // falta --id-usuario
        ];

        var ex = Assert.Throws<ArgumentException>(() => InterpretadorComandos.Interpretar(args));
        Assert.Contains("--id-usuario", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UuidVazio_LancaExcecao()
    {
        string[] args =
        [
            "conta", "desativar",
            "--id-tenant", Guid.Empty.ToString(),
            "--id-ambiente", AmbienteValido.ToString(),
            "--id-usuario", UsuarioValido.ToString()
        ];

        var ex = Assert.Throws<ArgumentException>(() => InterpretadorComandos.Interpretar(args));
        Assert.Contains("UUID válido e não vazio", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MenosDeDoisArgumentos_LancaExcecao()
    {
        string[] args = ["conta"];
        Assert.Throws<ArgumentException>(() => InterpretadorComandos.Interpretar(args));
    }

    [Fact]
    public void ComandoDesconhecido_LancaExcecao()
    {
        string[] args = ["usuario", "deletar"];
        Assert.Throws<ArgumentException>(() => InterpretadorComandos.Interpretar(args));
    }
}
