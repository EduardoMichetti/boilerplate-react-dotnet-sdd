namespace Platform.Application.Access.Operacoes;

using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Auditoria;
using Platform.Domain.Access;

/// <summary>
/// Caso de uso para desativação de conta local e revogação atômica de sessões (FR-033: <c>conta desativar</c>).
/// </summary>
public sealed class CasoUsoDesativarConta(
    IVerificadorAutorizacaoOperador verificadorAutorizacao,
    IManutencaoContas manutencaoContas,
    IRegistroOperacoesAcesso registroOperacoes,
    IContextoOperacao contextoOperacao,
    IClassificadorFalhaPersistencia classificadorFalha)
{
    /// <summary>
    /// Executa a desativação da conta.
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
                TipoOperacaoAcesso.DesativarConta, autorizacao.CodigoNegacao ?? "AUTORIZACAO_NEGADA",
                agora, autorizacao.MensagemDiagnostico ?? "Operador não autorizado.");
        }

        await using var transacao = await registroOperacoes.IniciarTransacaoAsync(tokenCancelamento);
        try
        {
            var resultadoMutacao = await manutencaoContas.DesativarContaAsync(idUsuario, tokenCancelamento);
            if (!resultadoMutacao.Sucesso)
            {
                await transacao.DesfazerAsync(tokenCancelamento);
                await RegistrarFalhaAsync(idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador, resultadoMutacao.Codigo, agora, tokenCancelamento);
                return ResultadoOperacao.CriarFalha(
                    idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                    TipoOperacaoAcesso.DesativarConta, resultadoMutacao.Codigo, agora,
                    resultadoMutacao.Mensagem ?? "Falha ao desativar conta.");
            }

            DadosRegistroOperacao dadosSucesso = new(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.DesativarConta, null, agora, ResultadoOperacaoAcesso.Sucesso, "SUCESSO");

            await transacao.RegistrarSucessoEConfirmarAsync(dadosSucesso, tokenCancelamento);
            return ResultadoOperacao.CriarSucesso(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.DesativarConta, agora);
        }
        catch (Exception ex)
        {
            await transacao.DesfazerAsync(tokenCancelamento);
            FalhaOperacaoClassificada falha = FalhaOperacaoClassificada.De(ex, classificadorFalha);
            await RegistrarFalhaAsync(idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador, falha.Codigo, agora, tokenCancelamento);
            return ResultadoOperacao.CriarFalha(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.DesativarConta, falha.Codigo, agora, falha.MensagemDiagnostico);
        }
    }

    private async Task RegistrarFalhaAsync(
        Guid idOperacao, Guid idTenant, Guid idAmbiente, Guid idUsuario, string loginOperador,
        string codigo, DateTimeOffset registradaEm, CancellationToken tokenCancelamento)
    {
        try
        {
            DadosRegistroOperacao dadosFalha = new(
                idOperacao, idTenant, idAmbiente, idUsuario, loginOperador, TipoOperacaoAcesso.DesativarConta,
                null, registradaEm, ResultadoOperacaoAcesso.Falha, codigo);
            await registroOperacoes.RegistrarFalhaOuNegadoEmTransacaoSeparadaAsync(dadosFalha, tokenCancelamento);
        }
        catch
        {
            // Protegido por journal de processo caso o banco falhe
        }
    }
}
