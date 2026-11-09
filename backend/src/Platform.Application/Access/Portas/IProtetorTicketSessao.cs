namespace Platform.Application.Access.Portas;

/// <summary>
/// Monta e protege o ticket nativo persistido da sessão (implementado pela API com Data Protection),
/// sem expor tipos de autenticação à Application.
/// </summary>
public interface IProtetorTicketSessao
{
    /// <summary>
    /// Serializa e protege o ticket.
    /// </summary>
    /// <param name="dados">Referências e carimbo da sessão.</param>
    /// <returns>Bytes protegidos.</returns>
    byte[] Proteger(DadosTicketSessao dados);
}
