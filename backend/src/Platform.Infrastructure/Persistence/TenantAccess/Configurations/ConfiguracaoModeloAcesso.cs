namespace Platform.Infrastructure.Persistence.TenantAccess.Configurations;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Access;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Mapeamento do marcador singleton <c>foundation.identidade_banco</c>.
/// </summary>
public sealed class ConfiguracaoIdentidadeBancoTenant : IEntityTypeConfiguration<IdentidadeBancoTenant>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<IdentidadeBancoTenant> construtor)
    {
        construtor.ToTable("identidade_banco", "foundation", tabela =>
        {
            tabela.HasComment("Identidade singleton do banco da instalação, conferida antes de qualquer acesso a contas e sessões.");
            tabela.HasCheckConstraint("ck_identidade_banco_id_unico", $"\"IdUnico\" = {IdentidadeBancoTenant.IdUnicoPadrao}");
        });

        construtor.HasKey(e => e.IdUnico);
        construtor.Property(e => e.IdUnico)
            .ValueGeneratedNever()
            .HasComment("Chave singleton fixa (IdUnicoPadrao = 1).");

        construtor.Property(e => e.IdTenant)
            .IsRequired()
            .HasComment("UUID imutável do tenant dono deste banco; chave alternativa referenciada pelas contas.");

        construtor.Property(e => e.IdAmbiente)
            .IsRequired()
            .HasComment("UUID imutável do ambiente da instalação.");

        construtor.Property(e => e.EpocaAcesso)
            .IsRequired()
            .HasComment("Geração local de sessões; sessões de outra época são rejeitadas.");

        construtor.Property(e => e.InicializadoEm)
            .IsRequired()
            .HasColumnType(MapeamentoAuditoria.TipoInstante)
            .HasComment("Instante UTC da inicialização técnica, do TimeProvider do tooling.");

        construtor.HasAlternateKey(e => e.IdTenant);
    }
}

/// <summary>
/// Mapeamento de <c>access.usuarios</c> (conta Identity local com auditoria).
/// </summary>
public sealed class ConfiguracaoUsuarioAplicacao : IEntityTypeConfiguration<UsuarioAplicacao>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UsuarioAplicacao> construtor)
    {
        construtor.ToTable("usuarios", "access", tabela =>
        {
            tabela.HasComment("Contas locais da aplicação, exclusivas do tenant desta instalação.");
            tabela.HasCheckConstraint("ck_usuarios_nome_exibicao", "char_length(\"NomeExibicao\") BETWEEN 1 AND 120");
        });

        construtor.HasKey(u => u.Id);
        construtor.Property(u => u.Id)
            .HasColumnName("IdUsuario")
            .ValueGeneratedNever()
            .HasComment("UUID v7 da conta, gerado pelo servidor.");

        construtor.Property(u => u.FkIdTenant)
            .IsRequired()
            .HasComment("FK para identidade_banco.IdTenant: tenant dono da conta.");

        construtor.Property(u => u.NomeExibicao)
            .IsRequired()
            .HasMaxLength(120)
            .HasComment("Nome de exibição (1 a 120 caracteres); dado pessoal.");

        construtor.Property(u => u.EstaAtivo)
            .IsRequired()
            .HasComment("Conta habilitada; false nega login e operações protegidas.");

        construtor.Property(u => u.UserName)
            .HasMaxLength(254)
            .HasComment("Nome de usuário Identity, derivado do email.");

        construtor.Property(u => u.NormalizedUserName)
            .HasMaxLength(254)
            .HasComment("Nome de usuário normalizado pelo Identity.");

        construtor.Property(u => u.Email)
            .HasMaxLength(254)
            .HasComment("Email de login (até 254); dado pessoal.");

        construtor.Property(u => u.NormalizedEmail)
            .HasMaxLength(254)
            .HasComment("Email normalizado; único no banco da instalação.");

        construtor.Property(u => u.EmailConfirmed)
            .HasComment("Confirmação de email do Identity; não exigida na L2.");

        construtor.Property(u => u.PasswordHash)
            .HasComment("Hash nativo do Identity; nunca senha pura.");

        construtor.Property(u => u.SecurityStamp)
            .HasComment("Carimbo de segurança vigente; mudança invalida sessões.");

        construtor.Property(u => u.ConcurrencyStamp)
            .HasComment("Token de concorrência da conta, renovado pelo Identity a cada atualização.");

        construtor.Property(u => u.PhoneNumber)
            .HasComment("Telefone do Identity; sem uso na L2.");

        construtor.Property(u => u.PhoneNumberConfirmed)
            .HasComment("Confirmação de telefone do Identity; sem uso na L2.");

        construtor.Property(u => u.TwoFactorEnabled)
            .HasComment("2FA do Identity; sem uso na L2.");

        construtor.Property(u => u.LockoutEnd)
            .HasColumnType(MapeamentoAuditoria.TipoInstante)
            .HasComment("Fim UTC do bloqueio por tentativas, gravado a partir do TimeProvider.");

        construtor.Property(u => u.LockoutEnabled)
            .HasComment("Lockout habilitado em toda conta desde a criação.");

        construtor.Property(u => u.AccessFailedCount)
            .HasComment("Falhas de senha consecutivas desde o último sucesso ou bloqueio.");

        MapeamentoAuditoria.Mapear(construtor, "usuarios");

        construtor.HasIndex(u => u.NormalizedEmail).IsUnique();
        construtor.HasIndex(u => u.FkIdTenant);

        construtor.HasOne<IdentidadeBancoTenant>()
            .WithMany()
            .HasForeignKey(u => u.FkIdTenant)
            .HasPrincipalKey(t => t.IdTenant)
            .OnDelete(DeleteBehavior.Restrict);

        construtor.HasMany(u => u.Permissoes)
            .WithOne(p => p.Usuario)
            .HasForeignKey(p => p.FkIdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>
/// Mapeamento de <c>access.permissoes_usuario</c> (concessão imutável).
/// </summary>
public sealed class ConfiguracaoPermissaoUsuario : IEntityTypeConfiguration<PermissaoUsuario>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PermissaoUsuario> construtor)
    {
        construtor.ToTable("permissoes_usuario", "access", tabela =>
        {
            tabela.HasComment("Concessões imutáveis de permissões do catálogo às contas locais.");
            tabela.HasCheckConstraint(
                "ck_permissoes_usuario_codigo_permissao",
                $"\"CodigoPermissao\" ~ '{CodigosPermissao.PadraoFormato}' AND char_length(\"CodigoPermissao\") <= 128");
            tabela.HasCheckConstraint(
                "ck_permissoes_usuario_imutavel",
                "\"AlteradoEm\" IS NULL AND \"TipoAutorAlteracao\" IS NULL AND \"AutorAlteracao\" IS NULL AND \"IdOperacaoAlteracao\" IS NULL");
        });

        construtor.HasKey(p => new { p.FkIdUsuario, p.CodigoPermissao });

        construtor.Property(p => p.FkIdUsuario)
            .HasComment("FK para a conta que recebeu a concessão.");

        construtor.Property(p => p.CodigoPermissao)
            .IsRequired()
            .HasMaxLength(128)
            .HasComment("Código do catálogo CodigosPermissao (exceção registrada: compõe a PK).");

        MapeamentoAuditoria.Mapear(construtor, "permissoes_usuario");
    }
}

