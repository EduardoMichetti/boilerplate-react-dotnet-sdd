namespace Platform.Domain.Auditoria;

/// <summary>
/// Entidade com token de concorrência numérico incrementado a cada alteração efetiva.
/// </summary>
public interface IEntidadeVersionada
{
    /// <summary>
    /// Versão positiva da linha, usada como token de concorrência otimista.
    /// </summary>
    long Versao { get; set; }
}
