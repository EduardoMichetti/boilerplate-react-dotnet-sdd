namespace Platform.Domain.Auditoria;

/// <summary>
/// Base abstrata com somente os metadados de auditoria (sem PK genérica nem exclusão lógica).
/// </summary>
public abstract class EntidadeAuditavel : IEntidadeAuditavel
{
    /// <summary>
    /// Instante UTC da criação, obrigatório (timestamptz).
    /// </summary>
    public DateTimeOffset CriadoEm { get; set; }

    /// <summary>
    /// Tipo do autor da criação.
    /// </summary>
    public TipoAutor TipoAutorCriacao { get; set; }

    /// <summary>
    /// Referência textual do autor da criação (string até 256); nula somente para Anonimo.
    /// </summary>
    public string? AutorCriacao { get; set; }

    /// <summary>
    /// UUID de correlação gerado pelo servidor, obrigatório.
    /// </summary>
    public Guid IdOperacaoCriacao { get; set; }

    /// <summary>
    /// Instante UTC opcional, preenchido somente quando há alteração efetiva.
    /// </summary>
    public DateTimeOffset? AlteradoEm { get; set; }

    /// <summary>
    /// Tipo do autor da última alteração, opcional até a primeira alteração.
    /// </summary>
    public TipoAutor? TipoAutorAlteracao { get; set; }

    /// <summary>
    /// Referência textual do autor da última alteração; nula para ação anônima ou sem alteração.
    /// </summary>
    public string? AutorAlteracao { get; set; }

    /// <summary>
    /// UUID de correlação, obrigatório quando <see cref="AlteradoEm"/> estiver preenchido.
    /// </summary>
    public Guid? IdOperacaoAlteracao { get; set; }
}
