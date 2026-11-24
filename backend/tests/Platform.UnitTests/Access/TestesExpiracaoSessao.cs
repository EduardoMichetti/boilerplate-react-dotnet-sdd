namespace Platform.UnitTests.Access;

using Platform.Application.Access;
using Xunit;

/// <summary>
/// T138: regra pura de expiração efetiva da sessão (D31) e limitação do registro de uso (D33).
/// </summary>
public sealed class TestesExpiracaoSessao
{
    private const int Janela = 30;

    private static readonly DateTimeOffset CriadoEm = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Teto = CriadoEm.AddMinutes(600);

    [Fact]
    public void ExpiracaoEfetiva_EhOMenorEntreInatividadeETeto()
    {
        Assert.Equal(CriadoEm.AddMinutes(30), ExpiracaoSessao.CalcularExpiracaoEfetiva(CriadoEm, Janela, Teto));

        DateTimeOffset ultimoUsoTardio = Teto.AddMinutes(-10);
        Assert.Equal(Teto, ExpiracaoSessao.CalcularExpiracaoEfetiva(ultimoUsoTardio, Janela, Teto));
    }

    [Fact]
    public void UltimoUsoMais29m59s_Valida_E30m_Inatividade()
    {
        DateTimeOffset ultimoUso = CriadoEm.AddMinutes(100);

        Assert.Null(ExpiracaoSessao.Avaliar(ultimoUso.AddMinutes(29).AddSeconds(59), ultimoUso, Janela, Teto));
        Assert.Equal(MotivoNegacaoSessao.Inatividade, ExpiracaoSessao.Avaliar(ultimoUso.AddMinutes(30), ultimoUso, Janela, Teto));
    }

    [Fact]
    public void CriacaoMais599m59s_Valida_E600m_TetoMesmoComUsoRecente()
    {
        DateTimeOffset usoRecente = Teto.AddMinutes(-1);

        Assert.Null(ExpiracaoSessao.Avaliar(CriadoEm.AddMinutes(599).AddSeconds(59), usoRecente, Janela, Teto));
        Assert.Equal(MotivoNegacaoSessao.TetoAbsoluto, ExpiracaoSessao.Avaliar(CriadoEm.AddMinutes(600), usoRecente, Janela, Teto));
    }

    [Fact]
    public void AmbosVencidos_TetoPrevalece()
    {
        DateTimeOffset ultimoUso = Teto.AddMinutes(-40);

        Assert.Equal(MotivoNegacaoSessao.TetoAbsoluto, ExpiracaoSessao.Avaliar(Teto.AddMinutes(5), ultimoUso, Janela, Teto));
    }

    [Fact]
    public void DeveRegistrarUso_SomenteAPartirDe60Segundos()
    {
        DateTimeOffset ultimoUso = CriadoEm.AddMinutes(10);

        Assert.Equal(TimeSpan.FromSeconds(60), ExpiracaoSessao.IntervaloMinimoRegistroUso);
        Assert.False(ExpiracaoSessao.DeveRegistrarUso(ultimoUso.AddSeconds(30), ultimoUso));
        Assert.False(ExpiracaoSessao.DeveRegistrarUso(ultimoUso.AddTicks(599_999_990), ultimoUso));
        Assert.True(ExpiracaoSessao.DeveRegistrarUso(ultimoUso.AddSeconds(60), ultimoUso));
    }

    [Fact]
    public void InstantesForaDeUtc_Rejeitados()
    {
        DateTimeOffset comOffset = new(2026, 11, 24, 9, 0, 0, TimeSpan.FromHours(-3));

        Assert.Throws<ArgumentException>(() => ExpiracaoSessao.Avaliar(comOffset, CriadoEm, Janela, Teto));
        Assert.Throws<ArgumentException>(() => ExpiracaoSessao.Avaliar(CriadoEm, comOffset, Janela, Teto));
        Assert.Throws<ArgumentException>(() => ExpiracaoSessao.CalcularExpiracaoEfetiva(CriadoEm, Janela, comOffset));
        Assert.Throws<ArgumentException>(() => ExpiracaoSessao.DeveRegistrarUso(default, CriadoEm));
    }
}
