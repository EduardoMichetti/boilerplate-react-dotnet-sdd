namespace Platform.UnitTests.Configuration;

using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Xunit;

/// <summary>
/// Testes unitários para validar que configuração obrigatória ausente ou inválida
/// em OpcoesInstalacaoTenant impede a prontidão operacional e fornece diagnóstico seguro,
/// sem vazar strings de conexão, senhas ou segredos nos erros gerados.
/// </summary>
public sealed class TestesValidacaoOpcoesInstalacaoTenant
{
    private const string SegredoPrivadoBanco = "SuperSenhaAltamenteSecreta123!@#";

    [Fact]
    public void ConfiguracaoCompletamenteAusente_BloqueiaProntidaoComErrosDescritivosESeguros()
    {
        OpcoesInstalacaoTenant opcoes = new();

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());

        Assert.Equal(6, excecao.Erros.Count);
        Assert.Contains(excecao.Erros, e => e.Contains("CodigoAplicacao", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("IdAmbiente", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("IdTenant", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("NomeExibicaoTenant", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("EstadoTenant", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("StringsConexao:BancoApp", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CodigoAplicacaoInvalidoOuVazio_FalhaValidacao(string? codigoAplicacaoInvalido)
    {
        OpcoesInstalacaoTenant opcoes = CriarOpcoesValidas();
        opcoes.CodigoAplicacao = codigoAplicacaoInvalido;

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("CodigoAplicacao", StringComparison.Ordinal));
    }

    [Fact]
    public void CodigoAplicacaoMaiorQueLimite_FalhaValidacao()
    {
        OpcoesInstalacaoTenant opcoes = CriarOpcoesValidas();
        opcoes.CodigoAplicacao = new string('x', 101);

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("CodigoAplicacao", StringComparison.Ordinal));
    }

    [Fact]
    public void IdAmbienteOuTenantVazio_FalhaValidacao()
    {
        OpcoesInstalacaoTenant opcoesAmbienteVazio = CriarOpcoesValidas();
        opcoesAmbienteVazio.IdAmbiente = Guid.Empty;

        ExcecaoConfiguracaoInstalacao excecaoAmbiente = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoesAmbienteVazio.Validar());
        Assert.Contains(excecaoAmbiente.Erros, e => e.Contains("IdAmbiente", StringComparison.Ordinal));

        OpcoesInstalacaoTenant opcoesTenantVazio = CriarOpcoesValidas();
        opcoesTenantVazio.IdTenant = Guid.Empty;

        ExcecaoConfiguracaoInstalacao excecaoTenant = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoesTenantVazio.Validar());
        Assert.Contains(excecaoTenant.Erros, e => e.Contains("IdTenant", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NomeExibicaoInvalidoOuVazio_FalhaValidacao(string? nomeInvalido)
    {
        OpcoesInstalacaoTenant opcoes = CriarOpcoesValidas();
        opcoes.NomeExibicaoTenant = nomeInvalido;

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("NomeExibicaoTenant", StringComparison.Ordinal));
    }

    [Fact]
    public void NomeExibicaoMaiorQueLimite_FalhaValidacao()
    {
        OpcoesInstalacaoTenant opcoes = CriarOpcoesValidas();
        opcoes.NomeExibicaoTenant = new string('y', 121);

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("NomeExibicaoTenant", StringComparison.Ordinal));
    }

    [Fact]
    public void EstadoTenantNuloOuInvalido_FalhaValidacao()
    {
        OpcoesInstalacaoTenant opcoesNulo = CriarOpcoesValidas();
        opcoesNulo.EstadoTenant = null;

        ExcecaoConfiguracaoInstalacao excecaoNulo = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoesNulo.Validar());
        Assert.Contains(excecaoNulo.Erros, e => e.Contains("EstadoTenant", StringComparison.Ordinal));

        OpcoesInstalacaoTenant opcoesInvalido = CriarOpcoesValidas();
        opcoesInvalido.EstadoTenant = (EstadoTenant)999;

        ExcecaoConfiguracaoInstalacao excecaoInvalido = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoesInvalido.Validar());
        Assert.Contains(excecaoInvalido.Erros, e => e.Contains("EstadoTenant", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StringConexaoAusente_FalhaValidacao(string? stringConexaoNula)
    {
        OpcoesInstalacaoTenant opcoes = CriarOpcoesValidas();
        opcoes.StringsConexao = stringConexaoNula is null ? null : new StringsConexaoInstalacao { BancoApp = stringConexaoNula };

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("StringsConexao:BancoApp", StringComparison.Ordinal));
    }

    [Fact]
    public void StringConexaoSintaxeInvalida_NaoVazaSegredosOuMensagensInternas()
    {
        OpcoesInstalacaoTenant opcoes = CriarOpcoesValidas();
        opcoes.StringsConexao = new StringsConexaoInstalacao
        {
            BancoApp = $"Host=localhost;Database=banco;Username=usuario;******;malformado_sem_igual"
        };

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());

        Assert.Contains(excecao.Erros, e => e.Contains("StringsConexao:BancoApp possui sintaxe inválida.", StringComparison.Ordinal));
        Assert.DoesNotContain(SegredoPrivadoBanco, excecao.ToString(), StringComparison.Ordinal);
        Assert.All(excecao.Erros, e => Assert.DoesNotContain(SegredoPrivadoBanco, e, StringComparison.Ordinal));
        Assert.Null(excecao.InnerException);
    }

    [Theory]
    [InlineData(EstadoTenant.Ativo)]
    [InlineData(EstadoTenant.Inativo)]
    public void ConfiguracaoCompletaEValida_NaoLancaExcecao(EstadoTenant estado)
    {
        OpcoesInstalacaoTenant opcoes = CriarOpcoesValidas(estado);
        opcoes.Validar();
    }

    private static OpcoesInstalacaoTenant CriarOpcoesValidas(EstadoTenant estado = EstadoTenant.Ativo) => new()
    {
        CodigoAplicacao = "boilerplate-react-dotnet-sdd",
        IdAmbiente = Guid.Parse("11111111-1111-4111-8111-111111111111"),
        IdTenant = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
        NomeExibicaoTenant = "Cliente de Produção",
        EstadoTenant = estado,
        StringsConexao = new StringsConexaoInstalacao
        {
            BancoApp = "Host=localhost;Database=banco_valido;Username=usuario"
        }
    };
}
