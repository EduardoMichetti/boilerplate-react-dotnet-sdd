namespace Platform.Infrastructure.Persistence.TenantAccess;

using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence.TenantAccess.Configurations;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// DbContext do acesso local da instalação: marcador, contas, concessões, sessões e
/// histórico operacional. O <c>InterceptadorAuditoria</c> é registrado pela composição
/// (API, host operacional e factory de design).
/// </summary>
public sealed class AcessoTenantDbContext(DbContextOptions<AcessoTenantDbContext> opcoes)
    : IdentityUserContext<UsuarioAplicacao, Guid>(opcoes)
{
    /// <summary>
    /// Concessões de permissões às contas.
    /// </summary>
    public DbSet<PermissaoUsuario> Permissoes => Set<PermissaoUsuario>();

    /// <summary>
    /// Marcador singleton de identidade do banco.
    /// </summary>
    public DbSet<IdentidadeBancoTenant> IdentidadeBanco => Set<IdentidadeBancoTenant>();

    /// <summary>
    /// Sessões autenticadas persistidas.
    /// </summary>
    public DbSet<SessaoAutenticacao> Sessoes => Set<SessaoAutenticacao>();

    /// <summary>
    /// Histórico append-only das operações restritas.
    /// </summary>
    public DbSet<RegistroOperacaoAcesso> OperacoesAcesso => Set<RegistroOperacaoAcesso>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfiguration(new ConfiguracaoIdentidadeBancoTenant());
        builder.ApplyConfiguration(new ConfiguracaoUsuarioAplicacao());
        builder.ApplyConfiguration(new ConfiguracaoPermissaoUsuario());
        builder.ApplyConfiguration(new ConfiguracaoSessaoAutenticacao());
        builder.ApplyConfiguration(new ConfiguracaoRegistroOperacaoAcesso());
        ConfiguracaoTabelasAuxiliaresIdentity.Aplicar(builder);

        // Sempre por último: nomes de constraints/índices dependem do modelo completo.
        ConvencaoNomesBanco.Aplicar(builder);
    }
}
