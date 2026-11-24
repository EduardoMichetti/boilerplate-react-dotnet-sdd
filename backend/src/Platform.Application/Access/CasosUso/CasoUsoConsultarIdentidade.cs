namespace Platform.Application.Access.CasosUso;

using Microsoft.Extensions.Options;
using Platform.Application.Access.Portas;
using Platform.Application.Configuration;
using Platform.Domain.Resultados;
using Platform.Domain.Tenancy;

/// <summary>
/// <c>GET /api/v1/me</c>: somente a própria conta da sessão válida. Instalação inativa → 409.
/// </summary>
/// <param name="instalacao">Configuração da instalação.</param>
/// <param name="consulta">Leitura da conta no banco.</param>
public sealed class CasoUsoConsultarIdentidade(IOptions<OpcoesInstalacaoTenant> instalacao, IConsultaContextoAcesso consulta)
{
    /// <summary>
    /// Lê a identidade corrente.
    /// </summary>
    /// <param name="atual">Sessão autenticada.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Identidade, <c>TENANT_UNAVAILABLE</c> ou <c>AUTH_REQUIRED</c>.</returns>
    public async Task<Resultado<IdentidadeAtual>> ExecutarAsync(SessaoCorrente atual, CancellationToken tokenCancelamento)
    {
        ArgumentNullException.ThrowIfNull(atual);
        OpcoesInstalacaoTenant config = instalacao.Value;
        if (config.EstadoTenant != EstadoTenant.Ativo)
        {
            return ErrosAcesso.TenantIndisponivel;
        }

        DadosContaAcesso? conta = await consulta.ObterContaAsync(atual.IdUsuario, tokenCancelamento);
        if (conta is null)
        {
            return ErrosAcesso.AutenticacaoNecessaria;
        }

        return new IdentidadeAtual(conta.IdUsuario, config.IdTenant, conta.Email, conta.NomeExibicao, atual.TetoAbsolutoEm, atual.JanelaInatividadeMinutos);
    }
}
