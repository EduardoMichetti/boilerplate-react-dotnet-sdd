namespace Platform.Domain.Resultados;

using System.Text.RegularExpressions;

/// <summary>
/// Falha de regra esperada, declarada uma única vez num catálogo <c>Erros&lt;Modulo&gt;</c>.
/// </summary>
public sealed partial record Erro
{
    /// <summary>
    /// Cria um erro com código estável, mensagem pt-BR (sem valores de entrada) e tipo.
    /// </summary>
    /// <param name="codigo">Código estável no padrão <c>^[A-Z][A-Z0-9_]+$</c>.</param>
    /// <param name="mensagem">Título pt-BR exibido ao consumidor.</param>
    /// <param name="tipo">Categoria que define o status HTTP.</param>
    /// <exception cref="ArgumentException">Código fora do padrão ou mensagem vazia.</exception>
    public Erro(string codigo, string mensagem, TipoErro tipo)
    {
        if (string.IsNullOrEmpty(codigo) || !PadraoCodigo().IsMatch(codigo))
        {
            throw new ArgumentException("Código de erro deve seguir ^[A-Z][A-Z0-9_]+$.", nameof(codigo));
        }

        if (string.IsNullOrWhiteSpace(mensagem))
        {
            throw new ArgumentException("Mensagem de erro é obrigatória.", nameof(mensagem));
        }

        Codigo = codigo;
        Mensagem = mensagem;
        Tipo = tipo;
    }

    /// <summary>
    /// Código estável publicado no campo <c>code</c>.
    /// </summary>
    public string Codigo { get; }

    /// <summary>
    /// Título pt-BR.
    /// </summary>
    public string Mensagem { get; }

    /// <summary>
    /// Categoria da falha.
    /// </summary>
    public TipoErro Tipo { get; }

    [GeneratedRegex("^[A-Z][A-Z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex PadraoCodigo();
}
