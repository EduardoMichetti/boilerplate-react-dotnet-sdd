namespace Platform.Operations;

using Platform.Application.Auditoria;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Domain.Tempo;

/// <summary>
/// Contexto operacional confiável para o host Platform.Operations (FR-033).
/// Utiliza tipo de autor <see cref="TipoAutor.Operador"/>; o <see cref="Autor"/> só existe depois
/// que o verificador de autorização confirma o login PostgreSQL individual na conexão.
/// </summary>
public sealed class ContextoOperacaoOperador : IContextoOperacao, IRegistroOperadorVerificado
{
    /// <summary>
    /// Construtor que inicializa o instante normalizado em UTC com microssegundos e gera o UUID v7 de correlação.
    /// </summary>
    /// <param name="relogio">Provedor de tempo do host.</param>
    public ContextoOperacaoOperador(TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);
        InstanteOperacao = InstanteUtc.Normalizar(relogio.GetUtcNow());
        IdOperacao = IdentificadorV7.Gerar(InstanteOperacao);
    }

    /// <inheritdoc />
    public Guid IdOperacao { get; }

    /// <inheritdoc />
    public TipoAutor TipoAutor => TipoAutor.Operador;

    /// <inheritdoc />
    public string? Autor { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset InstanteOperacao { get; }

    /// <inheritdoc />
    public void DefinirOperadorVerificado(string loginOperador)
    {
        if (string.IsNullOrWhiteSpace(loginOperador))
        {
            throw new ArgumentException("O login verificado do operador deve ser informado.", nameof(loginOperador));
        }

        if (Autor is not null && !string.Equals(Autor, loginOperador, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("O operador verificado da operação já foi definido com outro login.");
        }

        Autor = loginOperador;
    }
}
