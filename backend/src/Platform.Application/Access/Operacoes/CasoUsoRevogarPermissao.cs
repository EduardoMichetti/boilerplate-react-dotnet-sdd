namespace Platform.Application.Access.Operacoes;

using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Auditoria;
using Platform.Domain.Access;

/// <summary>
/// Caso de uso para revogação restrita de permissão da conta (FR-033: <c>permissao revogar</c>).
/// </summary>
public sealed class CasoUsoRevogarPermissao(
    IVerificadorAutorizacaoOperador verificadorAutorizacao,
    IManutencaoContas manutencaoContas,
    IRegistroOperacoesAcesso registroOperacoes,
    IContextoOperacao contextoOperacao,
    IClassificadorFalhaPersistencia classificadorFalha)
{
    /// <summary>
    /// Executa a revogação de permissão.
    /// </summary>
    public async Task<ResultadoOperacao> ExecutarAsync(
        Guid idTenant,
        Guid idAmbiente,
        Guid idUsuario,
        string codigoPermissao,
        CancellationToken tokenCancelamento = default)
    {
        Guid idOperacao = contextoOperacao.IdOperacao;
        DateTimeOffset agora = contextoOperacao.InstanteOperacao;

        var autorizacao = await verificadorAutorizacao.VerificarAutorizacaoAsync(idTenant, idAmbiente, tokenCancelamento);
        if (!autorizacao.Autorizado)
        {
            return ResultadoOperacao.CriarNegado(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.RevogarPermissao, autorizacao.CodigoNegacao ?? "AUTORIZACAO_NEGADA",
                agora, autorizacao.MensagemDiagnostico ?? "Operador não autorizado.");
        }

        await using var transacao = await registroOperacoes.IniciarTransacaoAsync(tokenCancelamento);
        try
        {
            var resultadoMutacao = await manutencaoContas.RevogarPermissaoAsync(idUsuario, codigoPermissao, tokenCancelamento);
            if (!resultadoMutacao.Sucesso)
            {
                await transacao.DesfazerAsync(tokenCancelamento);
                await RegistrarFalhaAsync(idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador, codigoPermissao, resultadoMutacao.Codigo, agora, tokenCancelamento);
                return ResultadoOperacao.CriarFalha(
                    idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                    TipoOperacaoAcesso.RevogarPermissao, resultadoMutacao.Codigo, agora,
                    resultadoMutacao.Mensagem ?? "Falha ao revogar permissão.");
            }

            DadosRegistroOperacao dadosSucesso = new(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.RevogarPermissao, codigoPermissao, agora, ResultadoOperacaoAcesso.Sucesso, "SUCESSO");

            await transacao.RegistrarSucessoEConfirmarAsync(dadosSucesso, tokenCancelamento);
            return ResultadoOperacao.CriarSucesso(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.RevogarPermissao, agora);
        }
        catch (Exception ex)
        {
            await transacao.DesfazerAsync(tokenCancelamento);
            FalhaOperacaoClassificada falha = FalhaOperacaoClassificada.De(ex, classificadorFalha);
            await RegistrarFalhaAsync(idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador, codigoPermissao, falha.Codigo, agora, tokenCancelamento);
            return ResultadoOperacao.CriarFalha(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.RevogarPermissao, falha.Codigo, agora, falha.MensagemDiagnostico);
        }
    }

    private async Task RegistrarFalhaAsync(
        Guid idOperacao, Guid idTenant, Guid idAmbiente, Guid idUsuario, string loginOperador,
        string codigoPermissao, string codigo, DateTimeOffset registradaEm, CancellationToken tokenCancelamento)
    {
        try
        {
            DadosRegistroOperacao dadosFalha = new(
                idOperacao, idTenant, idAmbiente, idUsuario, loginOperador, TipoOperacaoAcesso.RevogarPermissao,
                codigoPermissao, registradaEm, ResultadoOperacaoAcesso.Falha, codigo);
            await registroOperacoes.RegistrarFalhaOuNegadoEmTransacaoSeparadaAsync(dadosFalha, tokenCancelamento);
        }
        catch
        {
            // Protegido por journal de processo caso o banco falhe
        }
    }
}
