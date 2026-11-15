namespace Platform.Application.Access.Operacoes.Portas;

/// <summary>
/// Porta que distingue, entre exceções de persistência, o conflito concorrente real
/// (FR-033: violação de unicidade/PK em gravação simultânea, falha de concorrência otimista,
/// serialização ou deadlock) de qualquer outra falha, sem expor tipos do provedor à aplicação.
/// </summary>
public interface IClassificadorFalhaPersistencia
{
    /// <summary>
    /// Indica se a exceção (ou alguma exceção interna) representa conflito concorrente real.
    /// </summary>
    /// <param name="excecao">Exceção capturada durante a mutação ou a confirmação da transação.</param>
    /// <returns><see langword="true"/> somente para conflito concorrente real.</returns>
    bool EhConflitoConcorrente(Exception excecao);
}
