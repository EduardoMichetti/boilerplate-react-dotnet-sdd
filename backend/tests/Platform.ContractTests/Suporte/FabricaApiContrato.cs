namespace Platform.ContractTests.Suporte;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Platform.Application.Configuration;
using Platform.Application.Tenancy;
using Platform.Domain.Tenancy;
using Platform.Testes.Comum;

/// <summary>
/// Host da API para os testes de contrato em Production (T119): certificado de teste gerado na
/// execução, diretório temporário de key ring por instância, opções de instalação e verificador
/// do marcador substituíveis, sem banco real.
/// </summary>
public sealed class FabricaApiContrato : WebApplicationFactory<Program>
{
    /// <summary>Ambiente padrão do contrato.</summary>
    public static readonly Guid IdAmbientePadrao = Guid.Parse("11111111-1111-4111-8111-111111111111");

    /// <summary>Tenant padrão do contrato.</summary>
    public static readonly Guid IdTenantPadrao = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    /// <summary>Época padrão do marcador.</summary>
    public static readonly Guid EpocaAcessoPadrao = Guid.Parse("33333333-3333-4333-8333-333333333333");

    private readonly CertificadoTeste _certificado = new();
    private readonly string _diretorioChaves = Directory.CreateTempSubdirectory("bps-chaves-contrato-").FullName;
    private readonly OpcoesInstalacaoTenant _opcoes;
    private readonly IVerificadorIdentidadeBancoTenant _verificador;

    /// <summary>
    /// Cria a fábrica.
    /// </summary>
    /// <param name="opcoes">Opções da instalação; nulo usa as válidas e ativas.</param>
    /// <param name="verificador">Verificador do marcador; nulo usa um marcador pronto e coincidente.</param>
    public FabricaApiContrato(OpcoesInstalacaoTenant? opcoes = null, IVerificadorIdentidadeBancoTenant? verificador = null)
    {
        _opcoes = opcoes ?? CriarOpcoesValidas();
        _verificador = verificador ?? new VerificadorIdentidadeBancoFake(() => Task.FromResult(MarcadorPronto()));
    }

    /// <summary>
    /// Opções válidas da instalação de contrato.
    /// </summary>
    /// <param name="estado">Estado configurado.</param>
    /// <returns>Opções.</returns>
    public static OpcoesInstalacaoTenant CriarOpcoesValidas(EstadoTenant estado = EstadoTenant.Ativo) => new()
    {
        CodigoAplicacao = "boilerplate-react-dotnet-sdd",
        IdAmbiente = IdAmbientePadrao,
        IdTenant = IdTenantPadrao,
        NomeExibicaoTenant = "Cliente de Teste",
        EstadoTenant = estado,
        StringsConexao = new StringsConexaoInstalacao { BancoApp = "Host=localhost;Database=banco_teste;Username=runtime_teste" },
    };

    /// <summary>
    /// Marcador coincidente com cadeia de migrations completa.
    /// </summary>
    /// <returns>Snapshot.</returns>
    public static IdentidadeBancoTenantSnapshot MarcadorPronto() =>
        new(IdTenantPadrao, IdAmbientePadrao, EpocaAcessoPadrao, PossuiVersaoMigracaoEsperada: true);

    /// <summary>
    /// Cliente HTTPS (cookies Secure em Production).
    /// </summary>
    /// <returns>Cliente.</returns>
    public HttpClient CriarCliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        HandleCookies = false,
    });

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Production");
        builder.UseSetting("ProtecaoDados:DiretorioChaves", _diretorioChaves);
        builder.UseSetting("ProtecaoDados:ArquivoCertificado", _certificado.ArquivoCertificado);
        builder.UseSetting("ProtecaoDados:ArquivoSenhaCertificado", _certificado.ArquivoSenha);
        builder.UseSetting("Sessao:InatividadeMinutos", "30");
        builder.UseSetting("Sessao:TetoAbsolutoMinutos", "600");
        builder.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<IOptions<OpcoesInstalacaoTenant>>();
            servicos.AddSingleton(Options.Create(_opcoes));
            servicos.RemoveAll<IVerificadorIdentidadeBancoTenant>();
            servicos.AddScoped(_ => _verificador);
        });
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _certificado.Dispose();
            try
            {
                Directory.Delete(_diretorioChaves, recursive: true);
            }
            catch (IOException)
            {
                // Diretório temporário: limpeza best effort.
            }
        }
    }
}

/// <summary>
/// Verificador do marcador controlado pelo teste.
/// </summary>
/// <param name="acaoLeitura">Leitura simulada.</param>
public sealed class VerificadorIdentidadeBancoFake(Func<Task<IdentidadeBancoTenantSnapshot>> acaoLeitura) : IVerificadorIdentidadeBancoTenant
{
    /// <inheritdoc />
    public Task<IdentidadeBancoTenantSnapshot> LerAsync(CancellationToken tokenCancelamento = default) => acaoLeitura();
}
