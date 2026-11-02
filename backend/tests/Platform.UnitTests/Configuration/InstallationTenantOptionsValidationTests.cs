namespace Platform.UnitTests.Configuration;

using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Xunit;

/// <summary>
/// Testes unitários para validar que configuração obrigatória ausente ou inválida
/// em OpcoesInquilinoInstalacao impede a prontidão operacional e fornece diagnóstico seguro,
/// sem vazar connection strings, senhas ou segredos nos erros gerados.
/// Atende à tarefa T028 e aos requisitos da User Story 1.
/// </summary>
public sealed class InstallationTenantOptionsValidationTests
{
    private const string SegredoPrivadoBanco = "SuperSenhaAltamenteSecreta123!@#";

    [Fact]
    public void ConfiguracaoCompletamenteAusente_BloqueiaProntidaoComErrosDescritivosESeguros()
    {
        OpcoesInquilinoInstalacao opcoes = new();

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());

        Assert.Equal(6, excecao.Erros.Count);
        Assert.Contains(excecao.Erros, e => e.Contains("IdAplicacao", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("IdAmbiente", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("IdInquilino", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("NomeExibicaoInquilino", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("EstadoInquilino", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, e => e.Contains("StringsConexao:BancoApp", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IdAplicacaoInvalidoOuVazio_FalhaValidacao(string? idAplicacaoInvalido)
    {
        OpcoesInquilinoInstalacao opcoes = CriarOpcoesValidas();
        opcoes.IdAplicacao = idAplicacaoInvalido;

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("IdAplicacao", StringComparison.Ordinal));
    }

    [Fact]
    public void IdAplicacaoMaiorQueLimite_FalhaValidacao()
    {
        OpcoesInquilinoInstalacao opcoes = CriarOpcoesValidas();
        opcoes.IdAplicacao = new string('x', 101);

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("IdAplicacao", StringComparison.Ordinal));
    }

    [Fact]
    public void IdAmbienteOuInquilinoVazio_FalhaValidacao()
    {
        OpcoesInquilinoInstalacao opcoesAmbienteVazio = CriarOpcoesValidas();
        opcoesAmbienteVazio.IdAmbiente = Guid.Empty;

        ExcecaoConfiguracaoInstalacao excecaoAmbiente = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoesAmbienteVazio.Validar());
        Assert.Contains(excecaoAmbiente.Erros, e => e.Contains("IdAmbiente", StringComparison.Ordinal));

        OpcoesInquilinoInstalacao opcoesInquilinoVazio = CriarOpcoesValidas();
        opcoesInquilinoVazio.IdInquilino = Guid.Empty;

        ExcecaoConfiguracaoInstalacao excecaoInquilino = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoesInquilinoVazio.Validar());
        Assert.Contains(excecaoInquilino.Erros, e => e.Contains("IdInquilino", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NomeExibicaoInvalidoOuVazio_FalhaValidacao(string? nomeInvalido)
    {
        OpcoesInquilinoInstalacao opcoes = CriarOpcoesValidas();
        opcoes.NomeExibicaoInquilino = nomeInvalido;

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("NomeExibicaoInquilino", StringComparison.Ordinal));
    }

    [Fact]
    public void NomeExibicaoMaiorQueLimite_FalhaValidacao()
    {
        OpcoesInquilinoInstalacao opcoes = CriarOpcoesValidas();
        opcoes.NomeExibicaoInquilino = new string('y', 121);

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("NomeExibicaoInquilino", StringComparison.Ordinal));
    }

    [Fact]
    public void EstadoInquilinoNuloOuInvalido_FalhaValidacao()
    {
        OpcoesInquilinoInstalacao opcoesNulo = CriarOpcoesValidas();
        opcoesNulo.EstadoInquilino = null;

        ExcecaoConfiguracaoInstalacao excecaoNulo = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoesNulo.Validar());
        Assert.Contains(excecaoNulo.Erros, e => e.Contains("EstadoInquilino", StringComparison.Ordinal));

        OpcoesInquilinoInstalacao opcoesInvalido = CriarOpcoesValidas();
        opcoesInvalido.EstadoInquilino = (EstadoInquilino)999;

        ExcecaoConfiguracaoInstalacao excecaoInvalido = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoesInvalido.Validar());
        Assert.Contains(excecaoInvalido.Erros, e => e.Contains("EstadoInquilino", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void StringConexaoAusente_FalhaValidacao(string? stringConexaoNula)
    {
        OpcoesInquilinoInstalacao opcoes = CriarOpcoesValidas();
        opcoes.StringsConexao = stringConexaoNula is null ? null : new StringsConexaoInstalacao { BancoApp = stringConexaoNula };

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());
        Assert.Contains(excecao.Erros, e => e.Contains("StringsConexao:BancoApp", StringComparison.Ordinal));
    }

    [Fact]
    public void StringConexaoSintaxeInvalida_NaoVazaSegredosOuMensagensInternas()
    {
        OpcoesInquilinoInstalacao opcoes = CriarOpcoesValidas();
        opcoes.StringsConexao = new StringsConexaoInstalacao
        {
            BancoApp = $"Host=localhost;Database=banco;Username=usuario;Password={SegredoPrivadoBanco};malformado_sem_igual"
        };

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => opcoes.Validar());

        Assert.Contains(excecao.Erros, e => e.Contains("StringsConexao:BancoApp possui sintaxe inválida.", StringComparison.Ordinal));
        Assert.DoesNotContain(SegredoPrivadoBanco, excecao.ToString(), StringComparison.Ordinal);
        Assert.All(excecao.Erros, e => Assert.DoesNotContain(SegredoPrivadoBanco, e, StringComparison.Ordinal));
        Assert.Null(excecao.InnerException);
    }

    [Theory]
    [InlineData(EstadoInquilino.Ativo)]
    [InlineData(EstadoInquilino.Inativo)]
    public void ConfiguracaoCompletaEValida_NaoLancaExcecao(EstadoInquilino estado)
    {
        OpcoesInquilinoInstalacao opcoes = CriarOpcoesValidas(estado);
        opcoes.Validar();
    }

    private static OpcoesInquilinoInstalacao CriarOpcoesValidas(EstadoInquilino estado = EstadoInquilino.Ativo) => new()
    {
        IdAplicacao = "boilerplate-react-dotnet-sdd",
        IdAmbiente = Guid.Parse("11111111-1111-4111-8111-111111111111"),
        IdInquilino = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
        NomeExibicaoInquilino = "Cliente de Produção",
        EstadoInquilino = estado,
        StringsConexao = new StringsConexaoInstalacao
        {
            BancoApp = $"Host=localhost;Database=banco_valido;Username=usuario;Password={SegredoPrivadoBanco}"
        }
    };
}
