namespace Platform.UnitTests.Tempo;

using Platform.Domain.Tempo;
using Xunit;

/// <summary>
/// T050: normalização para UTC/microssegundos e validação de instantes.
/// </summary>
public sealed class TestesInstanteUtc
{
    [Fact]
    public void Normalizar_ConverteParaOffsetZero()
    {
        DateTimeOffset local = new(2026, 11, 8, 10, 30, 0, TimeSpan.FromHours(-3));

        DateTimeOffset normalizado = InstanteUtc.Normalizar(local);

        Assert.Equal(TimeSpan.Zero, normalizado.Offset);
        Assert.Equal(new DateTimeOffset(2026, 11, 8, 13, 30, 0, TimeSpan.Zero), normalizado);
    }

    [Fact]
    public void Normalizar_TruncaParaMicrossegundos()
    {
        DateTimeOffset comTicks = new DateTimeOffset(2026, 11, 8, 13, 30, 0, TimeSpan.Zero).AddTicks(1_234_567);

        DateTimeOffset normalizado = InstanteUtc.Normalizar(comTicks);

        Assert.Equal(0, normalizado.Ticks % 10);
        Assert.Equal(comTicks.AddTicks(-7), normalizado);
    }

    [Fact]
    public void Normalizar_EhIdempotente()
    {
        DateTimeOffset valor = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(2)).AddTicks(99);

        DateTimeOffset uma = InstanteUtc.Normalizar(valor);

        Assert.Equal(uma, InstanteUtc.Normalizar(uma));
    }

    [Fact]
    public void Validar_AceitaInstanteUtcExplicito()
    {
        InstanteUtc.Validar(new DateTimeOffset(2026, 11, 8, 0, 0, 0, TimeSpan.Zero), "CriadoEm");
    }

    [Fact]
    public void Validar_RejeitaDefaultCitandoOCampo()
    {
        ArgumentException excecao = Assert.Throws<ArgumentException>(() => InstanteUtc.Validar(default, "ExpiraEm"));

        Assert.Contains("ExpiraEm", excecao.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validar_RejeitaOffsetDiferenteDeZeroCitandoOCampo()
    {
        DateTimeOffset comOffset = new(2026, 11, 8, 0, 0, 0, TimeSpan.FromHours(-3));

        ArgumentException excecao = Assert.Throws<ArgumentException>(() => InstanteUtc.Validar(comOffset, "RevogadaEm"));

        Assert.Contains("RevogadaEm", excecao.Message, StringComparison.Ordinal);
    }
}
