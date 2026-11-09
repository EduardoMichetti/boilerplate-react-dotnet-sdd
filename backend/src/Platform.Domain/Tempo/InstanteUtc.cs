namespace Platform.Domain.Tempo;

/// <summary>
/// Padrão único de instantes (Constituição IX): UTC com offset zero e precisão de
/// microssegundos, igual ao <c>timestamptz</c> do PostgreSQL. Helper puro: não lê relógio.
/// </summary>
public static class InstanteUtc
{
    private const long TicksPorMicrossegundo = TimeSpan.TicksPerMillisecond / 1000;

    /// <summary>
    /// Converte para offset zero e trunca para microssegundos.
    /// </summary>
    /// <param name="valor">Instante em qualquer offset.</param>
    /// <returns>Instante UTC truncado para microssegundos.</returns>
    public static DateTimeOffset Normalizar(DateTimeOffset valor)
    {
        DateTimeOffset utc = valor.ToUniversalTime();
        return utc.AddTicks(-(utc.Ticks % TicksPorMicrossegundo));
    }

    /// <summary>
    /// Rejeita instante não definido (<c>default</c>) ou com offset diferente de zero.
    /// </summary>
    /// <param name="valor">Instante a validar.</param>
    /// <param name="nomeCampo">Nome do campo citado na mensagem de erro.</param>
    /// <exception cref="ArgumentException">Instante ausente ou fora de UTC.</exception>
    public static void Validar(DateTimeOffset valor, string nomeCampo)
    {
        if (valor == default)
        {
            throw new ArgumentException($"{nomeCampo} deve ser um instante UTC definido.", nomeCampo);
        }

        if (valor.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException($"{nomeCampo} deve estar em UTC (offset zero).", nomeCampo);
        }
    }
}
