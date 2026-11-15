namespace Platform.Application.Auditoria;

/// <summary>
/// Define a autoria da operação do host operacional com o login PostgreSQL individual do
/// operador, somente depois que a autorização foi verificada na própria conexão
/// (nunca a partir de argumento, variável de ambiente ou configuração).
/// </summary>
public interface IRegistroOperadorVerificado
{
    /// <summary>
    /// Define o login verificado como autor das próximas gravações da operação.
    /// </summary>
    /// <param name="loginOperador">Login individual confirmado por <c>current_user</c>.</param>
    void DefinirOperadorVerificado(string loginOperador);
}
