namespace App.Api.Access;

using App.Api.Diagnostics;
using Microsoft.Extensions.Options;
using Platform.Application.Auditoria;
using Platform.Application.Configuration;
using Platform.Application.Tenancy;

/// <summary>
/// Somente nas cinco rotas L2, depois do <see cref="MiddlewareCabecalhoTenant"/> e antes da
/// autenticação (G2 revisada): confere a cada requisição, sem cache, o marcador igual à
/// configuração e a cadeia de migrations completa. Divergência, versão incompatível ou banco
/// indisponível → 503 sem tocar contas/sessões. Não verifica <c>EstadoTenant</c>.
/// </summary>
/// <param name="proximo">Próximo middleware.</param>
/// <param name="registrador">Registrador.</param>
public sealed class MiddlewareVerificacaoInstalacao(RequestDelegate proximo, ILogger<MiddlewareVerificacaoInstalacao> registrador)
{
    /// <summary>
    /// Executa a verificação.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <param name="instalacao">Configuração da instalação.</param>
    /// <param name="verificador">Leitura do marcador.</param>
    /// <param name="estado">Estado da requisição.</param>
    /// <param name="operacao">Contexto da operação (correlação).</param>
    /// <returns>Tarefa do pipeline.</returns>
    public async Task InvokeAsync(
        HttpContext contexto,
        IOptions<OpcoesInstalacaoTenant> instalacao,
        IVerificadorIdentidadeBancoTenant verificador,
        EstadoRequisicaoAcesso estado,
        IContextoOperacao operacao)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(instalacao);
        ArgumentNullException.ThrowIfNull(verificador);
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(operacao);

        if (contexto.ObterRotaAcessoL2() is null)
        {
            await proximo(contexto);
            return;
        }

        string? motivo;
        try
        {
            OpcoesInstalacaoTenant config = instalacao.Value;
            config.Validar();
            IdentidadeBancoTenantSnapshot marcador = await verificador.LerAsync(contexto.RequestAborted);
            motivo = marcador switch
            {
                _ when marcador.IdTenant != config.IdTenant || marcador.IdAmbiente != config.IdAmbiente => "MarcadorDivergente",
                { PossuiVersaoMigracaoEsperada: false } => "VersaoIncompativel",
                _ => null,
            };

            if (motivo is null)
            {
                estado.Marcador = marcador;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            motivo = ex.GetType().Name;
        }

        if (motivo is not null)
        {
            registrador.LogError(
                new EventId(2001, "InstalacaoNaoVerificada"),
                "Instalação não verificada antes do acesso a contas/sessões. Motivo: {Motivo}; IdOperacao: {IdOperacao}",
                motivo,
                operacao.IdOperacao);
            await ConfiguracaoProblemDetails.EscreverAsync(contexto, StatusCodes.Status503ServiceUnavailable);
            return;
        }

        await proximo(contexto);
    }
}
