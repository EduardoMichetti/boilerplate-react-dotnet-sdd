namespace Platform.Domain.Access;

using System.Text.RegularExpressions;

/// <summary>
/// Catálogo fechado de códigos de permissão, declarado no código-fonte e versionado com o
/// produto. Código publicado nunca é reutilizado nem renomeado sem migration de dados.
/// </summary>
public static partial class CodigosPermissao
{
    /// <summary>
    /// Formato obrigatório dos códigos (decisão G7), também validado por CHECK no banco.
    /// </summary>
    public const string PadraoFormato = @"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$";

    /// <summary>
    /// Permite consultar o próprio contexto de acesso no tenant (<c>GET /api/v1/context</c>).
    /// </summary>
    public const string FundacaoContextoLer = "foundation.context.read";

    /// <summary>
    /// Todos os códigos conhecidos do catálogo.
    /// </summary>
    public static IReadOnlyList<string> Todos { get; } = [FundacaoContextoLer];

    /// <summary>
    /// Indica se o código pertence ao catálogo (comparação ordinal exata).
    /// </summary>
    /// <param name="codigo">Código a verificar.</param>
    /// <returns><c>true</c> somente para código do catálogo.</returns>
    public static bool EhConhecido(string? codigo) =>
        codigo is not null && Todos.Contains(codigo, StringComparer.Ordinal);

    /// <summary>
    /// Indica se o código respeita o formato e o tamanho (1–128).
    /// </summary>
    /// <param name="codigo">Código a verificar.</param>
    /// <returns><c>true</c> quando o formato é válido.</returns>
    public static bool FormatoValido(string? codigo) =>
        codigo is { Length: >= 1 and <= 128 } && ExpressaoFormato().IsMatch(codigo);

    [GeneratedRegex(PadraoFormato, RegexOptions.CultureInvariant)]
    private static partial Regex ExpressaoFormato();
}
