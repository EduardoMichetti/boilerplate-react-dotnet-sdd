namespace Platform.Infrastructure.Persistence.TenantAccess.Configurations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Platform.Domain.Auditoria;

/// <summary>
/// Mapeamento compartilhado dos metadados de auditoria (data-model §4) e de enums persistidos
/// como texto com CHECK (G7), aplicado igualmente às três entidades auditáveis.
/// </summary>
internal static class MapeamentoAuditoria
{
    /// <summary>
    /// Tipo de coluna dos instantes (UTC, microssegundos, sem default de banco).
    /// </summary>
    public const string TipoInstante = "timestamp with time zone";

    /// <summary>
    /// Mapeia colunas, comentários e CHECKs de auditoria de criação e alteração.
    /// </summary>
    /// <typeparam name="T">Entidade auditável.</typeparam>
    /// <param name="construtor">Construtor da entidade.</param>
    /// <param name="tabela">Nome da tabela (sem schema), usado nos nomes dos CHECKs.</param>
    public static void Mapear<T>(EntityTypeBuilder<T> construtor, string tabela)
        where T : class, IEntidadeAuditavel
    {
        construtor.Property(e => e.CriadoEm)
            .IsRequired()
            .HasColumnType(TipoInstante)
            .HasComment("Instante UTC da criação (microssegundos), do TimeProvider da operação.");

        MapearEnumTexto(construtor.Property(e => e.TipoAutorCriacao), "Tipo do autor da criação (Operador, Usuario, Sistema, Anonimo).")
            .IsRequired();

        construtor.Property(e => e.AutorCriacao)
            .HasMaxLength(256)
            .HasComment("Referência textual do autor da criação; nula somente para Anonimo.");

        construtor.Property(e => e.IdOperacaoCriacao)
            .IsRequired()
            .HasComment("UUID de correlação da operação que criou o registro.");

        construtor.Property(e => e.AlteradoEm)
            .HasColumnType(TipoInstante)
            .HasComment("Instante UTC da última alteração efetiva; nulo se nunca alterado.");

        MapearEnumTexto(construtor.Property(e => e.TipoAutorAlteracao), "Tipo do autor da última alteração; nulo se nunca alterado.");

        construtor.Property(e => e.AutorAlteracao)
            .HasMaxLength(256)
            .HasComment("Referência textual do autor da última alteração; nula para Anonimo ou sem alteração.");

        construtor.Property(e => e.IdOperacaoAlteracao)
            .HasComment("UUID de correlação da última alteração; obrigatório quando AlteradoEm está preenchido.");

        string tipos = ValoresEnum<TipoAutor>();
        construtor.ToTable(t =>
        {
            t.HasCheckConstraint($"ck_{tabela}_tipo_autor_criacao", $"\"TipoAutorCriacao\" IN ({tipos})");
            t.HasCheckConstraint($"ck_{tabela}_tipo_autor_alteracao", $"\"TipoAutorAlteracao\" IS NULL OR \"TipoAutorAlteracao\" IN ({tipos})");
            t.HasCheckConstraint(
                $"ck_{tabela}_auditoria_criacao",
                "(\"TipoAutorCriacao\" = 'Anonimo') = (\"AutorCriacao\" IS NULL)");
            t.HasCheckConstraint(
                $"ck_{tabela}_auditoria_alteracao",
                "(\"AlteradoEm\" IS NULL AND \"TipoAutorAlteracao\" IS NULL AND \"AutorAlteracao\" IS NULL AND \"IdOperacaoAlteracao\" IS NULL) OR " +
                "(\"AlteradoEm\" IS NOT NULL AND \"TipoAutorAlteracao\" IS NOT NULL AND \"IdOperacaoAlteracao\" IS NOT NULL " +
                "AND (\"TipoAutorAlteracao\" = 'Anonimo') = (\"AutorAlteracao\" IS NULL))");
        });
    }

    /// <summary>
    /// Persiste um enum como texto (nome do membro) com comentário.
    /// </summary>
    /// <typeparam name="TEnum">Tipo da propriedade (enum ou enum anulável).</typeparam>
    /// <param name="propriedade">Construtor da propriedade.</param>
    /// <param name="comentario">Comentário da coluna.</param>
    /// <returns>O mesmo construtor.</returns>
    public static PropertyBuilder<TEnum> MapearEnumTexto<TEnum>(PropertyBuilder<TEnum> propriedade, string comentario) =>
        propriedade.HasConversion<string>().HasMaxLength(32).HasComment(comentario);

    /// <summary>
    /// Lista SQL dos nomes do enum, para CHECK (ex.: <c>'Sucesso', 'Negado'</c>).
    /// </summary>
    /// <typeparam name="TEnum">Enum.</typeparam>
    /// <returns>Valores citados e separados por vírgula.</returns>
    public static string ValoresEnum<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetNames<TEnum>().Select(n => $"'{n}'"));
}
