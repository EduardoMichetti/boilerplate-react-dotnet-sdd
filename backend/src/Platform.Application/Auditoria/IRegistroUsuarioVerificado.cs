namespace Platform.Application.Auditoria;

/// <summary>
/// Promove a autoria da operação corrente de anônima para o usuário, somente depois que a
/// credencial foi verificada no servidor (nunca a partir do email enviado).
/// </summary>
public interface IRegistroUsuarioVerificado
{
    /// <summary>
    /// Define o usuário verificado como autor das próximas gravações da operação.
    /// </summary>
    /// <param name="idUsuario">Conta com credencial ou sessão verificada.</param>
    void DefinirUsuarioVerificado(Guid idUsuario);
}
