namespace Platform.Domain.Tenancy;

/// <summary>
/// Representa o estado operacional configurado para a instalação dedicada do inquilino.
/// </summary>
public enum EstadoInquilino
{
    /// <summary>
    /// Inquilino inativo ou em janela de manutenção.
    /// </summary>
    Inativo = 0,

    /// <summary>
    /// Inquilino ativo e operacional para receber requisições de negócio.
    /// </summary>
    Ativo = 1
}
