namespace App.Api.Access;

using System.Security.Claims;
using App.Api.Diagnostics;
using Platform.Application.Access;
using Platform.Application.Access.Portas;

/// <summary>
/// G31: exige a concessão <paramref name="codigoPermissao"/> vigente no banco nesta chamada (lida por
/// <see cref="IConsultaContextoAcesso"/>, nunca de claims copiados no login), depois da instalação
/// ativa e antes do registro de uso. Ausente → 403 <c>TENANT_ACCESS_DENIED</c> sem chegar ao handler.
/// Depende só de portas de <c>Platform.Application</c> e da claim de conta do principal autenticado.
/// </summary>
/// <param name="codigoPermissao">Código da permissão exigida.</param>
public sealed class FiltroExigirPermissao(string codigoPermissao) : IEndpointFilter
{
    /// <summary>
    /// Código da permissão exigida.
    /// </summary>
    public string CodigoPermissao { get; } = !string.IsNullOrWhiteSpace(codigoPermissao)
        ? codigoPermissao
        : throw new ArgumentException("Código de permissão obrigatório.", nameof(codigoPermissao));

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        HttpContext http = context.HttpContext;
        if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid idUsuario))
        {
            return RegistroNegacaoSessao.Resultado(http);
        }

        IConsultaContextoAcesso consulta = http.RequestServices.GetRequiredService<IConsultaContextoAcesso>();
        DadosContaAcesso? conta = await consulta.ObterContaAsync(idUsuario, http.RequestAborted);
        if (conta is null)
        {
            return RegistroNegacaoSessao.Resultado(http);
        }

        if (!conta.Permissoes.Contains(CodigoPermissao, StringComparer.Ordinal))
        {
            return ConfiguracaoProblemDetails.Resultado(ErrosAcesso.AcessoTenantNegado);
        }

        return await next(context);
    }
}
