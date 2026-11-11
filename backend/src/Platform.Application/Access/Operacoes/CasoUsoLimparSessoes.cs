namespace Platform.Application.Access.Operacoes;

using Microsoft.Extensions.Options;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Auditoria;
using Platform.Application.Configuration;
using Platform.Domain.Access;

/// <summary>
/// Caso de uso para limpeza em lote de sessões encerradas além da retenção configurada (FR-033: <c>sessao limpar</c>).
/// </summary>
public sealed class CasoUsoLimparSessoes(
    IVerificadorAutorizacaoOperador verificadorAutorizacao,
    IRepositorioLimpezaSessoes repositorioLimpeza,
    IRegistroOperacoesAcesso registroOperacoes,
    IOptions<OpcoesOperacaoAcesso> opcoesOperacao,
    IContextoOperacao contextoOperacao)
{
    /// <summary>
    /// Executa a limpeza de sessões expiradas/revogadas.
    /// </summary>
    public async Task<ResultadoOperacao> ExecutarAsync(
        Guid idTenant,
        Guid idAmbiente,
        CancellationToken tokenCancelamento = default)
    {
        Guid idOperacao = contextoOperacao.IdOperacao;
        DateTimeOffset agora = contextoOperacao.InstanteOperacao;

        OpcoesOperacaoAcesso config = opcoesOperacao.Value;
        if (!config.RetencaoValida)
        {
            return ResultadoOperacao.CriarFalha(
                idOperacao, idTenant, idAmbiente, null, string.Empty,
                TipoOperacaoAcesso.LimparSessoes, "RETENCAO_INVALIDA", agora,
                "A retenção configurada é inválida (deve ser um número inteiro entre 1 e 365 dias).");
        }

        int retencaoDias = config.RetencaoSessoesDias;
        DateTimeOffset instanteCorte = agora.AddDays(-retencaoDias);

        var autorizacao = await verificadorAutorizacao.VerificarAutorizacaoAsync(idTenant, idAmbiente, tokenCancelamento);
        if (!autorizacao.Autorizado)
        {
            return ResultadoOperacao.CriarNegado(
                idOperacao, idTenant, idAmbiente, null, autorizacao.LoginOperador,
                TipoOperacaoAcesso.LimparSessoes, autorizacao.CodigoNegacao ?? "AUTORIZACAO_NEGADA",
                agora, autorizacao.MensagemDiagnostico ?? "Operador não autorizado.");
        }

        await using var transacao = await registroOperacoes.IniciarTransacaoAsync(tokenCancelamento);
        try
        {
            int quantidadeRemovida = await repositorioLimpeza.LimparSessoesEncerradasAsync(instanteCorte, tokenCancelamento);

            DadosRegistroOperacao dadosSucesso = new(
                idOperacao, idTenant, idAmbiente, null, autorizacao.LoginOperador,
                TipoOperacaoAcesso.LimparSessoes, null, agora, ResultadoOperacaoAcesso.Sucesso, "SUCESSO",
                retencaoDias, instanteCorte, quantidadeRemovida);

            await transacao.RegistrarSucessoEConfirmarAsync(dadosSucesso, tokenCancelamento);
            return ResultadoOperacao.CriarSucesso(
                idOperacao, idTenant, idAmbiente, null, autorizacao.LoginOperador,
                TipoOperacaoAcesso.LimparSessoes, agora, retencaoDias, instanteCorte, quantidadeRemovida);
        }
        catch (Exception ex)
        {
            await transacao.DesfazerAsync(tokenCancelamento);
            await RegistrarFalhaAsync(idOperacao, idTenant, idAmbiente, autorizacao.LoginOperador, "FALHA_LIMPEZA", agora, retencaoDias, instanteCorte, tokenCancelamento);
            return ResultadoOperacao.CriarFalha(
                idOperacao, idTenant, idAmbiente, null, autorizacao.LoginOperador,
                TipoOperacaoAcesso.LimparSessoes, "FALHA_LIMPEZA", agora, ex.Message,
                retencaoDias, instanteCorte, 0);
        }
    }

    private async Task RegistrarFalhaAsync(
        Guid idOperacao, Guid idTenant, Guid idAmbiente, string loginOperador,
        string codigo, DateTimeOffset registradaEm, int retencaoDias, DateTimeOffset instanteCorte, CancellationToken tokenCancelamento)
    {
        try
        {
            DadosRegistroOperacao dadosFalha = new(
                idOperacao, idTenant, idAmbiente, null, loginOperador, TipoOperacaoAcesso.LimparSessoes,
                null, registradaEm, ResultadoOperacaoAcesso.Falha, codigo,
                retencaoDias, instanteCorte, 0);
            await registroOperacoes.RegistrarFalhaOuNegadoEmTransacaoSeparadaAsync(dadosFalha, tokenCancelamento);
        }
        catch
        {
            // Protegido por journal de processo caso o banco falhe
        }
    }
}
