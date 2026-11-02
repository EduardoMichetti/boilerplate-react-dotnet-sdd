namespace Platform.Domain.Tenancy;

/// <summary>
/// Exceção de domínio lançada quando a identidade do banco de dados configurada não corresponde a esta instalação do tenant.
/// </summary>
public sealed class ExcecaoIsolamentoTenant()
    : Exception("A identidade do banco de dados configurada não corresponde a esta instalação.");
