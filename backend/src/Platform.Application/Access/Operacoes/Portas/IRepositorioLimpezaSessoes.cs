namespace Platform.Application.Access.Operacoes.Portas;

/// <summary>
/// Porta para limpeza em lote de sessões encerradas além da retenção configurada (<c>sessao limpar</c>).
/// A remoção deve ser rastreada por entidade anexada com a <c>Versao</c> esperada, sem exclusão em lote direta no banco.
/// </summary>
public interface IRepositorioLimpezaSessoes
{
    /// <summary>
    /// Remove sessões encerradas além da retenção, rastreando cada uma com sua versão esperada. Elegível se
    /// <c>RevogadaEm &lt; instanteCorte</c>, ou se <c>RevogadaEm</c> nulo e a expiração efetiva
    /// <c>min(UltimoUsoEm + JanelaInatividadeMinutos, TetoAbsolutoEm) &lt; instanteCorte</c> (L2.1, D31), calculada
    /// no banco com <paramref name="instanteCorte"/> como parâmetro e pré-filtro <c>UltimoUsoEm &lt; instanteCorte</c>.
    /// Como o corte é anterior ao instante da operação, sessão ativa nunca é elegível.
    /// </summary>
    /// <param name="instanteCorte">Instante de corte UTC calculado a partir da retenção.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Quantidade de sessões removidas.</returns>
    Task<int> LimparSessoesEncerradasAsync(DateTimeOffset instanteCorte, CancellationToken tokenCancelamento = default);
}
