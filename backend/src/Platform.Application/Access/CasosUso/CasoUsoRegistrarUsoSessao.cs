namespace Platform.Application.Access.CasosUso;

using Platform.Application.Access.Portas;
using Platform.Application.Auditoria;

/// <summary>
/// Registro de uso da sessão (D33): executado só para requisição autenticada aceita, antes do
/// handler da operação protegida. Com o <c>InstanteOperacao</c>, grava <c>UltimoUsoEm</c> somente se
/// <see cref="ExpiracaoSessao.DeveRegistrarUso"/> (no máximo 1 escrita/minuto); conflito de
/// <c>Versao</c> é descartado (sucesso, sem repetição); falha de dependência propaga como
/// <see cref="ExcecaoDependenciaIndisponivel"/> (503). Nunca altera teto, janela, ticket ou revogação
/// e nunca emite cookie.
/// </summary>
/// <param name="contexto">Contexto da operação (instante único e autoria).</param>
/// <param name="registroUsuario">Autoria do usuário da sessão verificada.</param>
/// <param name="sessoes">Persistência das sessões.</param>
public sealed class CasoUsoRegistrarUsoSessao(
    IContextoOperacao contexto,
    IRegistroUsuarioVerificado registroUsuario,
    IRepositorioSessoes sessoes)
{
    /// <summary>
    /// Registra o uso da sessão corrente, quando devido.
    /// </summary>
    /// <param name="atual">Sessão autenticada da requisição.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns><c>true</c> quando <c>UltimoUsoEm</c> foi gravado; <c>false</c> quando não era devido ou houve conflito descartado.</returns>
    /// <exception cref="ExcecaoDependenciaIndisponivel">Falha de banco na gravação.</exception>
    public async Task<bool> ExecutarAsync(SessaoCorrente atual, CancellationToken tokenCancelamento)
    {
        ArgumentNullException.ThrowIfNull(atual);
        DateTimeOffset agora = contexto.InstanteOperacao;
        if (!ExpiracaoSessao.DeveRegistrarUso(agora, atual.UltimoUsoEm))
        {
            return false;
        }

        registroUsuario.DefinirUsuarioVerificado(atual.IdUsuario);
        return await sessoes.RegistrarUsoAsync(atual.IdSessao, atual.Versao, agora, tokenCancelamento);
    }
}
