namespace Platform.Infrastructure.Persistence.TenantAccess.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Application.Configuration;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Mapeamento de <c>access.sessoes_autenticacao</c>.
/// </summary>
public sealed class ConfiguracaoSessaoAutenticacao : IEntityTypeConfiguration<SessaoAutenticacao>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SessaoAutenticacao> construtor)
    {
        construtor.ToTable("sessoes_autenticacao", "access", tabela =>
        {
            tabela.HasComment("Sessões autenticadas persistidas e revogáveis, com expiração absoluta fixada na emissão.");
            tabela.HasCheckConstraint(
                "ck_sessoes_autenticacao_prazo",
                $"\"ExpiraEm\" - \"CriadoEm\" BETWEEN interval '{OpcoesSessao.PrazoMinimoMinutos} minutes' AND interval '{OpcoesSessao.PrazoMaximoMinutos} minutes'");
            tabela.HasCheckConstraint(
                "ck_sessoes_autenticacao_revogada_sem_ticket",
                "\"RevogadaEm\" IS NULL OR \"TicketProtegido\" IS NULL");
            tabela.HasCheckConstraint("ck_sessoes_autenticacao_versao", "\"Versao\" > 0");
            tabela.HasCheckConstraint(
                "ck_sessoes_autenticacao_uuids",
                "\"IdSessao\" <> '00000000-0000-0000-0000-000000000000' AND \"EpocaAcesso\" <> '00000000-0000-0000-0000-000000000000'");
        });

        construtor.HasKey(s => s.IdSessao);
        construtor.Property(s => s.IdSessao)
            .ValueGeneratedNever()
            .HasComment("UUID v7 opaco da sessão, gerado pelo servidor.");

        construtor.Property(s => s.FkIdUsuario)
            .IsRequired()
            .HasComment("FK para a conta dona da sessão (exclusão restrita).");

        construtor.Property(s => s.EpocaAcesso)
            .IsRequired()
            .HasComment("Cópia da EpocaAcesso vigente na emissão.");

        construtor.Property(s => s.TicketProtegido)
            .HasComment("Ticket nativo protegido por Data Protection; removido na revogação.");

        construtor.Property(s => s.ExpiraEm)
            .IsRequired()
            .HasColumnType(MapeamentoAuditoria.TipoInstante)
            .HasComment("CriadoEm + prazo configurado na emissão; imutável.");

        construtor.Property(s => s.RevogadaEm)
            .HasColumnType(MapeamentoAuditoria.TipoInstante)
            .HasComment("Instante UTC da revogação (igual a AlteradoEm); nulo enquanto não revogada.");

        construtor.Property(s => s.Versao)
            .IsRequired()
            .IsConcurrencyToken()
            .HasComment("Token de concorrência positivo, incrementado a cada alteração efetiva.");

        MapeamentoAuditoria.Mapear(construtor, "sessoes_autenticacao");

        construtor.HasIndex(s => s.FkIdUsuario);
        construtor.HasIndex(s => s.ExpiraEm);
        construtor.HasIndex(s => s.RevogadaEm);

        construtor.HasOne(s => s.Usuario)
            .WithMany()
            .HasForeignKey(s => s.FkIdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
