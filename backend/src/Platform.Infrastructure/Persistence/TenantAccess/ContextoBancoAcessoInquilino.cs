namespace Platform.Infrastructure.Persistence.TenantAccess;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Platform.Infrastructure.Persistence.TenantAccess.Configurations;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Contexto de banco de dados do Entity Framework Core para o módulo de acesso do inquilino.
/// Integra a identidade do banco e os usuários com permissões.
/// </summary>
public sealed class ContextoBancoAcessoInquilino(DbContextOptions<ContextoBancoAcessoInquilino> opcoes)
    : IdentityUserContext<UsuarioAplicacao, Guid>(opcoes)
{
    /// <summary>
    /// Conjunto de permissões concedidas aos usuários da instalação.
    /// </summary>
    public DbSet<PermissaoUsuario> Permissoes => Set<PermissaoUsuario>();

    /// <summary>
    /// Conjunto singleton que armazena a identidade do banco de dados do inquilino.
    /// </summary>
    public DbSet<IdentidadeBancoInquilino> IdentidadeBanco => Set<IdentidadeBancoInquilino>();

    protected override void OnModelCreating(ModelBuilder construtor)
    {
        base.OnModelCreating(construtor);

        construtor.ApplyConfiguration(new ConfiguracaoUsuarioAplicacao());
        construtor.ApplyConfiguration(new ConfiguracaoPermissaoUsuario());

        construtor.Entity<IdentityUserClaim<Guid>>(b => b.ToTable("reivindicacoes_usuario", "access"));
        construtor.Entity<IdentityUserLogin<Guid>>(b => b.ToTable("logins_usuario", "access"));
        construtor.Entity<IdentityUserToken<Guid>>(b => b.ToTable("tokens_usuario", "access"));

        construtor.Entity<IdentidadeBancoInquilino>(b =>
        {
            b.ToTable("identidade_banco", "foundation", tabela =>
                tabela.HasComment("Armazena a identidade singleton do banco de dados para comprovação e isolamento por inquilino."));

            b.HasKey(e => e.IdUnico);
            b.Property(e => e.IdUnico)
                .ValueGeneratedNever()
                .HasComment("Identificador singleton fixado em 1 para garantir linha única.");

            b.Property(e => e.IdInquilino)
                .IsRequired()
                .HasComment("Identificador imutável do inquilino proprietário deste banco de dados.");

            b.Property(e => e.IdAmbiente)
                .IsRequired()
                .HasComment("Identificador único do ambiente de execução correspondente a esta instalação.");

            b.Property(e => e.EpocaAcesso)
                .IsRequired()
                .HasComment("Época de acesso atual utilizada para revogação e invalidação global de sessões.");

            b.Property(e => e.InicializadoEm)
                .IsRequired()
                .HasColumnType("timestamp with time zone")
                .HasComment("Data e hora UTC em que o banco de dados da instalação foi inicializado tecnicamente.");

            b.HasAlternateKey(e => e.IdInquilino)
                .HasName("uq_identidade_banco_id_inquilino");
        });

        construtor.Entity<UsuarioAplicacao>(b =>
        {
            b.HasOne<IdentidadeBancoInquilino>()
                .WithMany()
                .HasForeignKey(u => u.IdInquilino)
                .HasPrincipalKey(t => t.IdInquilino)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
