namespace Platform.IntegrationTests.Http.Suporte;

using System.Net;
using System.Text.Json;
using Platform.IntegrationTests.Fixtures;
using Platform.IntegrationTests.Suporte;
using Platform.Testes.Comum;
using Xunit;

/// <summary>
/// Base das suítes HTTP: migra A e B pela via do tooling e oferece relógio controlável,
/// fábricas por instalação e asserções de Problem Details.
/// </summary>
/// <param name="fixture">Contêiner PostgreSQL com as instalações A e B.</param>
public abstract class TesteHttpBase(FixtureInstalacaoPostgres fixture) : IAsyncLifetime
{
    /// <summary>Instante inicial explícito de todas as suítes HTTP.</summary>
    public static readonly DateTimeOffset InstanteInicial = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private readonly List<IDisposable> _descartaveis = [];

    /// <summary>Fixture compartilhada da classe.</summary>
    protected FixtureInstalacaoPostgres Fixture => fixture;

    /// <summary>Instalação A.</summary>
    protected InstalacaoTestePostgres A => fixture.TenantA;

    /// <summary>Instalação B.</summary>
    protected InstalacaoTestePostgres B => fixture.TenantB;

    /// <summary>Relógio do teste.</summary>
    protected RelogioControlavel Relogio { get; } = new(InstanteInicial);

    /// <inheritdoc />
    public async ValueTask InitializeAsync()
    {
        await BancoTeste.MigrarAsync(A, Relogio, TestContext.Current.CancellationToken);
        await BancoTeste.MigrarAsync(B, Relogio, TestContext.Current.CancellationToken);
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync()
    {
        foreach (IDisposable descartavel in Enumerable.Reverse(_descartaveis))
        {
            descartavel.Dispose();
        }

        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Cria uma API de teste para a instalação, descartada ao fim do teste.
    /// </summary>
    /// <param name="instalacao">Instalação.</param>
    /// <param name="opcoes">Opções.</param>
    /// <returns>Fábrica.</returns>
    protected FabricaApiInstalacao CriarApi(InstalacaoTestePostgres instalacao, OpcoesFabricaApi? opcoes = null)
    {
        FabricaApiInstalacao fabrica = new(instalacao, Relogio, opcoes);
        _descartaveis.Add(fabrica);
        return fabrica;
    }

    /// <summary>
    /// Cria um cliente descartado ao fim do teste.
    /// </summary>
    /// <param name="fabrica">API.</param>
    /// <param name="https">Base HTTPS.</param>
    /// <returns>Cliente.</returns>
    protected ClienteAcessoHttp CriarCliente(FabricaApiInstalacao fabrica, bool https = false)
    {
        ArgumentNullException.ThrowIfNull(fabrica);
        ClienteAcessoHttp cliente = fabrica.CriarClienteAcesso(https);
        _descartaveis.Add(cliente);
        return cliente;
    }

    /// <summary>
    /// Confere status, <c>application/problem+json</c>, <c>code</c>, traceId e <c>no-store</c>.
    /// </summary>
    /// <param name="resposta">Resposta.</param>
    /// <param name="status">Status esperado.</param>
    /// <param name="codigo">Código esperado.</param>
    /// <returns>Corpo do problema.</returns>
    protected static async Task<JsonElement> AssegurarProblemaAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(resposta.StatusCode == status, $"Esperado {(int)status} {codigo}, obtido {(int)resposta.StatusCode}: {corpo}");
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.True(resposta.Headers.CacheControl?.NoStore, "Cache-Control deve conter no-store.");
        JsonElement raiz = JsonDocument.Parse(corpo).RootElement.Clone();
        Assert.Equal(codigo, raiz.GetProperty("code").GetString());
        Assert.Matches("^[0-9a-f]{32}$", raiz.GetProperty("traceId").GetString()!);
        return raiz;
    }

    /// <summary>
    /// Lê o corpo JSON de uma resposta de sucesso, conferindo status e <c>no-store</c>.
    /// </summary>
    /// <param name="resposta">Resposta.</param>
    /// <param name="status">Status esperado.</param>
    /// <returns>Corpo.</returns>
    protected static async Task<JsonElement> AssegurarSucessoAsync(HttpResponseMessage resposta, HttpStatusCode status = HttpStatusCode.OK)
    {
        ArgumentNullException.ThrowIfNull(resposta);
        string corpo = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.True(resposta.StatusCode == status, $"Esperado {(int)status}, obtido {(int)resposta.StatusCode}: {corpo}");
        Assert.True(resposta.Headers.CacheControl?.NoStore, "Cache-Control deve conter no-store.");
        return corpo.Length == 0 ? default : JsonDocument.Parse(corpo).RootElement.Clone();
    }
}
