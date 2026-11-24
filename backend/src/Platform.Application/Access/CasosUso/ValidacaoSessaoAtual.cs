namespace Platform.Application.Access.CasosUso;

using Platform.Application.Access.Portas;

/// <summary>
/// Regra única de validade de uma sessão a cada operação (D31, D37): registro não revogado e com
/// ticket, <c>agora &lt; min(UltimoUsoEm + JanelaInatividadeMinutos, TetoAbsolutoEm)</c> com os valores
/// gravados na emissão (nunca recalculados pela configuração atual), conta ativa e fora do bloqueio,
/// carimbo de segurança e época coincidentes com a referência e o marcador. Não grava nada.
/// </summary>
public static class ValidacaoSessaoAtual
{
    /// <summary>
    /// Avalia a sessão persistida.
    /// </summary>
    /// <param name="estado">Sessão e conta lidas agora no banco.</param>
    /// <param name="epocaReferencia">Época contida na referência do cookie.</param>
    /// <param name="epocaMarcador">Época vigente no marcador da instalação.</param>
    /// <param name="carimboTicket">Carimbo de segurança gravado no ticket na emissão.</param>
    /// <param name="agora">Instante da operação corrente (do <see cref="TimeProvider"/>).</param>
    /// <returns>
    /// <c>null</c> quando válida; senão o motivo: <see cref="MotivoNegacaoSessao.Revogada"/>,
    /// <see cref="MotivoNegacaoSessao.TetoAbsoluto"/>, <see cref="MotivoNegacaoSessao.Inatividade"/> ou
    /// <see cref="MotivoNegacaoSessao.Invalida"/> (conta, bloqueio, carimbo ou época).
    /// </returns>
    public static MotivoNegacaoSessao? Avaliar(
        EstadoSessaoPersistida estado,
        Guid epocaReferencia,
        Guid epocaMarcador,
        string? carimboTicket,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(estado);

        if (estado.RevogadaEm is not null || estado.TicketProtegido is null)
        {
            return MotivoNegacaoSessao.Revogada;
        }

        if (ExpiracaoSessao.Avaliar(agora, estado.UltimoUsoEm, estado.JanelaInatividadeMinutos, estado.TetoAbsolutoEm) is { } expirada)
        {
            return expirada;
        }

        bool contaLiberada = estado.ContaAtiva && !(estado.BloqueioHabilitado && estado.BloqueioAte is { } fim && fim > agora);
        bool carimboConfere = carimboTicket is not null
            && estado.CarimboSeguranca is not null
            && string.Equals(carimboTicket, estado.CarimboSeguranca, StringComparison.Ordinal);
        bool epocaConfere = estado.EpocaAcesso == epocaReferencia && epocaReferencia == epocaMarcador;

        return contaLiberada && carimboConfere && epocaConfere ? null : MotivoNegacaoSessao.Invalida;
    }
}
