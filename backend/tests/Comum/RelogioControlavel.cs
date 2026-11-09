namespace Platform.Testes.Comum;

/// <summary>
/// Relógio de teste controlável: devolve sempre um instante explícito com offset zero,
/// alterado somente por <see cref="Avancar"/> ou <see cref="DefinirAgora"/>.
/// </summary>
public sealed class RelogioControlavel : TimeProvider
{
    private readonly Lock _trava = new();
    private DateTimeOffset _agora;

    /// <summary>
    /// Cria o relógio no instante inicial informado (convertido para offset zero).
    /// </summary>
    /// <param name="instanteInicial">Instante inicial explícito; não pode ser <c>default</c>.</param>
    public RelogioControlavel(DateTimeOffset instanteInicial)
    {
        DefinirAgora(instanteInicial);
    }

    /// <inheritdoc />
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow()
    {
        lock (_trava)
        {
            return _agora;
        }
    }

    /// <summary>
    /// Avança o relógio pelo intervalo informado (não negativo).
    /// </summary>
    /// <param name="intervalo">Intervalo a avançar.</param>
    public void Avancar(TimeSpan intervalo)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(intervalo, TimeSpan.Zero);
        lock (_trava)
        {
            _agora = _agora.Add(intervalo);
        }
    }

    /// <summary>
    /// Define o instante atual (convertido para offset zero).
    /// </summary>
    /// <param name="instante">Novo instante; não pode ser <c>default</c>.</param>
    public void DefinirAgora(DateTimeOffset instante)
    {
        if (instante == default)
        {
            throw new ArgumentException("O instante do relógio de teste deve ser explícito.", nameof(instante));
        }

        lock (_trava)
        {
            _agora = instante.ToUniversalTime();
        }
    }
}
