namespace Platform.Domain.Access;

/// <summary>
/// Resultado registrado de uma operação restrita.
/// </summary>
public enum ResultadoOperacaoAcesso
{
    /// <summary>Operação concluída e persistida.</summary>
    Sucesso = 0,

    /// <summary>Operação negada antes de mutação.</summary>
    Negado = 1,

    /// <summary>Operação falhou e foi desfeita.</summary>
    Falha = 2
}
