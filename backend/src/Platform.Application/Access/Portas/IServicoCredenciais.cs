namespace Platform.Application.Access.Portas;

/// <summary>
/// Verificação de senha e lockout pela conta local (Identity), com contagem concorrente segura.
/// </summary>
public interface IServicoCredenciais
{
    /// <summary>
    /// Verifica email e senha. Falha incrementa a contagem (autoria anônima); a quinta falha
    /// bloqueia por 15 minutos e revoga todas as sessões trocando o stamp na mesma transação.
    /// Sucesso fora do bloqueio zera a contagem já com autoria do usuário verificado.
    /// </summary>
    /// <param name="email">Email informado.</param>
    /// <param name="senha">Senha informada (nunca registrada).</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Resultado tipado; inválido é sempre genérico.</returns>
    Task<ResultadoCredenciais> VerificarAsync(string email, string senha, CancellationToken tokenCancelamento);
}
