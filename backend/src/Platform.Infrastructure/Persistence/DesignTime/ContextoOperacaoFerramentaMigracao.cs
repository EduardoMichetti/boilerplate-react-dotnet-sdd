namespace Platform.Infrastructure.Persistence.DesignTime;

using Platform.Application.Auditoria;
using Platform.Domain.Auditoria;

/// <summary>
/// Contexto de operação do tooling de migrations. O tooling só grava o marcador (não auditável);
/// qualquer tentativa de gravar entidade auditável falha explicitamente, pois não há produtor
/// de autoria <see cref="TipoAutor.Sistema"/> na L2 nem seed de contas.
/// </summary>
internal sealed class ContextoOperacaoFerramentaMigracao : IContextoOperacao
{
    private const string Mensagem = "O tooling de migrations não grava entidades auditáveis (contas, concessões ou sessões).";

    /// <inheritdoc />
    public Guid IdOperacao => throw new InvalidOperationException(Mensagem);

    /// <inheritdoc />
    public TipoAutor TipoAutor => throw new InvalidOperationException(Mensagem);

    /// <inheritdoc />
    public string? Autor => throw new InvalidOperationException(Mensagem);

    /// <inheritdoc />
    public DateTimeOffset InstanteOperacao => throw new InvalidOperationException(Mensagem);
}
