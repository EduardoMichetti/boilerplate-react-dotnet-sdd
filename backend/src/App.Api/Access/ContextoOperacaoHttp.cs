namespace App.Api.Access;

using System.Diagnostics;
using Platform.Application.Auditoria;
using Platform.Domain.Auditoria;
using Platform.Domain.Identificadores;
using Platform.Domain.Tempo;

/// <summary>
/// Contexto confiável da requisição (scoped): <see cref="InstanteOperacao"/> lido uma vez do
/// <see cref="TimeProvider"/>, <see cref="IdOperacao"/> UUID v7 desse instante e autoria anônima
/// até a verificação da credencial ou da sessão no servidor.
/// </summary>
public sealed class ContextoOperacaoHttp : IContextoOperacao, IRegistroUsuarioVerificado
{
    /// <summary>
    /// Cria o contexto e registra a correlação entre <see cref="IdOperacao"/> e o trace da requisição.
    /// </summary>
    /// <param name="relogio">Fonte única do instante atual.</param>
    /// <param name="registrador">Registrador.</param>
    public ContextoOperacaoHttp(TimeProvider relogio, ILogger<ContextoOperacaoHttp> registrador)
    {
        ArgumentNullException.ThrowIfNull(relogio);
        ArgumentNullException.ThrowIfNull(registrador);
        InstanteOperacao = InstanteUtc.Normalizar(relogio.GetUtcNow());
        IdOperacao = IdentificadorV7.Gerar(InstanteOperacao);
        registrador.LogDebug(
            new EventId(2000, "OperacaoCorrelacionada"),
            "Operação {IdOperacao} correlacionada ao trace {TraceId}.",
            IdOperacao,
            Activity.Current?.TraceId.ToHexString());
    }

    /// <inheritdoc />
    public Guid IdOperacao { get; }

    /// <inheritdoc />
    public TipoAutor TipoAutor { get; private set; } = TipoAutor.Anonimo;

    /// <inheritdoc />
    public string? Autor { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset InstanteOperacao { get; }

    /// <inheritdoc />
    public void DefinirUsuarioVerificado(Guid idUsuario)
    {
        if (idUsuario == Guid.Empty)
        {
            throw new ArgumentException("O usuário verificado deve ser um UUID não vazio.", nameof(idUsuario));
        }

        TipoAutor = TipoAutor.Usuario;
        Autor = idUsuario.ToString();
    }
}