/// <summary>
/// Mapeamento das tabelas auxiliares do Identity, mantidas vazias e sem uso na L2
/// (exceção registrada em data-model §3).
/// </summary>
public static class ConfiguracaoTabelasAuxiliaresIdentity
{
    /// <summary>
    /// Aplica nomes de tabela, colunas e comentários às tabelas auxiliares.
    /// </summary>
    /// <param name="construtor">Construtor do modelo.</param>
    public static void Aplicar(ModelBuilder construtor)
    {
        ArgumentNullException.ThrowIfNull(construtor);

        construtor.Entity<IdentityUserClaim<Guid>>(b =>
        {
            b.ToTable("reivindicacoes_usuario", "access", t => t.HasComment("Tabela auxiliar do Identity (claims); vazia e sem uso na L2."));
            b.Property(e => e.Id).HasColumnName("IdReivindicacaoUsuario").HasComment("PK inteira imposta pelo Identity.");
            b.Property(e => e.UserId).HasColumnName("FkIdUsuario").HasComment("FK para a conta.");
            b.Property(e => e.ClaimType).HasComment("Tipo da claim.");
            b.Property(e => e.ClaimValue).HasComment("Valor da claim.");
        });

        construtor.Entity<IdentityUserLogin<Guid>>(b =>
        {
            b.ToTable("logins_usuario", "access", t => t.HasComment("Tabela auxiliar do Identity (logins externos); vazia e sem uso na L2."));
            b.Property(e => e.LoginProvider).HasComment("Provedor externo (parte da PK).");
            b.Property(e => e.ProviderKey).HasComment("Chave no provedor externo (parte da PK).");
            b.Property(e => e.ProviderDisplayName).HasComment("Nome de exibição do provedor.");
            b.Property(e => e.UserId).HasColumnName("FkIdUsuario").HasComment("FK para a conta.");
        });

        construtor.Entity<IdentityUserToken<Guid>>(b =>
        {
            b.ToTable("tokens_usuario", "access", t => t.HasComment("Tabela auxiliar do Identity (tokens); vazia e sem uso na L2."));
            b.Property(e => e.UserId).HasColumnName("FkIdUsuario").HasComment("FK para a conta (parte da PK).");
            b.Property(e => e.LoginProvider).HasComment("Provedor do token (parte da PK).");
            b.Property(e => e.Name).HasComment("Nome do token (parte da PK).");
            b.Property(e => e.Value).HasComment("Valor do token.");
        });
    }
}
