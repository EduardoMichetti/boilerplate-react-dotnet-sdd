namespace Platform.Domain.Identificadores;

/// <summary>
/// Gera identificadores técnicos UUID v7 a partir do instante da operação (vindo do
/// <see cref="TimeProvider"/>), nunca do relógio do sistema.
/// </summary>
public static class IdentificadorV7
{
    /// <summary>
    /// Gera um UUID v7 cujo timestamp em milissegundos corresponde ao instante informado.
    /// </summary>
    /// <param name="instante">Instante da operação, com offset zero e não <c>default</c>.</param>
    /// <returns>UUID v7 não vazio.</returns>
    /// <exception cref="ArgumentException">Instante ausente ou fora de UTC.</exception>
    public static Guid Gerar(DateTimeOffset instante)
    {
        if (instante == default)
        {
            throw new ArgumentException("O instante do identificador deve ser definido.", nameof(instante));
        }

        if (instante.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("O instante do identificador deve estar em UTC (offset zero).", nameof(instante));
        }

        return Guid.CreateVersion7(instante);
    }
}
