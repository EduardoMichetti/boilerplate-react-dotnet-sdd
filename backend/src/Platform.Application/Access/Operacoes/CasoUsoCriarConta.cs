namespace Platform.Application.Access.Operacoes;

using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Auditoria;
using Platform.Domain.Access;
using Platform.Domain.Identificadores;

/// <summary>
/// Caso de uso para criação restrita de conta local (FR-033: <c>conta criar</c>).
/// </summary>
public sealed class CasoUsoCriarConta(
    IVerificadorAutorizacaoOperador verificadorAutorizacao,
    IManutencaoContas manutencaoContas,
    IRegistroOperacoesAcesso registroOperacoes,
    IProvedorSenha provedorSenha,
    IContextoOperacao contextoOperacao)
{
    /// <summary>
    /// Executa a criação da conta.
    /// </summary>
    public async Task<ResultadoOperacao> ExecutarAsync(
        Guid idTenant,
        Guid idAmbiente,
        string email,
        string nomeExibicao,
        CancellationToken tokenCancelamento = default)
    {
        Guid idOperacao = contextoOperacao.IdOperacao;
        DateTimeOffset agora = contextoOperacao.InstanteOperacao;

        var autorizacao = await verificadorAutorizacao.VerificarAutorizacaoAsync(idTenant, idAmbiente, tokenCancelamento);
        if (!autorizacao.Autorizado)
        {
            return ResultadoOperacao.CriarNegado(
                idOperacao, idTenant, idAmbiente, null, autorizacao.LoginOperador,
                TipoOperacaoAcesso.CriarConta, autorizacao.CodigoNegacao ?? "AUTORIZACAO_NEGADA",
                agora, autorizacao.MensagemDiagnostico ?? "Operador não autorizado.");
        }

        string senha = await provedorSenha.ObterSenhaComConfirmacaoAsync(tokenCancelamento);
        Guid idUsuario = IdentificadorV7.Gerar(agora);

        await using var transacao = await registroOperacoes.IniciarTransacaoAsync(tokenCancelamento);
        try
        {
            var resultadoMutacao = await manutencaoContas.CriarContaAsync(idUsuario, email, nomeExibicao, senha, tokenCancelamento);
            if (!resultadoMutacao.Sucesso)
            {
                await transacao.DesfazerAsync(tokenCancelamento);
                await RegistrarFalhaAsync(idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador, resultadoMutacao.Codigo, agora, tokenCancelamento);
                return ResultadoOperacao.CriarFalha(
                    idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                    TipoOperacaoAcesso.CriarConta, resultadoMutacao.Codigo, agora,
                    resultadoMutacao.Mensagem ?? "Falha ao criar conta.");
            }

            DadosRegistroOperacao dadosSucesso = new(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.CriarConta, null, agora, ResultadoOperacaoAcesso.Sucesso, "SUCESSO");

            await transacao.RegistrarSucessoEConfirmarAsync(dadosSucesso, tokenCancelamento);
            return ResultadoOperacao.CriarSucesso(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.CriarConta, agora);
        }
        catch (Exception ex)
        {
            await transacao.DesfazerAsync(tokenCancelamento);
            await RegistrarFalhaAsync(idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador, "CONFLITO_CONCORRENTE", agora, tokenCancelamento);
            return ResultadoOperacao.CriarFalha(
                idOperacao, idTenant, idAmbiente, idUsuario, autorizacao.LoginOperador,
                TipoOperacaoAcesso.CriarConta, "CONFLITO_CONCORRENTE", agora, ex.Message);
        }
    }

    private async Task RegistrarFalhaAsync(
        Guid idOperacao, Guid idTenant, Guid idAmbiente, Guid? idUsuario, string loginOperador,
        string codigo, DateTimeOffset registradaEm, CancellationToken tokenCancelamento)
    {
        try
        {
            DadosRegistroOperacao dadosFalha = new(
                idOperacao, idTenant, idAmbiente, idUsuario, loginOperador, TipoOperacaoAcesso.CriarConta,
                null, registradaEm, ResultadoOperacaoAcesso.Falha, codigo);
            await registroOperacoes.RegistrarFalhaOuNegadoEmTransacaoSeparadaAsync(dadosFalha, tokenCancelamento);
        }
        catch
        {
            // Protegido por journal de processo caso o banco falhe
        }
    }
}
