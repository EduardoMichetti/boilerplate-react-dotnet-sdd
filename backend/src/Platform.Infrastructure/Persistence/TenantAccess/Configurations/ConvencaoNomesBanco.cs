namespace Platform.Infrastructure.Persistence.TenantAccess.Configurations;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// Convenção central de nomes de constraints e índices (data-model §2, Constituição IX):
/// <c>pk_&lt;tabela&gt;</c>, <c>fk_&lt;tabela&gt;_&lt;tabela_ref&gt;[_&lt;papel&gt;]</c>,
/// <c>ix_&lt;tabela&gt;_&lt;colunas&gt;</c>, <c>uq_&lt;tabela&gt;_&lt;colunas&gt;</c> e
/// <c>ck_&lt;tabela&gt;_&lt;regra&gt;</c>, em minúsculas, colunas em snake_case e até 63 caracteres.
/// Aplicada por último no <c>OnModelCreating</c>.
/// </summary>
public static partial class ConvencaoNomesBanco
{
    /// <summary>
    /// Limite de identificadores do PostgreSQL (que trunca sem erro).
    /// </summary>
    public const int TamanhoMaximoNome = 63;

    /// <summary>
    /// Renomeia PKs, chaves alternativas, FKs e índices e valida os CHECKs de todas as tabelas do modelo.
    /// </summary>
    /// <param name="construtor">Construtor do modelo.</param>
    /// <exception cref="InvalidOperationException">Nome gerado acima de 63 caracteres ou CHECK fora do padrão.</exception>
    public static void Aplicar(ModelBuilder construtor)
    {
        ArgumentNullException.ThrowIfNull(construtor);

        foreach (IMutableEntityType entidade in construtor.Model.GetEntityTypes())
        {
            string? tabela = entidade.GetTableName();
            if (tabela is null)
            {
                continue;
            }

            foreach (IMutableKey chave in entidade.GetKeys())
            {
                string nome = chave.IsPrimaryKey()
                    ? $"pk_{tabela}"
                    : $"uq_{tabela}_{Colunas(chave.Properties)}";
                chave.SetName(Verificar(nome));
            }

            List<IMutableForeignKey> chavesEstrangeiras = entidade.GetForeignKeys().ToList();
            foreach (IMutableForeignKey fk in chavesEstrangeiras)
            {
                string tabelaReferenciada = fk.PrincipalEntityType.GetTableName()
                    ?? throw new InvalidOperationException($"FK de {tabela} referencia entidade sem tabela.");
                bool exigePapel = chavesEstrangeiras.Count(o => o.PrincipalEntityType.GetTableName() == tabelaReferenciada) > 1;
                string nome = exigePapel
                    ? $"fk_{tabela}_{tabelaReferenciada}_{Colunas(fk.Properties)}"
                    : $"fk_{tabela}_{tabelaReferenciada}";
                fk.SetConstraintName(Verificar(nome));
            }

            foreach (IMutableIndex indice in entidade.GetIndexes())
            {
                string prefixo = indice.IsUnique ? "uq" : "ix";
                indice.SetDatabaseName(Verificar($"{prefixo}_{tabela}_{Colunas(indice.Properties)}"));
            }

            foreach (IMutableCheckConstraint restricao in entidade.GetCheckConstraints())
            {
                string nome = restricao.Name ?? restricao.ModelName;
                if (!nome.StartsWith($"ck_{tabela}_", StringComparison.Ordinal) || nome != nome.ToLowerInvariant())
                {
                    throw new InvalidOperationException($"CHECK '{nome}' deve seguir o padrão ck_{tabela}_<regra> em minúsculas.");
                }

                Verificar(nome);
            }
        }
    }

    /// <summary>
    /// Converte um nome PascalCase em snake_case minúsculo (ex.: <c>FkIdUsuario</c> → <c>fk_id_usuario</c>).
    /// </summary>
    /// <param name="nome">Nome PascalCase.</param>
    /// <returns>Nome em snake_case.</returns>
    public static string ParaSnakeCase(string nome)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        string comSeparadores = MaiusculaAposMinuscula().Replace(nome, "$1_$2");
        comSeparadores = SiglaSeguidaDePalavra().Replace(comSeparadores, "$1_$2");
        return comSeparadores.ToLowerInvariant();
    }

    private static string Colunas(IEnumerable<IReadOnlyProperty> propriedades) =>
        string.Join("_", propriedades.Select(p => ParaSnakeCase(p.GetColumnName())));

    private static string Verificar(string nome)
    {
        if (nome.Length > TamanhoMaximoNome)
        {
            throw new InvalidOperationException($"Nome de constraint/índice '{nome}' excede {TamanhoMaximoNome} caracteres.");
        }

        return nome;
    }

    [GeneratedRegex("([a-z0-9])([A-Z])", RegexOptions.CultureInvariant)]
    private static partial Regex MaiusculaAposMinuscula();

    [GeneratedRegex("([A-Z])([A-Z][a-z])", RegexOptions.CultureInvariant)]
    private static partial Regex SiglaSeguidaDePalavra();
}
