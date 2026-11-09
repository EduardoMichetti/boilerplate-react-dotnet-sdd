namespace App.Api.Access;

using System.Globalization;
using System.Net;
using System.Threading.RateLimiting;
using App.Api.Diagnostics;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;

/// <summary>
/// Parâmetros do limiter de login. Em produção são fixos (5 por minuto, janela fixa, fila zero);
/// não vêm de configuração. Somente a fábrica de testes substitui a instância (G11).
/// </summary>
public sealed class OpcoesLimiteLogin
{
    /// <summary>Requisições permitidas por janela e partição.</summary>
    public int Limite { get; init; } = 5;

    /// <summary>Duração da janela fixa.</summary>
    public TimeSpan Janela { get; init; } = TimeSpan.FromMinutes(1);
}

/// <summary>
/// Limiter nativo do login particionado pelo IP efetivo e forwarded headers condicionais (G7, G11).
/// </summary>
public static class ConfiguracaoLimiteLogin
{
    /// <summary>Política aplicada somente a <c>POST /api/v1/sessions</c>.</summary>
    public const string PoliticaLogin = "LimiteLogin";

    /// <summary>Chave de configuração dos proxies confiáveis.</summary>
    public const string ChaveProxiesConfiaveis = "CabecalhosEncaminhados:ProxiesConfiaveis";

    /// <summary>
    /// Partição única e fixa para requisições sem <c>RemoteIpAddress</c> (nunca isenção).
    /// </summary>
    public const string ParticaoSemIp = "sem-ip";

    /// <summary>
    /// Registra o limiter de login: janela fixa, fila zero, 429 <c>RATE_LIMITED</c> com <c>Retry-After</c>.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarLimiteLogin(this IServiceCollection servicos)
    {
        servicos.AddSingleton<OpcoesLimiteLogin>();
        servicos.AddRateLimiter(opcoes =>
        {
            opcoes.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            opcoes.OnRejected = (contexto, _) =>
            {
                int segundos = contexto.Lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan espera)
                    ? Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds))
                    : (int)contexto.HttpContext.RequestServices.GetRequiredService<OpcoesLimiteLogin>().Janela.TotalSeconds;
                return new ValueTask(ConfiguracaoProblemDetails.EscreverAsync(
                    contexto.HttpContext, ConfiguracaoProblemDetails.LimiteExcedido(), segundos));
            };
            opcoes.AddPolicy(PoliticaLogin, contexto =>
            {
                OpcoesLimiteLogin limite = contexto.RequestServices.GetRequiredService<OpcoesLimiteLogin>();
                string particao = contexto.Connection.RemoteIpAddress?.ToString() ?? ParticaoSemIp;
                return RateLimitPartition.GetFixedWindowLimiter(particao, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = limite.Limite,
                    Window = limite.Janela,
                    QueueLimit = 0,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    AutoReplenishment = true,
                });
            });
        });

        return servicos;
    }

    /// <summary>
    /// Lê os proxies confiáveis; vazio ou ausente (padrão L2) significa não processar <c>X-Forwarded-*</c>.
    /// </summary>
    /// <param name="configuracao">Configuração.</param>
    /// <returns>Endereços confiáveis.</returns>
    /// <exception cref="InvalidOperationException">Endereço inválido.</exception>
    public static IReadOnlyList<IPAddress> LerProxiesConfiaveis(IConfiguration configuracao)
    {
        ArgumentNullException.ThrowIfNull(configuracao);
        IConfigurationSection secao = configuracao.GetSection(ChaveProxiesConfiaveis);
        IEnumerable<string> valores = secao.GetChildren().Any()
            ? secao.GetChildren().Select(c => c.Value ?? string.Empty)
            : (secao.Value ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        List<IPAddress> enderecos = [];
        foreach (string valor in valores.Where(v => v.Length > 0))
        {
            enderecos.Add(IPAddress.TryParse(valor, out IPAddress? endereco)
                ? endereco
                : throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"{ChaveProxiesConfiaveis} contém endereço inválido.")));
        }

        return enderecos;
    }

    /// <summary>
    /// Registra forwarded headers somente quando há proxies confiáveis configurados.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <param name="proxies">Proxies confiáveis.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarCabecalhosEncaminhados(this IServiceCollection servicos, IReadOnlyList<IPAddress> proxies)
    {
        ArgumentNullException.ThrowIfNull(proxies);
        if (proxies.Count == 0)
        {
            return servicos;
        }

        servicos.Configure<ForwardedHeadersOptions>(opcoes =>
        {
            opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            opcoes.KnownProxies.Clear();
            opcoes.KnownIPNetworks.Clear();
            foreach (IPAddress proxy in proxies)
            {
                opcoes.KnownProxies.Add(proxy);
            }
        });
        return servicos;
    }
}
