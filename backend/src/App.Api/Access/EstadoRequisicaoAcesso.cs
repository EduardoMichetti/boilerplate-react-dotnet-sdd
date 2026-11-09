namespace App.Api.Access;

using Platform.Application.Access.Portas;
using Platform.Application.Tenancy;

/// <summary>
/// Metadado das cinco rotas L2 de <c>/api/v1</c>: somente elas passam por tenant, marcador e
/// autenticação de sessão. Health, OpenAPI e rotas não mapeadas ficam fora desse fluxo.
/// </summary>
/// <param name="EhLogin">Indica <c>POST /api/v1/sessions</c> (tenant divergente → 401 genérico).</param>
public sealed record RotaAcessoL2(bool EhLogin = false);

/// <summary>
/// Estado da requisição corrente produzido pelos middlewares e pelo store de tickets (scoped).
/// </summary>
public sealed class EstadoRequisicaoAcesso
{
    /// <summary>
    /// Marcador conferido nesta requisição pelo <see cref="MiddlewareVerificacaoInstalacao"/>.
    /// </summary>
    public IdentidadeBancoTenantSnapshot? Marcador { get; set; }

    /// <summary>
    /// Sessão válida do cookie recebido, com a versão lida na autenticação.
    /// </summary>
    public SessaoCorrente? Sessao { get; set; }
}

/// <summary>
/// Extensões de leitura do metadado L2.
/// </summary>
public static class ExtensoesRotaAcessoL2
{
    /// <summary>
    /// Obtém o metadado L2 do endpoint resolvido, quando houver.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <returns>Metadado ou nulo.</returns>
    public static RotaAcessoL2? ObterRotaAcessoL2(this HttpContext contexto) =>
        contexto.GetEndpoint()?.Metadata.GetMetadata<RotaAcessoL2>();
}
