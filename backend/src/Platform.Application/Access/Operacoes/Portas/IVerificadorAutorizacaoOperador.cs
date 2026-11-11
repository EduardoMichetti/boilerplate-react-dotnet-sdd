namespace Platform.Application.Access.Operacoes.Portas;

/// <summary>
/// Resultado da verificação de autorização do operador contra o banco PostgreSQL.
/// </summary>
/// <param name="Autorizado">Indica se o operador está autorizado.</param>
/// <param name="LoginOperador">Login individual verificado.</param>
/// <param name="CodigoNegacao">Código de negação quando não autorizado (ex.: AUTORIZACAO_NEGADA, ALVO_DIVERGENTE).</param>
/// <param name="MensagemDiagnostico">Mensagem segura de diagnóstico para stderr.</param>
public sealed record ResultadoAutorizacaoOperador(
    bool Autorizado,
    string LoginOperador,
    string? CodigoNegacao = null,
    string? MensagemDiagnostico = null)
{
    /// <summary>
    /// Autorizado com sucesso.
    /// </summary>
    public static ResultadoAutorizacaoOperador Sucesso(string loginOperador) =>
        new(true, loginOperador);

    /// <summary>
    /// Negado por divergência de alvo ou autorização.
    /// </summary>
    public static ResultadoAutorizacaoOperador Negado(string loginOperador, string codigoNegacao, string mensagemDiagnostico) =>
        new(false, loginOperador, codigoNegacao, mensagemDiagnostico);
}

/// <summary>
/// Porta para verificar se a conexão corrente do operador no PostgreSQL é válida e autorizada
/// (<c>session_user = current_user</c>, membro do grupo operador, preflight de marcador e migrations).
/// </summary>
public interface IVerificadorAutorizacaoOperador
{
    /// <summary>
    /// Verifica a autorização do operador no banco e o preflight do tenant/ambiente alvo.
    /// </summary>
    /// <param name="idTenant">UUID do tenant alvo.</param>
    /// <param name="idAmbiente">UUID do ambiente alvo.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>O resultado da verificação.</returns>
    Task<ResultadoAutorizacaoOperador> VerificarAutorizacaoAsync(
        Guid idTenant,
        Guid idAmbiente,
        CancellationToken tokenCancelamento = default);
}
