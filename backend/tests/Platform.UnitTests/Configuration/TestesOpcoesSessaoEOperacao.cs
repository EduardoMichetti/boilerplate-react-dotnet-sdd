namespace Platform.UnitTests.Configuration;

using Microsoft.Extensions.Options;
using Platform.Application.Configuration;
using Xunit;

/// <summary>
/// T052/T137: opções de sessão por inatividade + teto absoluto (D31, L2.1), chave obsoleta
/// <c>Sessao:PrazoAbsolutoMinutos</c> e retenção de limpeza (G10), parse estrito invariante.
/// </summary>
public sealed class TestesOpcoesSessaoEOperacao
{
    private const string SegredoOutraChave = "SenhaQueNaoPodeVazar123";

    [Theory]
    [InlineData("5", 5)]
    [InlineData("30", 30)]
    [InlineData("240", 240)]
    public void InatividadeSessao_AceitaValoresNaFaixa(string bruto, int esperado)
    {
        OpcoesSessao opcoes = new() { InatividadeMinutosBruto = bruto };

        ValidateOptionsResult resultado = new ValidadorOpcoesSessao().Validate(null, opcoes);

        Assert.True(resultado.Succeeded);
        Assert.Equal(esperado, opcoes.InatividadeMinutos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void InatividadeSessao_AusenteOuVazioUsaPadrao30(string? bruto)
    {
        OpcoesSessao opcoes = new() { InatividadeMinutosBruto = bruto };

        Assert.True(new ValidadorOpcoesSessao().Validate(null, opcoes).Succeeded);
        Assert.Equal(30, opcoes.InatividadeMinutos);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("241")]
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
    public void InatividadeSessao_RejeitaForaDaFaixaOuNaoInteiroComDiagnosticoSeguro(string bruto)
    {
        OpcoesSessao opcoes = new() { InatividadeMinutosBruto = bruto, TetoAbsolutoMinutosBruto = "719" };

        ValidateOptionsResult resultado = new ValidadorOpcoesSessao().Validate(null, opcoes);

        Assert.True(resultado.Failed);
        string mensagem = resultado.FailureMessage;
        Assert.Contains("Sessao:InatividadeMinutos", mensagem, StringComparison.Ordinal);
        Assert.Contains("5–240", mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("719", mensagem, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => opcoes.InatividadeMinutos);
    }

    [Theory]
    [InlineData("30", 30)]
    [InlineData("600", 600)]
    [InlineData("720", 720)]
    public void TetoSessao_AceitaValoresNaFaixa(string bruto, int esperado)
    {
        OpcoesSessao opcoes = new() { TetoAbsolutoMinutosBruto = bruto };

        ValidateOptionsResult resultado = new ValidadorOpcoesSessao().Validate(null, opcoes);

        Assert.True(resultado.Succeeded);
        Assert.Equal(esperado, opcoes.TetoAbsolutoMinutos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void TetoSessao_AusenteOuVazioUsaPadrao600(string? bruto)
    {
        OpcoesSessao opcoes = new() { TetoAbsolutoMinutosBruto = bruto };

        Assert.True(new ValidadorOpcoesSessao().Validate(null, opcoes).Succeeded);
        Assert.Equal(600, opcoes.TetoAbsolutoMinutos);
    }

    [Theory]
    [InlineData("29")]
    [InlineData("721")]
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
    public void TetoSessao_RejeitaForaDaFaixaOuNaoInteiroComDiagnosticoSeguro(string bruto)
    {
        OpcoesSessao opcoes = new() { TetoAbsolutoMinutosBruto = bruto, InatividadeMinutosBruto = "17" };

        ValidateOptionsResult resultado = new ValidadorOpcoesSessao().Validate(null, opcoes);

        Assert.True(resultado.Failed);
        string mensagem = resultado.FailureMessage;
        Assert.Contains("Sessao:TetoAbsolutoMinutos", mensagem, StringComparison.Ordinal);
        Assert.Contains("30–720", mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain("17", mensagem, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => opcoes.TetoAbsolutoMinutos);
    }

    [Fact]
    public void InatividadeMaiorQueTeto_Rejeitada()
    {
        OpcoesSessao opcoes = new() { InatividadeMinutosBruto = "60", TetoAbsolutoMinutosBruto = "30" };

        ValidateOptionsResult resultado = new ValidadorOpcoesSessao().Validate(null, opcoes);

        Assert.True(resultado.Failed);
        Assert.Contains("Sessao:InatividadeMinutos maior que Sessao:TetoAbsolutoMinutos", resultado.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void InatividadeIgualAoTeto_Aceita()
    {
        OpcoesSessao opcoes = new() { InatividadeMinutosBruto = "30", TetoAbsolutoMinutosBruto = "30" };

        Assert.True(new ValidadorOpcoesSessao().Validate(null, opcoes).Succeeded);
        Assert.Equal(30, opcoes.InatividadeMinutos);
        Assert.Equal(30, opcoes.TetoAbsolutoMinutos);
    }

    [Theory]
    [InlineData("30")]
    [InlineData(SegredoOutraChave)]
    public void PrazoAbsolutoObsoleto_ComValor_RejeitadoIndicandoChavesNovas(string bruto)
    {
        OpcoesSessao opcoes = new() { PrazoAbsolutoMinutosObsoletoBruto = bruto };

        ValidateOptionsResult resultado = new ValidadorOpcoesSessao().Validate(null, opcoes);

        Assert.True(resultado.Failed);
        string mensagem = resultado.FailureMessage;
        Assert.Contains("Sessao:PrazoAbsolutoMinutos", mensagem, StringComparison.Ordinal);
        Assert.Contains("Sessao:InatividadeMinutos", mensagem, StringComparison.Ordinal);
        Assert.Contains("Sessao:TetoAbsolutoMinutos", mensagem, StringComparison.Ordinal);
        Assert.DoesNotContain(SegredoOutraChave, mensagem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void PrazoAbsolutoObsoleto_AusenteOuVazio_Aceito(string? bruto)
    {
        OpcoesSessao opcoes = new() { PrazoAbsolutoMinutosObsoletoBruto = bruto };

        Assert.True(new ValidadorOpcoesSessao().Validate(null, opcoes).Succeeded);
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
