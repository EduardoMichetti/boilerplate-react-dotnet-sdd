namespace Platform.Application.Access.CasosUso;

using Microsoft.Extensions.Options;
using Platform.Application.Access.Portas;
using Platform.Application.Configuration;
using Platform.Domain.Access;
using Platform.Domain.Resultados;
using Platform.Domain.Tenancy;

/// <summary>
/// <c>GET /api/v1/context</c>: estado da instalação (409) antes da autorização; exige
/// <c>foundation.context.read</c> vigente no banco nesta chamada (403).
/// </summary>
/// <param name="instalacao">Configuração da instalação.</param>
/// <param name="consulta">Leitura da conta e concessões no banco.</param>
public sealed class CasoUsoConsultarContexto(IOptions<OpcoesInstalacaoTenant> instalacao, IConsultaContextoAcesso consulta)
{
    /// <summary>
    /// Lê o contexto de acesso corrente.
    /// </summary>
    /// <param name="atual">Sessão autenticada.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Contexto, <c>TENANT_UNAVAILABLE</c>, <c>AUTH_REQUIRED</c> ou <c>TENANT_ACCESS_DENIED</c>.</returns>
    public async Task<Resultado<ContextoAcesso>> ExecutarAsync(SessaoCorrente atual, CancellationToken tokenCancelamento)
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

        if (!conta.Permissoes.Contains(CodigosPermissao.FundacaoContextoLer, StringComparer.Ordinal))
        {
            return ErrosAcesso.AcessoTenantNegado;
        }

        IdentidadeAtual usuario = new(conta.IdUsuario, config.IdTenant, conta.Email, conta.NomeExibicao, atual.ExpiraEm);
        return new ContextoAcesso(usuario, config.IdTenant, config.NomeExibicaoTenant!, conta.Permissoes);
    }
}
