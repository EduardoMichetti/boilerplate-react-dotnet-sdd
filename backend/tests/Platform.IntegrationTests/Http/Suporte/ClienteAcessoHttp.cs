namespace Platform.IntegrationTests.Http.Suporte;

using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

/// <summary>
/// Cliente HTTP com cookie jar próprio (cookies reenviáveis deliberadamente a outra instalação),
/// token CSRF corrente e header <c>X-Tenant-Id</c>.
/// </summary>
public sealed class ClienteAcessoHttp : IDisposable
{
    /// <summary>Rota do token antiforgery.</summary>
    public const string RotaAntiforgery = "/api/v1/antiforgery-token";

    /// <summary>Rota de login.</summary>
    public const string RotaSessoes = "/api/v1/sessions";

    /// <summary>Rota de logout.</summary>
    public const string RotaSessaoAtual = "/api/v1/sessions/current";

    /// <summary>Rota da identidade.</summary>
    public const string RotaMe = "/api/v1/me";

    /// <summary>Rota do contexto.</summary>
    public const string RotaContexto = "/api/v1/context";

    private readonly HttpClient _http;

    internal ClienteAcessoHttp(HttpClient http, Guid idTenant)
    {
        _http = http;
        IdTenant = idTenant.ToString();
    }

    /// <summary>Valor enviado em <c>X-Tenant-Id</c> (nulo = não enviar).</summary>
    public string? IdTenant { get; set; }

    /// <summary>Token CSRF corrente (nulo = não enviar).</summary>
    public string? TokenCsrf { get; set; }

    /// <summary>Cookies correntes do jar (nome → valor).</summary>
    public Dictionary<string, string> Cookies { get; } = new(StringComparer.Ordinal);

    /// <summary>Headers <c>Set-Cookie</c> da última resposta.</summary>
    public IReadOnlyList<string> UltimoSetCookie { get; private set; } = [];

    /// <summary>
    /// Envia uma requisição com tenant, CSRF e cookies correntes.
    /// </summary>
    /// <param name="metodo">Método.</param>
    /// <param name="caminho">Caminho.</param>
    /// <param name="corpoJson">Corpo JSON bruto, quando houver.</param>
    /// <param name="ajustar">Ajuste final da requisição.</param>
    /// <returns>Resposta.</returns>
    public async Task<HttpResponseMessage> EnviarAsync(HttpMethod metodo, string caminho, string? corpoJson = null, Action<HttpRequestMessage>? ajustar = null)
    {
        using HttpRequestMessage requisicao = new(metodo, caminho);
        if (IdTenant is not null)
        {
            requisicao.Headers.TryAddWithoutValidation("X-Tenant-Id", IdTenant);
        }

        if (TokenCsrf is not null)
        {
            requisicao.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", TokenCsrf);
        }

        if (Cookies.Count > 0)
        {
            requisicao.Headers.TryAddWithoutValidation("Cookie", string.Join("; ", Cookies.Select(c => $"{c.Key}={c.Value}")));
        }

        if (corpoJson is not null)
        {
            requisicao.Content = new StringContent(corpoJson, Encoding.UTF8, "application/json");
        }

        ajustar?.Invoke(requisicao);
        HttpResponseMessage resposta = await _http.SendAsync(requisicao, TestContext.Current.CancellationToken);
        AtualizarCookies(resposta);
        return resposta;
    }

    /// <summary>
    /// Obtém novo token antiforgery e guarda em <see cref="TokenCsrf"/>.
    /// </summary>
    /// <returns>Código HTTP do GET.</returns>
    public async Task<HttpStatusCode> ObterTokenAsync()
    {
        string? anterior = TokenCsrf;
        TokenCsrf = null;
        using HttpResponseMessage resposta = await EnviarAsync(HttpMethod.Get, RotaAntiforgery);
        if (resposta.StatusCode == HttpStatusCode.OK)
        {
            using JsonDocument json = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            TokenCsrf = json.RootElement.GetProperty("requestToken").GetString();
        }
        else
        {
            TokenCsrf = anterior;
        }

        return resposta.StatusCode;
    }

    /// <summary>
    /// Login com novo token (opcional).
    /// </summary>
    /// <param name="email">Email.</param>
    /// <param name="senha">Senha.</param>
    /// <param name="novoToken">Obtém token antes.</param>
    /// <returns>Resposta.</returns>
    public async Task<HttpResponseMessage> LoginAsync(string email, string senha, bool novoToken = true)
    {
        if (novoToken)
        {
            await ObterTokenAsync();
        }

        return await EnviarAsync(HttpMethod.Post, RotaSessoes, JsonSerializer.Serialize(new { email, password = senha }));
    }

    /// <summary>
    /// Login que deve ter sucesso; já obtém o token da nova identidade.
    /// </summary>
    /// <param name="email">Email.</param>
    /// <param name="senha">Senha.</param>
    /// <returns>Corpo da resposta 201.</returns>
    public async Task<JsonElement> EntrarAsync(string email, string senha)
    {
        using HttpResponseMessage resposta = await LoginAsync(email, senha);
        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(resposta.StatusCode == HttpStatusCode.Created, $"Login esperado 201, obtido {(int)resposta.StatusCode}: {corpo}");
        await ObterTokenAsync();
        return JsonDocument.Parse(corpo).RootElement.Clone();
    }

    /// <summary>
    /// Logout com novo token (opcional).
    /// </summary>
    /// <param name="novoToken">Obtém token antes.</param>
    /// <returns>Resposta.</returns>
    public async Task<HttpResponseMessage> LogoutAsync(bool novoToken = true)
    {
        if (novoToken)
        {
            await ObterTokenAsync();
        }

        return await EnviarAsync(HttpMethod.Delete, RotaSessaoAtual);
    }

    /// <summary>GET /api/v1/me.</summary>
    /// <returns>Resposta.</returns>
    public Task<HttpResponseMessage> MeAsync() => EnviarAsync(HttpMethod.Get, RotaMe);

    /// <summary>GET /api/v1/context.</summary>
    /// <returns>Resposta.</returns>
    public Task<HttpResponseMessage> ContextoAsync() => EnviarAsync(HttpMethod.Get, RotaContexto);

    /// <summary>
    /// Copia o cookie de sessão deste jar para outro cliente (teste cruzado deliberado).
    /// </summary>
    /// <param name="destino">Cliente de destino.</param>
    /// <param name="nomeCookie">Nome do cookie de sessão.</param>
    public void CopiarCookieSessaoPara(ClienteAcessoHttp destino, string nomeCookie = "bps-session")
    {
        ArgumentNullException.ThrowIfNull(destino);
        destino.Cookies[nomeCookie] = Cookies[nomeCookie];
    }

    /// <inheritdoc />
    public void Dispose() => _http.Dispose();

    private void AtualizarCookies(HttpResponseMessage resposta)
    {
        UltimoSetCookie = resposta.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? valores) ? valores.ToList() : [];
        foreach (string setCookie in UltimoSetCookie)
        {
            string[] partes = setCookie.Split(';', StringSplitOptions.TrimEntries);
            int separador = partes[0].IndexOf('=', StringComparison.Ordinal);
            string nome = partes[0][..separador];
            string valor = partes[0][(separador + 1)..];
            bool expirado = partes.Skip(1).Any(p =>
                p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase)
                && DateTimeOffset.TryParse(p[8..], System.Globalization.CultureInfo.InvariantCulture, out DateTimeOffset expira)
                && expira.Year < 2000);
            if (expirado || valor.Length == 0)
            {
                Cookies.Remove(nome);
            }
            else
            {
                Cookies[nome] = valor;
            }
        }
    }
}
