namespace Platform.Infrastructure.Persistence.TenantAccess.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Configuração do mapeamento objeto-relacional para a entidade UsuarioAplicacao.
/// </summary>
public sealed class ConfiguracaoUsuarioAplicacao : IEntityTypeConfiguration<UsuarioAplicacao>
{
    public void Configure(EntityTypeBuilder<UsuarioAplicacao> construtor)
    {
        construtor.ToTable("usuarios", "access", tabela =>
            tabela.HasComment("Armazena os usuários da aplicação vinculados exclusivamente ao inquilino desta instalação."));

        construtor.HasKey(u => u.Id);
        construtor.Property(u => u.Id)
            .ValueGeneratedNever()
            .HasComment("Identificador único da conta do usuário (UUID v4).");

        construtor.Property(u => u.IdInquilino)
            .IsRequired()
            .HasComment("Identificador imutável do inquilino (tenant) proprietário desta conta.");

        construtor.Property(u => u.NomeExibicao)
            .IsRequired()
            .HasMaxLength(120)
            .HasComment("Nome amigável de exibição do usuário na aplicação.");

        construtor.Property(u => u.Email)
            .HasMaxLength(254)
            .HasComment("Endereço de e-mail do usuário utilizado para login e notificações.");

        construtor.Property(u => u.NormalizedEmail)
            .HasMaxLength(254)
            .HasComment("Endereço de e-mail normalizado em letras maiúsculas para consultas eficientes.");

        construtor.Property(u => u.UserName)
            .HasMaxLength(254)
            .HasComment("Nome de usuário no sistema para propósitos de autenticação.");

        construtor.Property(u => u.NormalizedUserName)
            .HasMaxLength(254)
            .HasComment("Nome de usuário normalizado para comparações e indexação.");

        construtor.Property(u => u.EstaAtivo)
            .IsRequired()
            .HasDefaultValue(true)
            .HasComment("Indica se o usuário está ativo no sistema e autorizado a acessar.");

        construtor.HasIndex(u => u.NormalizedEmail)
            .IsUnique()
            .HasDatabaseName("ix_usuarios_email_normalizado");

        construtor.HasIndex(u => u.IdInquilino)
            .HasDatabaseName("ix_usuarios_id_inquilino");

        construtor.HasMany(u => u.Permissoes)
            .WithOne(p => p.Usuario)
            .HasForeignKey(p => p.IdUsuario)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>
/// Configuração do mapeamento objeto-relacional para a entidade PermissaoUsuario.
/// </summary>
public sealed class ConfiguracaoPermissaoUsuario : IEntityTypeConfiguration<PermissaoUsuario>
{
    public void Configure(EntityTypeBuilder<PermissaoUsuario> construtor)
    {
        construtor.ToTable("permissoes_usuario", "access", tabela =>
            tabela.HasComment("Armazena as permissões granulares concedidas a cada usuário na instalação."));

        construtor.HasKey(p => new { p.IdUsuario, p.CodigoPermissao });

        construtor.Property(p => p.IdUsuario)
            .HasComment("Identificador do usuário que recebeu a permissão.");

        construtor.Property(p => p.CodigoPermissao)
            .IsRequired()
            .HasMaxLength(128)
            .HasComment("Código textual identificador da permissão concedida no sistema.");

        construtor.Property(p => p.ConcedidaEm)
            .IsRequired()
            .HasColumnType("timestamp with time zone")
            .HasComment("Data e hora UTC em que a permissão foi concedida.");

        construtor.Property(p => p.Versao)
            .IsConcurrencyToken()
            .HasComment("Token de versão para controle de concorrência otimista.");
    }
}
