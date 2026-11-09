using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Xunit;

namespace Platform.UnitTests.Foundation;

public sealed class TestesConfiguracaoInstalacao
{
    [Theory]
    [InlineData(EstadoTenant.Inativo)]
    [InlineData(EstadoTenant.Ativo)]
    public void ConfiguracaoValidaAceitaAmbosOsEstadosOperacionais(EstadoTenant estado)
    {
        CriarOpcoes(estado: estado).Validar();
    }

    [Fact]
    public void ConfiguracaoAusenteReportaTodosOsCamposObrigatoriosSemVazarValores()
    {
        ExcecaoConfiguracaoInstalacao excecao =
            Assert.Throws<ExcecaoConfiguracaoInstalacao>(() => new OpcoesInstalacaoTenant().Validar());

        Assert.Equal(6, excecao.Erros.Count);
        Assert.Contains(excecao.Erros, erro => erro.Contains("CodigoAplicacao", StringComparison.Ordinal));
        Assert.Contains(excecao.Erros, erro => erro.Contains("StringsConexao:BancoApp", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("string-de-conexao-invalida")]
    public void StringConexaoInvalidaERejeitadaSemVazarSeuValor(string stringConexao)
    {
        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(
            () => CriarOpcoes(stringConexao: stringConexao).Validar());

        Assert.Contains(excecao.Erros, erro => erro.Contains("StringsConexao:BancoApp", StringComparison.Ordinal));
        Assert.Null(excecao.InnerException);
    }

    [Theory]
    [InlineData(0, 1, false)]
    [InlineData(100, 120, true)]
    [InlineData(101, 120, false)]
    [InlineData(100, 121, false)]
    public void ComprimentoDeTextosRespeitaLimitesDeclarados(int tamanhoAplicacao, int tamanhoExibicao, bool valido)
    {
        OpcoesInstalacaoTenant opcoes = CriarOpcoes(
            codigoAplicacao: new string('a', tamanhoAplicacao),
            nomeExibicao: new string('b', tamanhoExibicao));

        if (valido)
        {
            opcoes.Validar();
        }
        else
        {
            Assert.Throws<ExcecaoConfiguracaoInstalacao>(opcoes.Validar);
        }
    }

    [Fact]
    public void IdentificadoresVaziosEEstadoDesconhecidoSaoRejeitados()
    {
        OpcoesInstalacaoTenant opcoes = new()
        {
            CodigoAplicacao = "produto-teste",
            NomeExibicaoTenant = "Instalação sintética",
            EstadoTenant = (EstadoTenant)42,
            StringsConexao = new() { BancoApp = "Host=localhost;Database=sintetico;Username=sintetico" }
        };

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(opcoes.Validar);

        Assert.Equal(3, excecao.Erros.Count);
    }

    [Fact]
    public void StringConexaoMalFormatadaNaoApareceNosDiagnosticos()
    {
        const string valorPrivado = "segredo-privado-sintetico";
        OpcoesInstalacaoTenant opcoes = CriarOpcoes(
            stringConexao: $"******;malformada");

        ExcecaoConfiguracaoInstalacao excecao = Assert.Throws<ExcecaoConfiguracaoInstalacao>(opcoes.Validar);

        Assert.DoesNotContain(valorPrivado, excecao.ToString(), StringComparison.Ordinal);
        Assert.All(excecao.Erros, erro => Assert.DoesNotContain(valorPrivado, erro, StringComparison.Ordinal));
    }

    private static OpcoesInstalacaoTenant CriarOpcoes(
        EstadoTenant estado = EstadoTenant.Ativo,
        string codigoAplicacao = "produto-teste",
        string nomeExibicao = "Instalação sintética",
        string stringConexao = "Host=localhost;Database=sintetico;Username=sintetico")
    {
        return new()
        {
            CodigoAplicacao = codigoAplicacao,
            IdAmbiente = Guid.NewGuid(),
            IdTenant = Guid.NewGuid(),
            NomeExibicaoTenant = nomeExibicao,
            EstadoTenant = estado,
            StringsConexao = new() { BancoApp = stringConexao }
        };
    }
}
