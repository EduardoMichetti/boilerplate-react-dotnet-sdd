namespace Platform.Application.Access.Portas;

using Platform.Domain.Resultados;

/// <summary>
/// Persistência das sessões autenticadas: alterações rastreadas com <c>Versao</c> esperada, sem
/// upsert nem operação em lote. Falha de banco propaga como exceção de dependência (503), nunca
/// como “sessão ausente”.
/// </summary>
public interface IRepositorioSessoes
{
    /// <summary>
    /// Lê a <c>EpocaAcesso</c> vigente no marcador da instalação.
    /// </summary>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Geração vigente.</returns>
    Task<Guid> ObterEpocaAcessoAsync(CancellationToken tokenCancelamento);

    /// <summary>
    /// Emite a nova sessão numa única transação; com <paramref name="anterior"/>, revoga-a com a
    /// versão esperada na mesma transação. Commit antes do retorno.
    /// </summary>
    /// <param name="nova">Nova sessão.</param>
    /// <param name="anterior">Sessão do cookie recebido, quando válida.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Sessão emitida ou <c>SESSION_CONFLICT</c> quando a anterior mudou.</returns>
    Task<Resultado<SessaoEmitida>> EmitirAsync(NovaSessao nova, SessaoCorrente? anterior, CancellationToken tokenCancelamento);

    /// <summary>
    /// Lê a sessão e a conta dona, sem rastreamento.
    /// </summary>
    /// <param name="idSessao">Sessão.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Estado persistido ou nulo quando inexistente.</returns>
    Task<EstadoSessaoPersistida?> ObterAsync(Guid idSessao, CancellationToken tokenCancelamento);

    /// <summary>
    /// Atualiza o ticket de uma sessão ainda ativa com a versão esperada; nunca reativa, insere ou
    /// estende o prazo. Ticket igual ao gravado é no-op.
    /// </summary>
    /// <param name="idSessao">Sessão.</param>
    /// <param name="versaoEsperada">Versão lida.</param>
    /// <param name="ticketProtegido">Novo ticket protegido.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns><c>true</c> quando a sessão segue ativa após a operação.</returns>
    Task<bool> RenovarAsync(Guid idSessao, long versaoEsperada, byte[] ticketProtegido, CancellationToken tokenCancelamento);

    /// <summary>
    /// Revoga uma sessão ativa com a versão esperada (remove o ticket). Commit antes do retorno.
    /// </summary>
    /// <param name="idSessao">Sessão.</param>
    /// <param name="versaoEsperada">Versão lida na autenticação.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Sucesso, ou <c>AUTH_REQUIRED</c> quando a sessão já não está ativa ou mudou.</returns>
    Task<Resultado> RevogarAsync(Guid idSessao, long versaoEsperada, CancellationToken tokenCancelamento);

    /// <summary>
    /// Marca como revogadas todas as sessões ativas da conta e salva na transação ambiente do
    /// chamador (lockout, reset, desativação), sem confirmá-la.
    /// </summary>
    /// <param name="idUsuario">Conta.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Quantidade de sessões revogadas.</returns>
    Task<int> RevogarTodasDaContaAsync(Guid idUsuario, CancellationToken tokenCancelamento);
}
