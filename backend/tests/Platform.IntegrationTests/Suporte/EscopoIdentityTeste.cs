namespace Platform.IntegrationTests.Suporte;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Auditoria;
using Platform.Infrastructure.Persistence.Auditoria;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Provedor de serviços mínimo com Identity e o interceptor de auditoria sobre uma conexão
/// de identidade específica (owner, runtime ou operador).
/// </summary>
public sealed class EscopoIdentityTeste : IAsyncDisposable
{
    private readonly ServiceProvider _provedor;

    private EscopoIdentityTeste(ServiceProvider provedor)
    {
        _provedor = provedor;
        GerenciadorUsuarios = provedor.GetRequiredService<UserManager<UsuarioAplicacao>>();
        Contexto = provedor.GetRequiredService<AcessoTenantDbContext>();
    }

    /// <summary>
    /// Gerenciador de usuários do escopo.
    /// </summary>
    public UserManager<UsuarioAplicacao> GerenciadorUsuarios { get; }

    /// <summary>
    /// Contexto EF do escopo (o mesmo usado pelo Identity).
    /// </summary>
    public AcessoTenantDbContext Contexto { get; }

    /// <summary>
    /// Cria o escopo.
    /// </summary>
    /// <param name="stringConexao">Conexão da identidade desejada.</param>
    /// <param name="contextoOperacao">Contexto de operação que alimenta a auditoria.</param>
    /// <returns>Escopo pronto.</returns>
    public static EscopoIdentityTeste Criar(string stringConexao, IContextoOperacao contextoOperacao)
    {
        ServiceCollection servicos = new();
        servicos.AddLogging();
        servicos.AddSingleton(contextoOperacao);
        servicos.AddDbContext<AcessoTenantDbContext>(opcoes =>
        {
            opcoes.UseNpgsql(stringConexao, npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation"));
            opcoes.AddInterceptors(new InterceptadorAuditoria(contextoOperacao));
        });
        servicos.AddIdentityCore<UsuarioAplicacao>(opcoes => opcoes.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<AcessoTenantDbContext>();

        return new EscopoIdentityTeste(servicos.BuildServiceProvider());
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => _provedor.DisposeAsync();
}
