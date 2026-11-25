namespace Platform.IntegrationTests.Consumidor;

using System.Net;
using System.Text;
using System.Text.Json;
using Platform.Domain.Tenancy;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Http.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// T120 / SC-005: Teste de aceitação do consumidor HTTP independente baseado exclusivamente
/// no contrato publicado openapi.json e em tipos nativos do .NET (HttpClient + CookieContainer),
/// sem qualquer referência a tipos internos de App.Api.
/// </summary>
public sealed class TestesConsumidorIndependente(FixtureInstalacaoPostgres fixture)
    : TesteHttpBase(fixture), IClassFixture<FixtureInstalacaoPostgres>
{
    private static readonly string CaminhoContratoOpenApi = ObterCaminhoContratoOpenApi();

    [Fact]
    public async Task ConsumidorIndependente_JornadaCompleta_EmAEB_ValidaComOpenApiESemTiposInternos()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;

        // Valida que o contrato openapi.json existe e possui os esquemas contratados
        string conteudoOpenApi = await File.ReadAllTextAsync(CaminhoContratoOpenApi, cancelamento);
        using JsonDocument docContrato = JsonDocument.Parse(conteudoOpenApi);
        JsonElement schemas = docContrato.RootElement.GetProperty("components").GetProperty("schemas");

        Assert.True(schemas.TryGetProperty("AntiforgeryToken", out _));
        Assert.True(schemas.TryGetProperty("LoginResponse", out _));
        Assert.True(schemas.TryGetProperty("CurrentUser", out _));
        Assert.True(schemas.TryGetProperty("TenantContext", out _));
        Assert.True(schemas.TryGetProperty("ProblemDetails", out _));

        string emailA = ContasSinteticas.NovoEmail("consumidor_a");
        string emailB = ContasSinteticas.NovoEmail("consumidor_b");
        Guid idA = await ContasSinteticas.CriarContaAsync(A, Relogio, emailA, ContasSinteticas.SenhaPadraoA, conceder: true, nome: "Consumidor A");
        Guid idB = await ContasSinteticas.CriarContaAsync(B, Relogio, emailB, ContasSinteticas.SenhaPadraoB, conceder: true, nome: "Consumidor B");

        FabricaApiInstalacao apiA = CriarApi(A);
        FabricaApiInstalacao apiB = CriarApi(B);

        // Executa a jornada completa em jars isolados para Tenant A e Tenant B
        await ExecutarJornadaConsumidorAsync(apiA, A.IdTenant, emailA, ContasSinteticas.SenhaPadraoA, idA, "Consumidor A", cancelamento);
        await ExecutarJornadaConsumidorAsync(apiB, B.IdTenant, emailB, ContasSinteticas.SenhaPadraoB, idB, "Consumidor B", cancelamento);
    }

    [Fact]
    public async Task ConsumidorIndependente_ReconheceCodigosDeErroPadronizados_ETraceIdHexadecimal()
    {
        CancellationToken cancelamento = TestContext.Current.CancellationToken;
        FabricaApiInstalacao api = CriarApi(A);
        CookieContainer jar = new();
        using HttpClient http = CriarHttpClient(api, jar);

        string tenantValido = A.IdTenant.ToString();

        // 1. INPUT_INVALID (400) - JSON malformado ou campo obrigatório vazio
        using (HttpRequestMessage req = new(HttpMethod.Post, "/api/v1/sessions"))
        {
            req.Headers.Add("X-Tenant-Id", tenantValido);
            req.Content = new StringContent("""{"email":"invalido","password":""}""", Encoding.UTF8, "application/json");
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            JsonElement problema = await ValidarProblemDetailsAsync(resp, HttpStatusCode.BadRequest, "INPUT_INVALID", cancelamento);
            Assert.True(problema.TryGetProperty("errors", out _));
        }

        // 2. CSRF_INVALID (400) - Tentativa de login sem token CSRF
        using (HttpRequestMessage req = new(HttpMethod.Post, "/api/v1/sessions"))
        {
            req.Headers.Add("X-Tenant-Id", tenantValido);
            req.Content = new StringContent("""{"email":"teste@teste.local","password":"SenhaValida123!"}""", Encoding.UTF8, "application/json");
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            await ValidarProblemDetailsAsync(resp, HttpStatusCode.BadRequest, "CSRF_INVALID", cancelamento);
        }

        // 3. AUTH_REQUIRED (401) - /me sem sessão
        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/me"))
        {
            req.Headers.Add("X-Tenant-Id", tenantValido);
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            await ValidarProblemDetailsAsync(resp, HttpStatusCode.Unauthorized, "AUTH_REQUIRED", cancelamento);
        }

        // 4. TENANT_ACCESS_DENIED (403) - X-Tenant-Id divergente em rota protegida
        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/me"))
        {
            req.Headers.Add("X-Tenant-Id", Guid.NewGuid().ToString());
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            await ValidarProblemDetailsAsync(resp, HttpStatusCode.Forbidden, "TENANT_ACCESS_DENIED", cancelamento);
        }

        // Conta sem permissão de contexto -> 403 TENANT_ACCESS_DENIED
        string emailSemPerm = ContasSinteticas.NovoEmail("sem_perm");
        await ContasSinteticas.CriarContaAsync(A, Relogio, emailSemPerm, ContasSinteticas.SenhaPadraoA, conceder: false);

        CookieContainer jarSemPerm = new();
        using HttpClient httpSemPerm = CriarHttpClient(api, jarSemPerm);

        // Obter antiforgery
        string csrfToken;
        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/antiforgery-token"))
        {
            req.Headers.Add("X-Tenant-Id", tenantValido);
            using HttpResponseMessage resp = await httpSemPerm.SendAsync(req, cancelamento);
            string json = await resp.Content.ReadAsStringAsync(cancelamento);
            using JsonDocument doc = JsonDocument.Parse(json);
            csrfToken = doc.RootElement.GetProperty("requestToken").GetString()!;
        }

        // Login
        using (HttpRequestMessage req = new(HttpMethod.Post, "/api/v1/sessions"))
        {
            req.Headers.Add("X-Tenant-Id", tenantValido);
            req.Headers.Add("X-CSRF-TOKEN", csrfToken);
            req.Content = new StringContent(JsonSerializer.Serialize(new { email = emailSemPerm, password = ContasSinteticas.SenhaPadraoA }), Encoding.UTF8, "application/json");
            using HttpResponseMessage resp = await httpSemPerm.SendAsync(req, cancelamento);
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
        }

        // /context sem permissão -> 403 TENANT_ACCESS_DENIED
        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/context"))
        {
            req.Headers.Add("X-Tenant-Id", tenantValido);
            using HttpResponseMessage resp = await httpSemPerm.SendAsync(req, cancelamento);
            await ValidarProblemDetailsAsync(resp, HttpStatusCode.Forbidden, "TENANT_ACCESS_DENIED", cancelamento);
        }

        // 5. TENANT_UNAVAILABLE (409) - Sessão válida com instalação inativa
        FabricaApiInstalacao apiInativa = CriarApi(A, new OpcoesFabricaApi
        {
            Estado = EstadoTenant.Inativo,
            DiretorioChaves = api.DiretorioChaves,
        });
        using HttpClient httpInativa = CriarHttpClient(apiInativa, jarSemPerm);

        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/me"))
        {
            req.Headers.Add("X-Tenant-Id", tenantValido);
            using HttpResponseMessage resp = await httpInativa.SendAsync(req, cancelamento);
            await ValidarProblemDetailsAsync(resp, HttpStatusCode.Conflict, "TENANT_UNAVAILABLE", cancelamento);
        }
    }

    private static async Task ExecutarJornadaConsumidorAsync(
        FabricaApiInstalacao api,
        Guid idTenant,
        string email,
        string senha,
        Guid idUsuarioEsperado,
        string nomeEsperado,
        CancellationToken cancelamento)
    {
        string tenantIdStr = idTenant.ToString();
        CookieContainer jar = new();
        using HttpClient http = CriarHttpClient(api, jar);

        // Passo 1: GET /api/v1/antiforgery-token
        string csrfToken;
        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/antiforgery-token"))
        {
            req.Headers.Add("X-Tenant-Id", tenantIdStr);
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.True(resp.Headers.CacheControl?.NoStore);

            string json = await resp.Content.ReadAsStringAsync(cancelamento);
            AssegurarSemSegredos(json, senha, api);

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement raiz = doc.RootElement;
            Assert.True(raiz.TryGetProperty("requestToken", out JsonElement propToken));
            Assert.True(raiz.TryGetProperty("headerName", out JsonElement propHeader));
            Assert.Equal("X-CSRF-TOKEN", propHeader.GetString());
            csrfToken = propToken.GetString()!;
            Assert.False(string.IsNullOrWhiteSpace(csrfToken));
        }

        // Passo 2: POST /api/v1/sessions (login)
        using (HttpRequestMessage req = new(HttpMethod.Post, "/api/v1/sessions"))
        {
            req.Headers.Add("X-Tenant-Id", tenantIdStr);
            req.Headers.Add("X-CSRF-TOKEN", csrfToken);
            req.Content = new StringContent(
                JsonSerializer.Serialize(new { email, password = senha }),
                Encoding.UTF8,
                "application/json");

            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            Assert.Equal(HttpStatusCode.Created, resp.StatusCode);
            Assert.True(resp.Headers.CacheControl?.NoStore);

            string json = await resp.Content.ReadAsStringAsync(cancelamento);
            AssegurarSemSegredos(json, senha, api);

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement raiz = doc.RootElement;
            Assert.Equal(idTenant, raiz.GetProperty("tenantId").GetGuid());
            Assert.True(raiz.TryGetProperty("expiresAt", out _));
            AssegurarConformeSchema(raiz, "LoginResponse");
        }

        // Passo 3: Novo GET antiforgery após login
        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/antiforgery-token"))
        {
            req.Headers.Add("X-Tenant-Id", tenantIdStr);
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            string json = await resp.Content.ReadAsStringAsync(cancelamento);
            using JsonDocument doc = JsonDocument.Parse(json);
            csrfToken = doc.RootElement.GetProperty("requestToken").GetString()!;
        }

        // Passo 4: GET /api/v1/me
        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/me"))
        {
            req.Headers.Add("X-Tenant-Id", tenantIdStr);
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.True(resp.Headers.CacheControl?.NoStore);

            string json = await resp.Content.ReadAsStringAsync(cancelamento);
            AssegurarSemSegredos(json, senha, api);

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement raiz = doc.RootElement;
            Assert.Equal(idUsuarioEsperado, raiz.GetProperty("id").GetGuid());
            Assert.Equal(idTenant, raiz.GetProperty("tenantId").GetGuid());
            Assert.Equal(email, raiz.GetProperty("email").GetString());
            Assert.Equal(nomeEsperado, raiz.GetProperty("displayName").GetString());
            Assert.True(raiz.TryGetProperty("sessionExpiresAt", out _));
            AssegurarConformeSchema(raiz, "CurrentUser");
        }

        // Passo 5: GET /api/v1/context
        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/context"))
        {
            req.Headers.Add("X-Tenant-Id", tenantIdStr);
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            Assert.True(resp.Headers.CacheControl?.NoStore);

            string json = await resp.Content.ReadAsStringAsync(cancelamento);
            AssegurarSemSegredos(json, senha, api);

            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement raiz = doc.RootElement;
            Assert.Equal(idUsuarioEsperado, raiz.GetProperty("user").GetProperty("id").GetGuid());
            Assert.Equal(idTenant, raiz.GetProperty("tenant").GetProperty("id").GetGuid());
            Assert.Contains("foundation.context.read", raiz.GetProperty("permissions").EnumerateArray().Select(p => p.GetString()));
            Assert.True(raiz.TryGetProperty("applicationVersion", out _));
        }

        // Passo 6: DELETE /api/v1/sessions/current (logout)
        using (HttpRequestMessage req = new(HttpMethod.Delete, "/api/v1/sessions/current"))
        {
            req.Headers.Add("X-Tenant-Id", tenantIdStr);
            req.Headers.Add("X-CSRF-TOKEN", csrfToken);
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            Assert.Equal(HttpStatusCode.NoContent, resp.StatusCode);
        }

        // Passo 7: GET /api/v1/me após logout -> 401 AUTH_REQUIRED
        using (HttpRequestMessage req = new(HttpMethod.Get, "/api/v1/me"))
        {
            req.Headers.Add("X-Tenant-Id", tenantIdStr);
            using HttpResponseMessage resp = await http.SendAsync(req, cancelamento);
            await ValidarProblemDetailsAsync(resp, HttpStatusCode.Unauthorized, "AUTH_REQUIRED", cancelamento);
        }
    }

    private static HttpClient CriarHttpClient(FabricaApiInstalacao api, CookieContainer jar)
    {
        return api.CreateDefaultClient(new CookieHandler(jar));
    }

    private sealed class CookieHandler(CookieContainer container) : DelegatingHandler
    {
        private static readonly Uri BaseUri = new("http://localhost");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string cookieHeader = container.GetCookieHeader(BaseUri);
            if (!string.IsNullOrEmpty(cookieHeader))
            {
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            }

            HttpResponseMessage response = await base.SendAsync(request, cancellationToken);

            if (response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? setCookies))
            {
                foreach (string setCookie in setCookies)
                {
                    string partePrincipal = setCookie.Split(';')[0];
                    string[] par = partePrincipal.Split('=', 2);
                    if (par.Length == 2)
                    {
                        string nome = par[0].Trim();
                        string valor = par[1].Trim();
                        bool expirado = string.IsNullOrEmpty(valor) ||
                            setCookie.Contains("1970", StringComparison.OrdinalIgnoreCase);

                        container.Add(new Cookie(nome, valor, "/", "localhost")
                        {
                            Expired = expirado,
                        });
                    }
                }
            }

            return response;
        }
    }

    private static async Task<JsonElement> ValidarProblemDetailsAsync(
        HttpResponseMessage resposta,
        HttpStatusCode statusEsperado,
        string codigoEsperado,
        CancellationToken cancelamento)
    {
        Assert.Equal(statusEsperado, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.True(resposta.Headers.CacheControl?.NoStore);

        string json = await resposta.Content.ReadAsStringAsync(cancelamento);
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement raiz = doc.RootElement.Clone();

        Assert.Equal(codigoEsperado, raiz.GetProperty("code").GetString());
        Assert.Equal((int)statusEsperado, raiz.GetProperty("status").GetInt32());
        Assert.True(raiz.TryGetProperty("title", out _));
        Assert.True(raiz.TryGetProperty("type", out _));

        string traceId = raiz.GetProperty("traceId").GetString()!;
        Assert.Matches("^[0-9a-f]{32}$", traceId);

        return raiz;
    }

    /// <summary>
    /// Confere a resposta só pelo schema do arquivo publicado: todo campo <c>required</c> presente e
    /// inteiros dentro de <c>minimum</c>/<c>maximum</c> (inclui os campos aditivos do contrato 2.1.0).
    /// </summary>
    private static void AssegurarConformeSchema(JsonElement resposta, string nomeSchema)
    {
        using JsonDocument contrato = JsonDocument.Parse(File.ReadAllText(CaminhoContratoOpenApi));
        JsonElement schema = contrato.RootElement.GetProperty("components").GetProperty("schemas").GetProperty(nomeSchema);
        foreach (JsonElement obrigatorio in schema.GetProperty("required").EnumerateArray())
        {
            string campo = obrigatorio.GetString()!;
            Assert.True(resposta.TryGetProperty(campo, out JsonElement valor), $"{nomeSchema}.{campo} ausente na resposta.");
            JsonElement propriedade = schema.GetProperty("properties").GetProperty(campo);
            if (propriedade.TryGetProperty("type", out JsonElement tipo) && tipo.GetString() == "integer")
            {
                int numero = valor.GetInt32();
                Assert.True(!propriedade.TryGetProperty("minimum", out JsonElement minimo) || numero >= minimo.GetInt32(), $"{nomeSchema}.{campo} abaixo do mínimo.");
                Assert.True(!propriedade.TryGetProperty("maximum", out JsonElement maximo) || numero <= maximo.GetInt32(), $"{nomeSchema}.{campo} acima do máximo.");
            }
        }
    }

    private static void AssegurarSemSegredos(string json, string senha, FabricaApiInstalacao api)
    {
        Assert.DoesNotContain(senha, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordHash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecurityStamp", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConcurrencyStamp", json, StringComparison.OrdinalIgnoreCase);
    }

    private static string ObterCaminhoContratoOpenApi()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            string candidato = Path.Combine(dir, "specs", "001-fullstack-foundation", "contracts", "openapi.json");
            if (File.Exists(candidato))
            {
                return candidato;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "specs", "001-fullstack-foundation", "contracts", "openapi.json"));
    }
}
