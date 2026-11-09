namespace Platform.Infrastructure.Persistence.TenantAccess.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Access;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Mapeamento de <c>access.operacoes_acesso</c> (histórico append-only).
/// </summary>
public sealed class ConfiguracaoRegistroOperacaoAcesso : IEntityTypeConfiguration<RegistroOperacaoAcesso>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RegistroOperacaoAcesso> construtor)
    {
        construtor.ToTable("operacoes_acesso", "access", tabela =>
        {
            tabela.HasComment("Histórico append-only das operações restritas de contas, permissões e sessões (FR-033).");
            tabela.HasCheckConstraint(
                "ck_operacoes_acesso_tipo_operacao",
                $"\"TipoOperacao\" IN ({MapeamentoAuditoria.ValoresEnum<TipoOperacaoAcesso>()})");
            tabela.HasCheckConstraint(
                "ck_operacoes_acesso_resultado",
                $"\"Resultado\" IN ({MapeamentoAuditoria.ValoresEnum<ResultadoOperacaoAcesso>()})");
            tabela.HasCheckConstraint(
                "ck_operacoes_acesso_limpeza",
                "(\"TipoOperacao\" = 'LimparSessoes' AND \"RetencaoDias\" IS NOT NULL AND \"InstanteCorte\" IS NOT NULL " +
                "AND \"QuantidadeRemovida\" IS NOT NULL AND \"IdUsuarioAlvo\" IS NULL AND \"CodigoPermissao\" IS NULL) OR " +
                "(\"TipoOperacao\" <> 'LimparSessoes' AND \"RetencaoDias\" IS NULL AND \"InstanteCorte\" IS NULL AND \"QuantidadeRemovida\" IS NULL)");
            tabela.HasCheckConstraint(
                "ck_operacoes_acesso_valores_limpeza",
                "(\"RetencaoDias\" IS NULL OR \"RetencaoDias\" BETWEEN 1 AND 365) AND (\"QuantidadeRemovida\" IS NULL OR \"QuantidadeRemovida\" >= 0)");
            tabela.HasCheckConstraint(
                "ck_operacoes_acesso_login_operador",
                "char_length(\"LoginOperador\") BETWEEN 1 AND 128");
        });

        construtor.HasKey(r => r.IdOperacao);
        construtor.Property(r => r.IdOperacao)
            .ValueGeneratedNever()
            .HasComment("UUID v7 do evento e correlação do comando.");

        construtor.Property(r => r.IdTenant)
            .IsRequired()
            .HasComment("Tenant do alvo validado (referência sem constraint).");

        construtor.Property(r => r.IdAmbiente)
            .IsRequired()
            .HasComment("Ambiente do alvo validado (referência sem constraint).");

        construtor.Property(r => r.IdUsuarioAlvo)
            .HasComment("Conta alvo (referência sem FK, preservada após evolução da conta).");

        construtor.Property(r => r.LoginOperador)
            .IsRequired()
            .HasMaxLength(128)
            .HasComment("Login PostgreSQL individual autenticado do operador.");

        MapeamentoAuditoria.MapearEnumTexto(construtor.Property(r => r.TipoOperacao), "Tipo da operação restrita.")
            .IsRequired();

        construtor.Property(r => r.CodigoPermissao)
            .HasMaxLength(128)
            .HasComment("Código de permissão, quando relevante.");

        construtor.Property(r => r.RegistradaEm)
            .IsRequired()
            .HasColumnType(MapeamentoAuditoria.TipoInstante)
            .HasComment("Instante UTC do registro, igual ao InstanteOperacao do comando.");

        MapeamentoAuditoria.MapearEnumTexto(construtor.Property(r => r.Resultado), "Resultado da operação (Sucesso, Negado, Falha).")
            .IsRequired();

        construtor.Property(r => r.CodigoResultado)
            .IsRequired()
            .HasMaxLength(100)
            .HasComment("Código estável e seguro do resultado, sem mensagem SQL.");

        construtor.Property(r => r.RetencaoDias)
            .HasComment("Somente LimparSessoes: retenção aplicada, em dias (1–365).");

        construtor.Property(r => r.InstanteCorte)
            .HasColumnType(MapeamentoAuditoria.TipoInstante)
            .HasComment("Somente LimparSessoes: RegistradaEm - RetencaoDias.");

        construtor.Property(r => r.QuantidadeRemovida)
            .HasComment("Somente LimparSessoes: sessões removidas (0 quando nada elegível ou falha).");

        construtor.HasIndex(r => new { r.IdUsuarioAlvo, r.RegistradaEm });
        construtor.HasIndex(r => r.RegistradaEm);
    }
}
