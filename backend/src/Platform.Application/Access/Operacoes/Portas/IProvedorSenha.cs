namespace Platform.Application.Access.Operacoes.Portas;

/// <summary>
/// Provedor para leitura de senha e confirmação de forma segura (prompt sem eco no console,
/// sintético em testes).
/// </summary>
public interface IProvedorSenha
{
    /// <summary>
    /// Obtém a senha com confirmação.
    /// </summary>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>A senha confirmada.</returns>
    Task<string> ObterSenhaComConfirmacaoAsync(CancellationToken tokenCancelamento = default);
}
