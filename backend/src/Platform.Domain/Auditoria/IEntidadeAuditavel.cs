namespace Platform.Domain.Auditoria;

/// <summary>
/// Metadados de criação e última alteração comuns às entidades mutáveis auditadas.
/// Preenchidos centralmente a partir do contexto da operação; nunca vêm de payloads.
/// </summary>
public interface IEntidadeAuditavel
{
    /// <summary>
    /// Instante UTC da criação (concessão, emissão etc.).
    /// </summary>
    DateTimeOffset CriadoEm { get; set; }

    /// <summary>
    /// Tipo do autor da criação.
    /// </summary>
    TipoAutor TipoAutorCriacao { get; set; }

    /// <summary>
    /// Referência textual do autor da criação (até 256); nula somente para <see cref="TipoAutor.Anonimo"/>.
    /// </summary>
    string? AutorCriacao { get; set; }

    /// <summary>
    /// UUID de correlação da operação que criou o registro.
    /// </summary>
    Guid IdOperacaoCriacao { get; set; }

    /// <summary>
    /// Instante UTC da última alteração efetiva; nulo se nunca alterado.
    /// </summary>
    DateTimeOffset? AlteradoEm { get; set; }

    /// <summary>
    /// Tipo do autor da última alteração; nulo se nunca alterado.
    /// </summary>
    TipoAutor? TipoAutorAlteracao { get; set; }

    /// <summary>
    /// Referência textual do autor da última alteração; nula para ação anônima ou sem alteração.
    /// </summary>
    string? AutorAlteracao { get; set; }

    /// <summary>
    /// UUID de correlação da última alteração; obrigatório quando <see cref="AlteradoEm"/> está preenchido.
    /// </summary>
    Guid? IdOperacaoAlteracao { get; set; }
}
