namespace Platform.Application.Access.CasosUso;

using Platform.Application.Access.Portas;

/// <summary>
/// Regra única de validade de uma sessão a cada operação: registro ativo e não revogado,
/// <c>agora &lt; TetoAbsolutoEm</c> gravado (nunca recalculado pela configuração atual), conta ativa e
/// fora do bloqueio, carimbo de segurança e época coincidentes com a referência e o marcador.
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
    /// <param name="agora">Instante da operação corrente.</param>
    /// <returns><c>true</c> somente quando todas as condições são atendidas.</returns>
    public static bool EhValida(
        EstadoSessaoPersistida estado,
        Guid epocaReferencia,
        Guid epocaMarcador,
        string? carimboTicket,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(estado);

        bool sessaoAtiva = estado.RevogadaEm is null && estado.TicketProtegido is not null && agora < estado.TetoAbsolutoEm;
        bool contaLiberada = estado.ContaAtiva && !(estado.BloqueioHabilitado && estado.BloqueioAte is { } fim && fim > agora);
        bool carimboConfere = carimboTicket is not null
            && estado.CarimboSeguranca is not null
            && string.Equals(carimboTicket, estado.CarimboSeguranca, StringComparison.Ordinal);
        bool epocaConfere = estado.EpocaAcesso == epocaReferencia && epocaReferencia == epocaMarcador;

        return sessaoAtiva && contaLiberada && carimboConfere && epocaConfere;
    }
}
