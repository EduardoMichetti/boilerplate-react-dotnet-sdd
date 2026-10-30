namespace Platform.Domain.Tenancy;

/// <summary>
/// Exceção de domínio lançada quando a identidade do banco de dados configurada não corresponde a esta instalação.
/// </summary>
public sealed class ExcecaoIsolamentoInquilino()
    : Exception("A identidade do banco de dados configurada não corresponde a esta instalação.");
