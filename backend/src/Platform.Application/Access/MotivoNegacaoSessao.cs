namespace Platform.Application.Access;

/// <summary>
/// Motivo interno de negação de uma sessão (D37). Registrado somente no log estruturado; a
/// resposta HTTP é sempre a mesma (401 <c>AUTHENTICATION_REQUIRED</c>), sem revelar o motivo.
/// </summary>
public enum MotivoNegacaoSessao
{
    /// <summary>
    /// <c>agora ≥ UltimoUsoEm + JanelaInatividadeMinutos</c> (antes do teto).
    /// </summary>
    Inatividade,

    /// <summary>
    /// <c>agora ≥ TetoAbsolutoEm</c>; prevalece quando ambos venceram.
    /// </summary>
    TetoAbsoluto,

    /// <summary>
    /// Sessão revogada (logout, operação ou troca de sessão).
    /// </summary>
    Revogada,

    /// <summary>
    /// Referência, ticket, época, stamp ou conta inválidos.
    /// </summary>
    Invalida,

    /// <summary>
    /// Requisição sem cookie de sessão.
    /// </summary>
    Ausente,
}
