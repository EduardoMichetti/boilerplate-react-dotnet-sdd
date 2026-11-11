namespace Platform.IntegrationTests.Suporte;

using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Access.Operacoes;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Auditoria;
using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Platform.Infrastructure;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.IntegrationTests.Fixtures;
using Platform.Operations;
using Platform.Testes.Comum;

/// <summary>
/// Provedor sintético de senha para testes de operações (sem console interativo).
/// </summary>
public sealed class ProvedorSenhaSintetico(string senha) : IProvedorSenha
{
    public Task<string> ObterSenhaComConfirmacaoAsync(CancellationToken tokenCancelamento = default) =>
        Task.FromResult(senha);
}

/// <summary>
/// Escopo de DI com serviços de infraestrutura e casos de uso de operações restritas (FR-033).
/// </summary>
public sealed class EscopoOperacoesTeste : IAsyncDisposable
{
    private readonly ServiceProvider _provedor;
    private readonly IServiceScope _escopo;

    private EscopoOperacoesTeste(ServiceProvider provedor, IServiceScope escopo)
    {
        _provedor = provedor;
        _escopo = escopo;
    }

    /// <summary>
    /// Provedor de serviços com escopo ativo.
    /// </summary>
    public IServiceProvider Servicos => _escopo.ServiceProvider;

    /// <summary>
    /// DbContext da instalação no escopo.
    /// </summary>
    public AcessoTenantDbContext Contexto => Servicos.GetRequiredService<AcessoTenantDbContext>();

    /// <summary>
    /// Cria o escopo para testes de operações restritas.
    /// </summary>
    public static EscopoOperacoesTeste Criar(
        InstalacaoTestePostgres tenant,
        string stringConexao,
        string loginAutorizado,
        RelogioControlavel relogio,
        string senhaSintetica = "SenhaSegura123!",
        string? retencaoDiasBruto = "30",
        Guid? overrideIdTenant = null,
        Guid? overrideIdAmbiente = null)
    {
        ServiceCollection servicos = new();
        servicos.AddLogging();
        servicos.AddMemoryCache();
        servicos.AddSingleton<TimeProvider>(relogio);
        servicos.AddSingleton<IProvedorSenha>(new ProvedorSenhaSintetico(senhaSintetica));
        servicos.AddScoped<IContextoOperacao>(_ => new ContextoOperacaoOperador(relogio, loginAutorizado));

        servicos.Configure<OpcoesInstalacaoTenant>(opcoes =>
        {
            opcoes.CodigoAplicacao = "app-teste";
            opcoes.IdTenant = overrideIdTenant ?? tenant.IdTenant;
            opcoes.IdAmbiente = overrideIdAmbiente ?? tenant.IdAmbiente;
            opcoes.NomeExibicaoTenant = "Tenant Teste";
            opcoes.EstadoTenant = EstadoTenant.Ativo;
            opcoes.StringsConexao = new StringsConexaoInstalacao
            {
                BancoApp = stringConexao,
            };
        });

        servicos.Configure<OpcoesOperacaoAcesso>(opcoes =>
        {
            opcoes.LoginAutorizado = loginAutorizado;
            opcoes.RetencaoSessoesDiasBruto = retencaoDiasBruto;
        });

        servicos.AdicionarInfraestruturaAcesso();
        servicos.AdicionarCasosUsoOperacoes();

        ServiceProvider provedor = servicos.BuildServiceProvider();
        IServiceScope escopo = provedor.CreateScope();
        return new EscopoOperacoesTeste(provedor, escopo);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        _escopo.Dispose();
        await _provedor.DisposeAsync();
    }
}
