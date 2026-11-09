namespace Platform.Application.Configuration;

using System.Globalization;

/// <summary>
/// Parse estrito de opções inteiras (D21/G10): somente dígitos decimais, sem sinal,
/// espaços, separadores ou expoente, com cultura invariante. Valor ausente ou vazio
/// (<c>""</c>) equivale a ausente e usa o padrão.
/// </summary>
public static class LeitorInteiroConfiguracao
{
    /// <summary>
    /// Tenta ler um inteiro dentro da faixa inclusiva informada.
    /// </summary>
    /// <param name="valorBruto">Texto da configuração (pode ser nulo ou vazio).</param>
    /// <param name="minimo">Mínimo inclusivo.</param>
    /// <param name="maximo">Máximo inclusivo.</param>
    /// <param name="padrao">Valor usado quando ausente ou vazio.</param>
    /// <param name="valor">Valor lido ou padrão; zero quando inválido.</param>
    /// <returns><c>true</c> quando ausente/vazio ou inteiro válido na faixa.</returns>
    public static bool TentarLer(string? valorBruto, int minimo, int maximo, int padrao, out int valor)
    {
        if (string.IsNullOrEmpty(valorBruto))
        {
            valor = padrao;
            return true;
        }

        if (int.TryParse(valorBruto, NumberStyles.None, CultureInfo.InvariantCulture, out int lido)
            && lido >= minimo
            && lido <= maximo)
        {
            valor = lido;
            return true;
        }

        valor = 0;
        return false;
    }
}
