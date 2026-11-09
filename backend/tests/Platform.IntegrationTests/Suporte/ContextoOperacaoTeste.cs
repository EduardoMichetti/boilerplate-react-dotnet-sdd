namespace Platform.IntegrationTests.Suporte;

using Platform.Application.Auditoria;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Domain.Tempo;

/// <summary>
/// Contexto de operação de teste: instante lido uma vez do relógio informado e
/// <see cref="IdOperacao"/> UUID v7 desse instante.
/// </summary>
public sealed class ContextoOperacaoTeste : IContextoOperacao
{
    private ContextoOperacaoTeste(DateTimeOffset instante, TipoAutor tipoAutor, string? autor)
    {
        InstanteOperacao = instante;
        IdOperacao = IdentificadorV7.Gerar(instante);
        TipoAutor = tipoAutor;
        Autor = autor;
    }

    /// <inheritdoc />
    public Guid IdOperacao { get; }

    /// <inheritdoc />
    public TipoAutor TipoAutor { get; }

    /// <inheritdoc />
    public string? Autor { get; }

    /// <inheritdoc />
    public DateTimeOffset InstanteOperacao { get; }

    /// <summary>
    /// Cria um contexto lendo o instante do relógio uma única vez.
    /// </summary>
    /// <param name="relogio">Relógio (normalmente <c>RelogioControlavel</c>).</param>
    /// <param name="tipoAutor">Tipo do autor.</param>
    /// <param name="autor">Referência textual do autor; nula para anônimo.</param>
    /// <returns>Contexto da operação.</returns>
    public static ContextoOperacaoTeste Criar(TimeProvider relogio, TipoAutor tipoAutor = TipoAutor.Operador, string? autor = "operador_teste") =>
        new(InstanteUtc.Normalizar(relogio.GetUtcNow()), tipoAutor, tipoAutor == TipoAutor.Anonimo ? null : autor);
}
