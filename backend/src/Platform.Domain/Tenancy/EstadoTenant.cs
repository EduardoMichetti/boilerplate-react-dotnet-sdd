namespace Platform.Domain.Tenancy;

/// <summary>
/// Representa o estado operacional configurado para a instalação dedicada do tenant.
/// </summary>
public enum EstadoTenant
{
    /// <summary>
    /// Tenant inativo ou em janela de manutenção.
    /// </summary>
    Inativo = 0,

    /// <summary>
    /// Tenant ativo e operacional para receber requisições de negócio.
    /// </summary>
    Ativo = 1
}
