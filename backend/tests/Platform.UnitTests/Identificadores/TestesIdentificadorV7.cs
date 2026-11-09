namespace Platform.UnitTests.Identificadores;

using Platform.Domain.Identificadores;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T051: UUID v7 gerado a partir do instante da operação (relógio controlável).
/// </summary>
public sealed class TestesIdentificadorV7
{
    [Fact]
    public void Gerar_ProduzVersao7ComVarianteRfc()
    {
        RelogioControlavel relogio = new(new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero));

        Guid id = IdentificadorV7.Gerar(relogio.GetUtcNow());

        Assert.Equal(7, id.Version);
        Assert.Equal(0b10, id.Variant >> 2);
    }

    [Fact]
    public void Gerar_TimestampEmMilissegundosIgualAoInstante()
    {
        DateTimeOffset instante = new DateTimeOffset(2026, 11, 8, 12, 34, 56, TimeSpan.Zero).AddMilliseconds(789).AddTicks(1234);
        RelogioControlavel relogio = new(instante);

        Guid id = IdentificadorV7.Gerar(relogio.GetUtcNow());

        Assert.Equal(instante.ToUnixTimeMilliseconds(), ExtrairMilissegundosUnix(id));
    }

    [Fact]
    public void Gerar_RejeitaOffsetDiferenteDeZero()
    {
        DateTimeOffset comOffset = new(2026, 11, 8, 12, 0, 0, TimeSpan.FromHours(-3));

        Assert.Throws<ArgumentException>(() => IdentificadorV7.Gerar(comOffset));
    }

    [Fact]
    public void Gerar_RejeitaDefault()
    {
        Assert.Throws<ArgumentException>(() => IdentificadorV7.Gerar(default));
    }

    [Fact]
    public void Gerar_IdsDistintosNoMesmoInstante()
    {
        DateTimeOffset instante = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

        Assert.NotEqual(IdentificadorV7.Gerar(instante), IdentificadorV7.Gerar(instante));
    }

    private static long ExtrairMilissegundosUnix(Guid id)
    {
        byte[] bytes = id.ToByteArray(bigEndian: true);
        long milissegundos = 0;
        for (int i = 0; i < 6; i++)
        {
            milissegundos = (milissegundos << 8) | bytes[i];
        }

        return milissegundos;
    }
}
