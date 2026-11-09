namespace App.Api.Access;

using App.Api.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Platform.Application.Access;
using Platform.Application.Configuration;

/// <summary>
/// Somente nas cinco rotas L2: exatamente um <c>X-Tenant-Id</c> UUID não vazio (ausente ou vazio →
/// <c>TENANT_REQUIRED</c>; formato, duplicação ou lista → <c>INPUT_INVALID</c>) e igual ao
/// tenant configurado (401 genérico no login, 403 nas demais). Não acessa banco; o header nunca
/// escolhe conexão.
/// </summary>
/// <param name="proximo">Próximo middleware.</param>
public sealed class MiddlewareCabecalhoTenant(RequestDelegate proximo)
{
    /// <summary>
    /// Nome do header de tenant.
    /// </summary>
    public const string NomeCabecalho = "X-Tenant-Id";

    private const string MensagemFormato = "Informe um único UUID não vazio.";

    /// <summary>
    /// Executa a verificação.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <param name="instalacao">Configuração da instalação.</param>
    /// <returns>Tarefa do pipeline.</returns>
    public Task InvokeAsync(HttpContext contexto, IOptions<OpcoesInstalacaoTenant> instalacao)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(instalacao);

        RotaAcessoL2? rota = contexto.ObterRotaAcessoL2();
        if (rota is null)
        {
            return proximo(contexto);
        }

        StringValues valores = contexto.Request.Headers[NomeCabecalho];
        // Vazio equivale a ausente (mesma regra de G10 para valores vazios).
        if (valores.All(string.IsNullOrEmpty))
        {
            return ConfiguracaoProblemDetails.EscreverAsync(contexto, ConfiguracaoProblemDetails.TenantObrigatorio());
        }

        if (valores.Count != 1
            || !Guid.TryParseExact(valores[0], "D", out Guid idTenant)
            || idTenant == Guid.Empty)
        {
            return ConfiguracaoProblemDetails.EscreverAsync(
                contexto,
                ConfiguracaoProblemDetails.EntradaInvalida(new Dictionary<string, string[]> { [NomeCabecalho] = [MensagemFormato] }));
        }

        if (idTenant != instalacao.Value.IdTenant)
        {
            return ConfiguracaoProblemDetails.EscreverAsync(
                contexto,
                ConfiguracaoProblemDetails.DeErro(rota.EhLogin ? ErrosAcesso.CredenciaisInvalidas : ErrosAcesso.AcessoTenantNegado));
        }

        return proximo(contexto);
    }
}
