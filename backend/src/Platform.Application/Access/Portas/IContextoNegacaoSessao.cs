namespace Platform.Application.Access.Portas;

/// <summary>
/// Motivo interno da negação da sessão na requisição corrente (scoped, D37). Preenchido pela
/// autenticação (Retrieve) e lido no ponto único que registra <c>EventId(1002, "SessaoNegada")</c>;
/// nunca chega à resposta HTTP.
/// </summary>
public interface IContextoNegacaoSessao
{
    /// <summary>
    /// Motivo registrado nesta requisição; nulo quando nenhuma sessão foi negada pela autenticação.
    /// </summary>
    MotivoNegacaoSessao? Motivo { get; }

    /// <summary>
    /// Registra o motivo da negação (o último registro prevalece).
    /// </summary>
    /// <param name="motivo">Motivo.</param>
    void Registrar(MotivoNegacaoSessao motivo);
}
