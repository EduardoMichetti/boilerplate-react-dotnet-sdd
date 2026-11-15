namespace Platform.Application.Access.Operacoes;

using Platform.Application.Access.Operacoes.Portas;

/// <summary>
/// Código estável e diagnóstico de uma exceção capturada por um caso de uso do host operacional.
/// O código vai para o resultado e para o registro de auditoria; o diagnóstico vai somente para stderr.
/// </summary>
/// <param name="Codigo"><c>CONFLITO_CONCORRENTE</c> para conflito real; <c>FALHA_INTERNA</c> para o restante.</param>
/// <param name="MensagemDiagnostico">Diagnóstico técnico destinado apenas a stderr/journal.</param>
public sealed record FalhaOperacaoClassificada(string Codigo, string MensagemDiagnostico)
{
    /// <summary>
    /// Classifica a exceção: somente conflito concorrente real recebe <c>CONFLITO_CONCORRENTE</c>.
    /// </summary>
    /// <param name="excecao">Exceção capturada.</param>
    /// <param name="classificador">Classificador de falhas de persistência.</param>
    /// <returns>Falha classificada.</returns>
    public static FalhaOperacaoClassificada De(Exception excecao, IClassificadorFalhaPersistencia classificador)
    {
        ArgumentNullException.ThrowIfNull(excecao);
        ArgumentNullException.ThrowIfNull(classificador);

        string tipo = excecao.GetType().Name;
        return classificador.EhConflitoConcorrente(excecao)
            ? new FalhaOperacaoClassificada(
                ResultadoAcaoConta.CodigoConflitoConcorrente,
                $"Conflito concorrente ao persistir a operação ({tipo}); nada foi aplicado e não há repetição automática.")
            : new FalhaOperacaoClassificada(
                ResultadoAcaoConta.CodigoFalhaInterna,
                $"Falha interna inesperada ({tipo}): {excecao.Message}");
    }
}
