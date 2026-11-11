namespace Platform.Application.Access.Operacoes.Portas;

/// <summary>
/// Porta para limpeza em lote de sessões encerradas além da retenção configurada (<c>sessao limpar</c>).
/// A remoção deve ser rastreada por entidade anexada com a <c>Versao</c> esperada, sem exclusão em lote direta no banco.
/// </summary>
public interface IRepositorioLimpezaSessoes
{
    /// <summary>
    /// Remove sessões encerradas (<c>RevogadaEm &lt; instanteCorte</c> ou <c>RevogadaEm == null &amp;&amp; ExpiraEm &lt; instanteCorte</c>)
    /// rastreando cada uma com sua versão esperada.
    /// </summary>
    /// <param name="instanteCorte">Instante de corte UTC calculado a partir da retenção.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Quantidade de sessões removidas.</returns>
    Task<int> LimparSessoesEncerradasAsync(DateTimeOffset instanteCorte, CancellationToken tokenCancelamento = default);
}
