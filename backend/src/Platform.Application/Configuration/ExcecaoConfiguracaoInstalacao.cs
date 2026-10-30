namespace Platform.Application.Configuration;

/// <summary>
/// Exceção lançada quando a configuração da instalação do inquilino é inválida ou incompleta.
/// </summary>
/// <param name="erros">Lista de mensagens detalhadas sobre os erros de validação encontrados.</param>
public sealed class ExcecaoConfiguracaoInstalacao(IReadOnlyList<string> erros)
    : Exception("A configuração da instalação é inválida.")
{
    /// <summary>
    /// Lista somente leitura dos erros de validação da configuração da instalação.
    /// </summary>
    public IReadOnlyList<string> Erros { get; } = Array.AsReadOnly(erros.ToArray());
}
