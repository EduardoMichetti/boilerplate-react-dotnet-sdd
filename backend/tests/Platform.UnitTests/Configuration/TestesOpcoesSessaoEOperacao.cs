namespace Platform.UnitTests.Configuration;

using Microsoft.Extensions.Options;
using Platform.Application.Configuration;
using Xunit;

/// <summary>
/// T052: opções de prazo de sessão e retenção de limpeza (D21, G10), parse estrito invariante.
/// </summary>
public sealed class TestesOpcoesSessaoEOperacao
{
    private const string SegredoOutraChave = "SenhaQueNaoPodeVazar123";

    [Theory]
    [InlineData("5", 5)]
    [InlineData("30", 30)]
    [InlineData("480", 480)]
    public void PrazoSessao_AceitaValoresNaFaixa(string bruto, int esperado)
    {
        OpcoesSessao opcoes = new() { PrazoAbsolutoMinutosBruto = bruto };

        ValidateOptionsResult resultado = new ValidadorOpcoesSessao().Validate(null, opcoes);

        Assert.True(resultado.Succeeded);
        Assert.Equal(esperado, opcoes.PrazoAbsolutoMinutos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PrazoSessao_AusenteOuVazioUsaPadrao30(string? bruto)
    {
        OpcoesSessao opcoes = new() { PrazoAbsolutoMinutosBruto = bruto };

        Assert.True(new ValidadorOpcoesSessao().Validate(null, opcoes).Succeeded);
        Assert.Equal(30, opcoes.PrazoAbsolutoMinutos);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("481")]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("30.5")]
    [InlineData(" 30")]
    [InlineData("30 ")]
    [InlineData("   ")]
    [InlineData("1e2")]
    [InlineData("+30")]
    [InlineData("trinta")]
    [InlineData("99999999999")]
    public void PrazoSessao_RejeitaForaDaFaixaOuNaoInteiroComDiagnosticoSeguro(string bruto)
    {
        OpcoesSessao opcoes = new() { PrazoAbsolutoMinutosBruto = bruto };

        ValidateOptionsResult resultado = new ValidadorOpcoesSessao().Validate(null, opcoes);

        Assert.True(resultado.Failed);
        string mensagem = resultado.FailureMessage;
        Assert.Contains("Sessao:PrazoAbsolutoMinutos", mensagem, StringComparison.Ordinal);
        Assert.Contains("5–480", mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(SegredoOutraChave, mensagem, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => opcoes.PrazoAbsolutoMinutos);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("365", 365)]
    [InlineData(null, 30)]
    [InlineData("", 30)]
    public void RetencaoSessoes_AceitaFaixaEPadrao(string? bruto, int esperado)
    {
        OpcoesOperacaoAcesso opcoes = new() { LoginAutorizado = "appdb_a_op_um", RetencaoSessoesDiasBruto = bruto };

        Assert.True(new ValidadorOpcoesOperacaoAcesso().Validate(null, opcoes).Succeeded);
        Assert.Equal(esperado, opcoes.RetencaoSessoesDias);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("366")]
    [InlineData("1.5")]
    [InlineData(" 30")]
    [InlineData("dez")]
    public void RetencaoSessoes_RejeitaForaDaFaixaOuNaoInteiro(string bruto)
    {
        OpcoesOperacaoAcesso opcoes = new() { LoginAutorizado = SegredoOutraChave, RetencaoSessoesDiasBruto = bruto };

        ValidateOptionsResult resultado = new ValidadorOpcoesOperacaoAcesso().Validate(null, opcoes);

        Assert.True(resultado.Failed);
        Assert.Contains("OperacaoAcesso:RetencaoSessoesDias", resultado.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("1–365", resultado.FailureMessage, StringComparison.Ordinal);
        Assert.DoesNotContain(SegredoOutraChave, resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void LoginAutorizado_Obrigatorio(string? login)
    {
        OpcoesOperacaoAcesso opcoes = new() { LoginAutorizado = login };

        ValidateOptionsResult resultado = new ValidadorOpcoesOperacaoAcesso().Validate(null, opcoes);

        Assert.True(resultado.Failed);
        Assert.Contains("OperacaoAcesso:LoginAutorizado", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", true, 30)]
    [InlineData(null, true, 30)]
    [InlineData("007", true, 7)]
    [InlineData("-1", false, 0)]
    [InlineData("1,0", false, 0)]
    public void LeitorInteiro_ParseEstritoInvariante(string? bruto, bool valido, int esperado)
    {
        bool lido = LeitorInteiroConfiguracao.TentarLer(bruto, 0, 100, 30, out int valor);

        Assert.Equal(valido, lido);
        if (valido)
        {
            Assert.Equal(esperado, valor);
        }
    }
}
