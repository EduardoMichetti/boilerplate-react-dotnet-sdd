namespace Platform.Operations;

using Platform.Application.Auditoria;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Domain.Tempo;

/// <summary>
/// Contexto operacional confiável para o host Platform.Operations (FR-033).
/// Utiliza tipo de autor <see cref="TipoAutor.Operador"/> e o login PostgreSQL verificado.
/// </summary>
public sealed class ContextoOperacaoOperador : IContextoOperacao
{
    /// <inheritdoc />
    public Guid IdOperacao { get; }

    /// <inheritdoc />
    public TipoAutor TipoAutor => TipoAutor.Operador;

    /// <inheritdoc />
    public string? Autor { get; }

    /// <inheritdoc />
    public DateTimeOffset InstanteOperacao { get; }

    /// <summary>
    /// Construtor que inicializa o instante normalizado em UTC com microssegundos e gera o UUID v7 de correlação.
    /// </summary>
    /// <param name="relogio">Provedor de tempo do host.</param>
    /// <param name="loginOperador">Login PostgreSQL verificado do operador.</param>
    public ContextoOperacaoOperador(TimeProvider relogio, string? loginOperador = null)
    {
        ArgumentNullException.ThrowIfNull(relogio);
        InstanteOperacao = InstanteUtc.Normalizar(relogio.GetUtcNow());
        IdOperacao = IdentificadorV7.Gerar(InstanteOperacao);
        Autor = loginOperador;
    }
}
