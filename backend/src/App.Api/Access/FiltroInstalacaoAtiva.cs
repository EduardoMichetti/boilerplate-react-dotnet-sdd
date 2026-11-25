namespace App.Api.Access;

using App.Api.Diagnostics;
using Microsoft.Extensions.Options;
using Platform.Application.Access;
using Platform.Application.Configuration;
using Platform.Domain.Tenancy;

/// <summary>
/// G31: instalação ativa verificada como filtro de endpoint, depois da autenticação e da autorização
/// de recurso próprio e antes da permissão e do registro de uso. Instalação inativa → 409
/// <c>TENANT_UNAVAILABLE</c> sem chegar ao handler (e sem renovar a sessão). Os casos de uso mantêm a
/// mesma verificação (defesa em profundidade, mesma resposta).
/// </summary>
/// <param name="instalacao">Configuração da instalação.</param>
public sealed class FiltroInstalacaoAtiva(IOptions<OpcoesInstalacaoTenant> instalacao) : IEndpointFilter
{
    /// <inheritdoc />
    public ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        return instalacao.Value.EstadoTenant == EstadoTenant.Ativo
            ? next(context)
            : ValueTask.FromResult<object?>(ConfiguracaoProblemDetails.Resultado(ErrosAcesso.TenantIndisponivel));
    }
}

/// <summary>
/// Extensões que aplicam a ordem G31 a endpoints ou grupos de rotas (reutilizáveis pelas rotas de
/// módulos na L3): instalação ativa → permissão → registro de uso → handler.
/// </summary>
public static class ExtensoesFiltrosSessao
{
    /// <summary>
    /// Exige instalação ativa (409 <c>TENANT_UNAVAILABLE</c>).
    /// </summary>
    /// <typeparam name="TConstrutor">Endpoint ou grupo.</typeparam>
    /// <param name="construtor">Construtor.</param>
    /// <returns>O mesmo construtor.</returns>
    public static TConstrutor ExigirInstalacaoAtiva<TConstrutor>(this TConstrutor construtor)
        where TConstrutor : IEndpointConventionBuilder =>
        construtor.AddEndpointFilter<TConstrutor, FiltroInstalacaoAtiva>();

    /// <summary>
    /// Exige a permissão vigente no banco (403 <c>TENANT_ACCESS_DENIED</c>).
    /// </summary>
    /// <typeparam name="TConstrutor">Endpoint ou grupo.</typeparam>
    /// <param name="construtor">Construtor.</param>
    /// <param name="codigoPermissao">Código da permissão exigida.</param>
    /// <returns>O mesmo construtor.</returns>
    public static TConstrutor ExigirPermissao<TConstrutor>(this TConstrutor construtor, string codigoPermissao)
        where TConstrutor : IEndpointConventionBuilder =>
        construtor.AddEndpointFilter(new FiltroExigirPermissao(codigoPermissao));

    /// <summary>
    /// Registra o uso da sessão imediatamente antes do handler (requisição autenticada aceita, D33).
    /// </summary>
    /// <typeparam name="TConstrutor">Endpoint ou grupo.</typeparam>
    /// <param name="construtor">Construtor.</param>
    /// <returns>O mesmo construtor.</returns>
    public static TConstrutor RegistrarUsoSessao<TConstrutor>(this TConstrutor construtor)
        where TConstrutor : IEndpointConventionBuilder =>
        construtor.AddEndpointFilter<TConstrutor, FiltroRegistroUsoSessao>();
}
