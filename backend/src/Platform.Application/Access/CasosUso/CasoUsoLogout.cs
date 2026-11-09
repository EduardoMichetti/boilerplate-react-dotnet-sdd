namespace Platform.Application.Access.CasosUso;

using Platform.Application.Access.Portas;
using Platform.Application.Auditoria;
using Platform.Domain.Resultados;

/// <summary>
/// Logout (<c>DELETE /api/v1/sessions/current</c>): revoga somente a própria sessão, com a versão
/// lida na autenticação. Não exige instalação ativa nem permissão.
/// </summary>
/// <param name="sessoes">Persistência das sessões.</param>
/// <param name="registroUsuario">Autoria do usuário da sessão verificada.</param>
public sealed class CasoUsoLogout(IRepositorioSessoes sessoes, IRegistroUsuarioVerificado registroUsuario)
{
    /// <summary>
    /// Revoga a sessão corrente; commit antes do retorno.
    /// </summary>
    /// <param name="atual">Sessão autenticada da requisição.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Sucesso ou <c>AUTH_REQUIRED</c> quando a sessão deixou de estar ativa.</returns>
    public Task<Resultado> ExecutarAsync(SessaoCorrente atual, CancellationToken tokenCancelamento)
    {
        ArgumentNullException.ThrowIfNull(atual);
        registroUsuario.DefinirUsuarioVerificado(atual.IdUsuario);
        return sessoes.RevogarAsync(atual.IdSessao, atual.Versao, tokenCancelamento);
    }
}
