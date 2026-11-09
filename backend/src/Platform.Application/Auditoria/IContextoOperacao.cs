namespace Platform.Application.Auditoria;

using Platform.Domain.Auditoria;

/// <summary>
/// Contexto confiável da operação corrente, fornecido por cada host (API por requisição,
/// host operacional por comando). Nunca vem de DTO ou argumento do consumidor.
/// </summary>
public interface IContextoOperacao
{
    /// <summary>
    /// UUID v7 de correlação da operação, gerado pelo servidor a partir de <see cref="InstanteOperacao"/>.
    /// </summary>
    Guid IdOperacao { get; }

    /// <summary>
    /// Tipo do autor efetivo da operação.
    /// </summary>
    TipoAutor TipoAutor { get; }

    /// <summary>
    /// Referência textual do autor (login do operador ou Id do usuário verificado); nula para anônimo.
    /// </summary>
    string? Autor { get; }

    /// <summary>
    /// Instante único da operação, lido uma vez do <see cref="TimeProvider"/> e normalizado (UTC, microssegundos).
    /// </summary>
    DateTimeOffset InstanteOperacao { get; }
}
