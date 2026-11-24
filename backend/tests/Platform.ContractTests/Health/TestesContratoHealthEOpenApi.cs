namespace Platform.ContractTests.Health;

using System.Net;
using System.Reflection;
using System.Text.Json;
using App.Api.Diagnostics;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Platform.Application.Access;
using Platform.Application.Tenancy;
using Platform.ContractTests.Suporte;
using Platform.Domain.Resultados;
using Platform.Domain.Tenancy;
using Xunit;

/// <summary>
/// T119: contrato HTTP L2 servido em <c>/openapi/v1.json</c> igual ao promovido em
/// <c>contracts/openapi.json</c> (sete operações, status, <c>SessionCookie</c>, enum de <c>code</c>),
/// health checks seguros e rotas ausentes em 404, no host Production com key ring e certificado.
/// </summary>
public sealed class TestesContratoHealthEOpenApi
{
    private static readonly string[] OperacoesContrato =
    [
        "getAntiforgeryToken", "createSession", "deleteCurrentSession", "getCurrentUser", "getTenantContext", "getLiveness", "getReadiness",
    ];

    [Fact]
    public async Task GetLive_Retorna200EStatusAlive_SemDependerDeBancoOuCookies()
    {
        await using FabricaApiContrato aplicacao = new(verificador: new VerificadorIdentidadeBancoFake(
            () => throw new InvalidOperationException("Banco não deve ser acessado por liveness!")));
        using HttpClient cliente = aplicacao.CriarCliente();
        cliente.DefaultRequestHeaders.Add("Cookie", "__Host-bps-session=token-sintetico-que-nao-deve-ser-lido");
        cliente.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/live", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);
        using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("alive", documento.RootElement.GetProperty("status").GetString());
        Assert.False(resposta.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task GetReady_Retorna200_ComBancoEKeyRingProntos()
    {
        await using FabricaApiContrato aplicacao = new();
        using HttpClient cliente = aplicacao.CriarCliente();

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        using JsonDocument documento = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal("ready", documento.RootElement.GetProperty("status").GetString());
    }

    public static TheoryData<string> CenariosNaoProntos => ["marcador-divergente", "excecao", "inativo", "migracoes-pendentes"];

    [Theory]
    [MemberData(nameof(CenariosNaoProntos))]
    public async Task GetReady_Retorna503ProblemDetailsSeguro(string cenario)
    {
        const string SegredoPrivado = "postgres-secret-que-nao-pode-vazar";
        IdentidadeBancoTenantSnapshot pronto = FabricaApiContrato.MarcadorPronto();
        await using FabricaApiContrato aplicacao = cenario switch
        {
            "marcador-divergente" => new(verificador: new VerificadorIdentidadeBancoFake(() => Task.FromResult(pronto with { IdTenant = Guid.NewGuid() }))),
            "excecao" => new(verificador: new VerificadorIdentidadeBancoFake(() => throw new TimeoutException($"Conexão falhou para {SegredoPrivado}"))),
            "inativo" => new(FabricaApiContrato.CriarOpcoesValidas(EstadoTenant.Inativo)),
            _ => new(verificador: new VerificadorIdentidadeBancoFake(() => Task.FromResult(pronto with { PossuiVersaoMigracaoEsperada = false }))),
        };
        using HttpClient cliente = aplicacao.CriarCliente();

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        await AssegurarProblemaAsync(resposta, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE", SegredoPrivado);
    }

    [Fact]
    public async Task GetReady_Retorna503_QuandoKeyRingIndisponivelMesmoComBancoPronto()
    {
        await using FabricaApiContrato aplicacao = new();
        using HttpClient cliente = aplicacao.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IDataProtectionProvider>();
            s.AddSingleton<IDataProtectionProvider, ProvedorProtecaoIndisponivel>();
        })).CreateClient(new() { BaseAddress = new Uri("https://localhost") });

        using HttpResponseMessage resposta = await cliente.GetAsync("/health/ready", TestContext.Current.CancellationToken);

        await AssegurarProblemaAsync(resposta, HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE", null);
    }

    [Fact]
    public async Task OpenApiServido_TemExatamenteAsSeteOperacoesEStatusDoContratoPromovido()
    {
        await using FabricaApiContrato aplicacao = new();
        using HttpClient cliente = aplicacao.CriarCliente();
        using HttpResponseMessage resposta = await cliente.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument servido = JsonDocument.Parse(corpo);
        using JsonDocument contrato = JsonDocument.Parse(await File.ReadAllTextAsync(CaminhoContrato(), TestContext.Current.CancellationToken));

        Assert.StartsWith("3.0", servido.RootElement.GetProperty("openapi").GetString(), StringComparison.Ordinal);
        Assert.Equal(contrato.RootElement.GetProperty("info").GetProperty("version").GetString(), servido.RootElement.GetProperty("info").GetProperty("version").GetString());
        Assert.DoesNotContain("I1", servido.RootElement.GetProperty("info").GetProperty("title").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain("x-planned-only", corpo, StringComparison.Ordinal);
        Assert.False(contrato.RootElement.TryGetProperty("x-planned-only", out _));

        Dictionary<string, (string Rota, string Metodo, string[] Status, bool Anonima)> operacoesServidas = Operacoes(servido.RootElement);
        Dictionary<string, (string Rota, string Metodo, string[] Status, bool Anonima)> operacoesContrato = Operacoes(contrato.RootElement);
        Assert.Equal(OperacoesContrato.Order(StringComparer.Ordinal), operacoesServidas.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(OperacoesContrato.Order(StringComparer.Ordinal), operacoesContrato.Keys.Order(StringComparer.Ordinal));
        foreach (string operacao in OperacoesContrato)
        {
            Assert.Equal(operacoesContrato[operacao].Rota, operacoesServidas[operacao].Rota);
            Assert.Equal(operacoesContrato[operacao].Metodo, operacoesServidas[operacao].Metodo);
            Assert.Equal(operacoesContrato[operacao].Status, operacoesServidas[operacao].Status);
            Assert.Equal(operacoesContrato[operacao].Anonima, operacoesServidas[operacao].Anonima);
        }
    }

    [Fact]
    public async Task OpenApi_210_ExigeJanelaDeInatividade_NoLoginENoUsuarioAtual()
    {
        await using FabricaApiContrato aplicacao = new();
        using HttpClient cliente = aplicacao.CriarCliente();
        using HttpResponseMessage resposta = await cliente.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using JsonDocument servido = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using JsonDocument contrato = JsonDocument.Parse(await File.ReadAllTextAsync(CaminhoContrato(), TestContext.Current.CancellationToken));

        Assert.Equal("2.1.0", contrato.RootElement.GetProperty("info").GetProperty("version").GetString());
        Assert.Equal("2.1.0", servido.RootElement.GetProperty("info").GetProperty("version").GetString());
        const string DescricaoExpiracao =
            "Expiração por inatividade (padrão 30 min, 5–240) ou teto absoluto (padrão 600 min, 30–720), o que ocorrer primeiro, " +
            "ambos fixados na emissão; o uso renova só a inatividade";
        foreach (JsonDocument documento in new[] { servido, contrato })
        {
            string descricao = documento.RootElement.GetProperty("info").GetProperty("description").GetString()!;
            Assert.Contains(DescricaoExpiracao, descricao, StringComparison.Ordinal);
            Assert.DoesNotContain("5 a 480", descricao, StringComparison.Ordinal);
        }

        JsonElement schemasContrato = contrato.RootElement.GetProperty("components").GetProperty("schemas");
        AssegurarCampoJanela(schemasContrato.GetProperty("LoginResponse"), "idleTimeoutMinutes");
        AssegurarCampoJanela(schemasContrato.GetProperty("CurrentUser"), "sessionIdleTimeoutMinutes");

        // Servido: os schemas das respostas 201 do login e 200 de /me declaram os campos aditivos.
        AssegurarPropriedade(servido.RootElement, "/api/v1/sessions", "post", "201", "idleTimeoutMinutes");
        AssegurarPropriedade(servido.RootElement, "/api/v1/me", "get", "200", "sessionIdleTimeoutMinutes");
    }

    [Fact]
    public async Task OpenApiServido_TemSessionCookieEEnumDeCodigosDaPlataforma()
    {
        await using FabricaApiContrato aplicacao = new();
        using HttpClient cliente = aplicacao.CriarCliente();
        using HttpResponseMessage resposta = await cliente.GetAsync("/openapi/v1.json", TestContext.Current.CancellationToken);
        using JsonDocument servido = JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using JsonDocument contrato = JsonDocument.Parse(await File.ReadAllTextAsync(CaminhoContrato(), TestContext.Current.CancellationToken));

        JsonElement esquema = servido.RootElement.GetProperty("components").GetProperty("securitySchemes").GetProperty("SessionCookie");
        Assert.Equal("apiKey", esquema.GetProperty("type").GetString());
        Assert.Equal("cookie", esquema.GetProperty("in").GetString());
        Assert.Equal("__Host-bps-session", esquema.GetProperty("name").GetString());

        string[] catalogo = CodigosCatalogo().Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(catalogo, EnumCodigo(servido.RootElement).Order(StringComparer.Ordinal));
        Assert.Equal(catalogo, EnumCodigo(contrato.RootElement).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("/api/v1/me/tenants")]
    [InlineData("/api/v1/auth/login")]
    [InlineData("/api/v1/users")]
    [InlineData("/rota-inexistente")]
    public async Task RotasAusentes_Retornam404ProblemDetails(string rotaAusente)
    {
        await using FabricaApiContrato aplicacao = new();
        using HttpClient cliente = aplicacao.CriarCliente();

        using HttpResponseMessage resposta = await cliente.GetAsync(rotaAusente, TestContext.Current.CancellationToken);

        await AssegurarProblemaAsync(resposta, HttpStatusCode.NotFound, "NOT_FOUND", null);
    }

    private static async Task AssegurarProblemaAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo, string? segredo)
    {
        Assert.Equal(status, resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument documento = JsonDocument.Parse(corpo);
        JsonElement raiz = documento.RootElement;
        Assert.Equal((int)status, raiz.GetProperty("status").GetInt32());
        Assert.Equal(codigo, raiz.GetProperty("code").GetString());
        Assert.True(Uri.IsWellFormedUriString(raiz.GetProperty("type").GetString(), UriKind.Absolute));
        Assert.Matches("^[0-9a-f]{32}$", raiz.GetProperty("traceId").GetString()!);
        Assert.DoesNotContain("Host=", corpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", corpo, StringComparison.OrdinalIgnoreCase);
        if (segredo is not null)
        {
            Assert.DoesNotContain(segredo, corpo, StringComparison.Ordinal);
        }
    }

    private static Dictionary<string, (string Rota, string Metodo, string[] Status, bool Anonima)> Operacoes(JsonElement documento)
    {
        bool segurancaGlobal = documento.TryGetProperty("security", out JsonElement global) && global.GetArrayLength() > 0;
        Dictionary<string, (string, string, string[], bool)> operacoes = new(StringComparer.Ordinal);
        foreach (JsonProperty rota in documento.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty metodo in rota.Value.EnumerateObject())
            {
                JsonElement operacao = metodo.Value;
                string[] status = operacao.GetProperty("responses").EnumerateObject().Select(r => r.Name).Order(StringComparer.Ordinal).ToArray();
                bool anonima = operacao.TryGetProperty("security", out JsonElement seguranca) ? seguranca.GetArrayLength() == 0 : !segurancaGlobal;
                operacoes.Add(operacao.GetProperty("operationId").GetString()!, (rota.Name, metodo.Name, status, anonima));
            }
        }

        return operacoes;
    }

    private static void AssegurarCampoJanela(JsonElement schema, string campo)
    {
        Assert.Contains(campo, schema.GetProperty("required").EnumerateArray().Select(r => r.GetString()));
        JsonElement propriedade = schema.GetProperty("properties").GetProperty(campo);
        Assert.Equal("integer", propriedade.GetProperty("type").GetString());
        Assert.Equal(5, propriedade.GetProperty("minimum").GetInt32());
        Assert.Equal(240, propriedade.GetProperty("maximum").GetInt32());
    }

    private static void AssegurarPropriedade(JsonElement documento, string rota, string metodo, string status, string campo)
    {
        JsonElement schema = documento.GetProperty("paths").GetProperty(rota).GetProperty(metodo)
            .GetProperty("responses").GetProperty(status).GetProperty("content").GetProperty("application/json").GetProperty("schema");
        if (schema.TryGetProperty("$ref", out JsonElement referencia))
        {
            string nome = referencia.GetString()!.Split('/')[^1];
            schema = documento.GetProperty("components").GetProperty("schemas").GetProperty(nome);
        }

        Assert.True(schema.GetProperty("properties").TryGetProperty(campo, out _), $"{rota} {status} sem {campo}.");
        Assert.Contains(campo, schema.GetProperty("required").EnumerateArray().Select(r => r.GetString()));
    }

    private static IEnumerable<string> EnumCodigo(JsonElement documento) =>
        documento.GetProperty("components").GetProperty("schemas").GetProperty("ProblemDetails")
            .GetProperty("properties").GetProperty("code").GetProperty("enum").EnumerateArray().Select(c => c.GetString()!);

    private static IEnumerable<string> CodigosCatalogo() =>
        typeof(ErrosAcesso).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(c => c.FieldType == typeof(Erro))
            .Select(c => ((Erro)c.GetValue(null)!).Codigo)
            .Concat(typeof(ErrosPlataforma).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(c => c.IsLiteral)
                .Select(c => (string)c.GetRawConstantValue()!));

    private static string CaminhoContrato()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (diretorio is not null)
        {
            string candidato = Path.Combine(diretorio, "specs", "001-fullstack-foundation", "contracts", "openapi.json");
            if (File.Exists(candidato))
            {
                return candidato;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new InvalidOperationException("contracts/openapi.json não localizado.");
    }

    private sealed class ProvedorProtecaoIndisponivel : IDataProtectionProvider, IDataProtector
    {
        public IDataProtector CreateProtector(string purpose) => this;

        public byte[] Protect(byte[] plaintext) => throw new System.Security.Cryptography.CryptographicException("Key ring indisponível.");

        public byte[] Unprotect(byte[] protectedData) => throw new System.Security.Cryptography.CryptographicException("Key ring indisponível.");
    }
}
