namespace Platform.Application.Access.Operacoes;

using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Auditoria;
using Platform.Domain.Access;

/// <summary>
/// Caso de uso para redefinição de senha com remoção de lockout e revogação de sessões (FR-033: <c>conta redefinir-senha</c>).
/// </summary>
public sealed class CasoUsoRedefinirSenha(
    IVerificadorAutorizacaoOperador verificadorAutorizacao,
    IManutencaoContas manutencaoContas,
    IRegistroOperacoesAcesso registroOperacoes,
    IProvedorSenha provedorSenha,
    IContextoOperacao contextoOperacao,
    IClassificadorFalhaPersistencia classificadorFalha)
{
    /// <summary>
    /// Executa a redefinição de senha.
    /// </summary>
    public async Task<ResultadoOperacao> ExecutarAsync(
        Guid idTenant,
        Guid idAmbiente,
        Guid idUsuario,
        CancellationToken tokenCancelamento = default)
    {
        Guid idOperacao = contextoOperacao.IdOperacao;
        DateTimeOffset agora = contextoOperacao.InstanteOperacao;

        var autorizacao = await verificadorAutorizacao.VerificarAutorizacaoAsync(idTenant, idAmbiente, tokenCancelamento);
        if (!autorizacao.Autorizado)
        {
            return ResultadoOperacao.CriarNegado(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.RedefinirSenha, autorizacao.CodigoNegacao ?? "AUTORIZACAO_NEGADA",
                agora, autorizacao.MensagemDiagnostico ?? "Operador não autorizado.");
        }

        string novaSenha = await provedorSenha.ObterSenhaComConfirmacaoAsync(tokenCancelamento);

        await using var transacao = await registroOperacoes.IniciarTransacaoAsync(tokenCancelamento);
        try
        {
            var resultadoMutacao = await manutencaoContas.RedefinirSenhaAsync(idUsuario, novaSenha, tokenCancelamento);
            if (!resultadoMutacao.Sucesso)
            {
                await transacao.DesfazerAsync(tokenCancelamento);
                await RegistrarFalhaAsync(idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador, resultadoMutacao.Codigo, agora, tokenCancelamento);
                return ResultadoOperacao.CriarFalha(
                    idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                    TipoOperacaoAcesso.RedefinirSenha, resultadoMutacao.Codigo, agora,
                    resultadoMutacao.Mensagem ?? "Falha ao redefinir senha.");
            }

            DadosRegistroOperacao dadosSucesso = new(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.RedefinirSenha, null, agora, ResultadoOperacaoAcesso.Sucesso, "SUCESSO");

            await transacao.RegistrarSucessoEConfirmarAsync(dadosSucesso, tokenCancelamento);
            return ResultadoOperacao.CriarSucesso(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.RedefinirSenha, agora);
        }
        catch (Exception ex)
        {
            await transacao.DesfazerAsync(tokenCancelamento);
            FalhaOperacaoClassificada falha = FalhaOperacaoClassificada.De(ex, classificadorFalha);
            await RegistrarFalhaAsync(idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador, falha.Codigo, agora, tokenCancelamento);
            return ResultadoOperacao.CriarFalha(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.RedefinirSenha, falha.Codigo, agora, falha.MensagemDiagnostico);
        }
    }

    private async Task RegistrarFalhaAsync(
        Guid idOperacao, Guid idTenant, Guid idAmbiente, Guid idUsuario, string loginOperador,
        string codigo, DateTimeOffset registradaEm, CancellationToken tokenCancelamento)
    {
        try
        {
            DadosRegistroOperacao dadosFalha = new(
                idOperacao, idTenant, idAmbiente, idUsuario, loginOperador, TipoOperacaoAcesso.RedefinirSenha,
                null, registradaEm, ResultadoOperacaoAcesso.Falha, codigo);
            await registroOperacoes.RegistrarFalhaOuNegadoEmTransacaoSeparadaAsync(dadosFalha, tokenCancelamento);
        }
        catch
        {
            // Protegido por journal de processo caso o banco falhe
        }
    }
}
