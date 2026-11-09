namespace Platform.Domain.Resultados;

/// <summary>
/// Resultado de operação sem valor: sucesso ou <see cref="Resultados.Erro"/> esperado.
/// </summary>
public sealed class Resultado
{
    private static readonly Resultado SucessoUnico = new(null);

    private Resultado(Erro? erro)
    {
        Erro = erro;
    }

    /// <summary>
    /// Indica sucesso.
    /// </summary>
    public bool EhSucesso => Erro is null;

    /// <summary>
    /// Erro da falha; nulo em sucesso.
    /// </summary>
    public Erro? Erro { get; }

    /// <summary>
    /// Sucesso sem valor.
    /// </summary>
    /// <returns>Resultado de sucesso.</returns>
    public static Resultado Ok() => SucessoUnico;

    /// <summary>
    /// Converte um erro em falha.
    /// </summary>
    /// <param name="erro">Erro esperado.</param>
    public static implicit operator Resultado(Erro erro) => new(erro ?? throw new ArgumentNullException(nameof(erro)));

    /// <summary>
    /// Executa o ramo correspondente ao resultado.
    /// </summary>
    /// <typeparam name="TRetorno">Tipo do retorno.</typeparam>
    /// <param name="sucesso">Ramo de sucesso.</param>
    /// <param name="falha">Ramo de falha.</param>
    /// <returns>Valor do ramo executado.</returns>
    public TRetorno Corresponder<TRetorno>(Func<TRetorno> sucesso, Func<Erro, TRetorno> falha)
    {
        ArgumentNullException.ThrowIfNull(sucesso);
        ArgumentNullException.ThrowIfNull(falha);
        return Erro is null ? sucesso() : falha(Erro);
    }
}

/// <summary>
/// Resultado de operação com valor: <typeparamref name="T"/> ou <see cref="Resultados.Erro"/> esperado.
/// </summary>
/// <typeparam name="T">Tipo do valor de sucesso.</typeparam>
public sealed class Resultado<T>
{
    private readonly T? _valor;

    private Resultado(T? valor, Erro? erro)
    {
        _valor = valor;
        Erro = erro;
    }

    /// <summary>
    /// Indica sucesso.
    /// </summary>
    public bool EhSucesso => Erro is null;

    /// <summary>
    /// Erro da falha; nulo em sucesso.
    /// </summary>
    public Erro? Erro { get; }

    /// <summary>
    /// Valor de sucesso.
    /// </summary>
    /// <exception cref="InvalidOperationException">Lido numa falha.</exception>
    public T Valor => Erro is null ? _valor! : throw new InvalidOperationException("Resultado de falha não possui valor.");

    /// <summary>
    /// Converte um valor em sucesso.
    /// </summary>
    /// <param name="valor">Valor.</param>
    public static implicit operator Resultado<T>(T valor) => new(valor, null);

    /// <summary>
    /// Converte um erro em falha.
    /// </summary>
    /// <param name="erro">Erro esperado.</param>
    public static implicit operator Resultado<T>(Erro erro) => new(default, erro ?? throw new ArgumentNullException(nameof(erro)));

    /// <summary>
    /// Executa o ramo correspondente ao resultado.
    /// </summary>
    /// <typeparam name="TRetorno">Tipo do retorno.</typeparam>
    /// <param name="sucesso">Ramo de sucesso.</param>
    /// <param name="falha">Ramo de falha.</param>
    /// <returns>Valor do ramo executado.</returns>
    public TRetorno Corresponder<TRetorno>(Func<T, TRetorno> sucesso, Func<Erro, TRetorno> falha)
    {
        ArgumentNullException.ThrowIfNull(sucesso);
        ArgumentNullException.ThrowIfNull(falha);
        return Erro is null ? sucesso(_valor!) : falha(Erro);
    }
}
