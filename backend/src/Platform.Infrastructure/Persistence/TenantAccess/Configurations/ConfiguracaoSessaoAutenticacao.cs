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
            tabela.HasComment("Sessões autenticadas persistidas e revogáveis; expiram por inatividade ou teto absoluto, o que ocorrer primeiro.");
            tabela.HasCheckConstraint(
                "ck_sessoes_autenticacao_teto",
                $"\"TetoAbsolutoEm\" - \"CriadoEm\" BETWEEN interval '{OpcoesSessao.TetoMinimoMinutos} minutes' AND interval '{OpcoesSessao.TetoMaximoMinutos} minutes'");
            tabela.HasCheckConstraint(
                "ck_sessoes_autenticacao_janela_inatividade",
                $"\"JanelaInatividadeMinutos\" BETWEEN {OpcoesSessao.InatividadeMinimaMinutos} AND {OpcoesSessao.InatividadeMaximaMinutos} " +
                "AND \"JanelaInatividadeMinutos\" * interval '1 minute' <= \"TetoAbsolutoEm\" - \"CriadoEm\"");
            tabela.HasCheckConstraint(
                "ck_sessoes_autenticacao_ultimo_uso",
                "\"CriadoEm\" <= \"UltimoUsoEm\" AND \"UltimoUsoEm\" < \"TetoAbsolutoEm\"");
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

        construtor.Property(s => s.TetoAbsolutoEm)
            .IsRequired()
            .HasColumnType(MapeamentoAuditoria.TipoInstante)
            .HasComment("CriadoEm + Sessao:TetoAbsolutoMinutos vigente na emissão; imutável e nunca estendido pelo uso.");

        construtor.Property(s => s.JanelaInatividadeMinutos)
            .IsRequired()
            .HasComment("Sessao:InatividadeMinutos vigente na emissão (5–240, não maior que o teto); imutável.");

        construtor.Property(s => s.UltimoUsoEm)
            .IsRequired()
            .HasColumnType(MapeamentoAuditoria.TipoInstante)
            .HasComment("Instante UTC do último uso registrado (igual a CriadoEm na emissão; no máximo 1 escrita/minuto); só cresce.");

        construtor.Property(s => s.RevogadaEm)
            .HasColumnType(MapeamentoAuditoria.TipoInstante)
            .HasComment("Instante UTC da revogação (igual a AlteradoEm); nulo enquanto não revogada.");

        construtor.Property(s => s.Versao)
            .IsRequired()
            .IsConcurrencyToken()
            .HasComment("Token de concorrência positivo, incrementado a cada alteração efetiva.");

        MapeamentoAuditoria.Mapear(construtor, "sessoes_autenticacao");

        construtor.HasIndex(s => s.FkIdUsuario);
        construtor.HasIndex(s => s.UltimoUsoEm);
        construtor.HasIndex(s => s.TetoAbsolutoEm);
        construtor.HasIndex(s => s.RevogadaEm);

        construtor.HasOne(s => s.Usuario)
            .WithMany()
            .HasForeignKey(s => s.FkIdUsuario)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
