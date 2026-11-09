namespace Platform.UnitTests.Resultados;

using Platform.Domain.Resultados;
using Xunit;

/// <summary>
/// T080: resultado explícito de regra de negócio (ADR 0003 D3).
/// </summary>
public sealed class TestesResultado
{
    private static readonly Erro ErroConflito = new("MODULO_REGRA", "Conflito de teste.", TipoErro.Conflito);

    [Fact]
    public void ConversaoImplicitaDeValor_GeraSucesso()
    {
        Resultado<int> resultado = 42;

        Assert.True(resultado.EhSucesso);
        Assert.Equal(42, resultado.Valor);
        Assert.Null(resultado.Erro);
    }

    [Fact]
    public void ConversaoImplicitaDeErro_GeraFalha()
    {
        Resultado<int> resultado = ErroConflito;

        Assert.False(resultado.EhSucesso);
        Assert.Same(ErroConflito, resultado.Erro);
    }

    [Fact]
    public void ValorEmFalha_LancaInvalidOperationException()
    {
        Resultado<string> resultado = ErroConflito;

        Assert.Throws<InvalidOperationException>(() => resultado.Valor);
    }

    [Fact]
    public void Corresponder_ExecutaRamoDeSucessoOuDeFalha()
    {
        Resultado<int> sucesso = 7;
        Resultado<int> falha = ErroConflito;

        Assert.Equal("ok:7", sucesso.Corresponder(v => $"ok:{v}", e => $"erro:{e.Codigo}"));
        Assert.Equal("erro:MODULO_REGRA", falha.Corresponder(v => $"ok:{v}", e => $"erro:{e.Codigo}"));
    }

    [Fact]
    public void ResultadoSemValor_OkEFalha()
    {
        Resultado ok = Resultado.Ok();
        Resultado falha = ErroConflito;

        Assert.True(ok.EhSucesso);
        Assert.Null(ok.Erro);
        Assert.False(falha.EhSucesso);
        Assert.Equal(204, ok.Corresponder(() => 204, _ => 409));
        Assert.Equal(409, falha.Corresponder(() => 204, _ => 409));
    }

    [Fact]
    public void Erro_RejeitaCodigoForaDoPadrao()
    {
        Assert.Throws<ArgumentException>(() => new Erro("codigo_minusculo", "x", TipoErro.Conflito));
        Assert.Throws<ArgumentException>(() => new Erro("", "x", TipoErro.Conflito));
        Assert.Throws<ArgumentException>(() => new Erro("CODIGO", " ", TipoErro.Conflito));
    }

    [Fact]
    public void TipoErro_TemOsCincoTiposDaAdr()
    {
        Assert.Equal(
            ["NaoEncontrado", "Conflito", "RegraNegocio", "AcessoNegado", "NaoAutenticado"],
            Enum.GetNames<TipoErro>());
    }
}
