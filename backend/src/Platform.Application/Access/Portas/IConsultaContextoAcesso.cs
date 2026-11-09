namespace Platform.Application.Access.Portas;

/// <summary>
/// Leitura da conta ativa e das concessões vigentes no banco a cada chamada, sem claims nem cache.
/// </summary>
public interface IConsultaContextoAcesso
{
    /// <summary>
    /// Lê a conta e suas concessões atuais.
    /// </summary>
    /// <param name="idUsuario">Conta da sessão corrente.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Dados da conta ou nulo quando inexistente/desativada.</returns>
    Task<DadosContaAcesso?> ObterContaAsync(Guid idUsuario, CancellationToken tokenCancelamento);
}
