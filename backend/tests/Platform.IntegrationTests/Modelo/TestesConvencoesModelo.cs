namespace Platform.IntegrationTests.Modelo;

using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Platform.Infrastructure.Persistence.TenantAccess;
using Xunit;

/// <summary>
/// T076: convenções de nomes e identificadores do modelo EF (data-model §2–§3), sem banco.
/// Exceções formam uma lista fechada: tabela nova fora dela que viole a regra reprova.
/// </summary>
public sealed class TestesConvencoesModelo
{
    private static readonly IModel Modelo = CarregarModelo();

    /// <summary>Tabelas cuja PK não é <c>Id&lt;Entidade&gt;</c> UUID (exceções registradas).</summary>
    private static readonly HashSet<string> ExcecoesPk =
    [
        "identidade_banco",       // IdUnico inteiro singleton
        "permissoes_usuario",     // (FkIdUsuario, CodigoPermissao)
        "reivindicacoes_usuario", // int imposto pelo Identity
        "logins_usuario",         // (LoginProvider, ProviderKey)
        "tokens_usuario",         // (FkIdUsuario, LoginProvider, Name)
    ];

    private static readonly Regex NomeRestricao = new("^(pk|fk|ix|uq|ck)_[a-z0-9_]+$", RegexOptions.CultureInvariant);

    [Fact]
    public void ModeloContemAsTabelasEsperadas()
    {
        string[] tabelas = Modelo.GetEntityTypes().Select(e => $"{e.GetSchema()}.{e.GetTableName()}").Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(
            [
                "access.logins_usuario",
                "access.operacoes_acesso",
                "access.permissoes_usuario",
                "access.reivindicacoes_usuario",
                "access.sessoes_autenticacao",
                "access.tokens_usuario",
                "access.usuarios",
                "foundation.identidade_banco",
            ],
            tabelas);
    }

    [Fact]
    public void PksSaoIdEntidadeUuidGeradaPeloServidorOuExcecaoRegistrada()
    {
        foreach (IEntityType entidade in Modelo.GetEntityTypes())
        {
            string tabela = entidade.GetTableName()!;
            IKey pk = entidade.FindPrimaryKey()!;
            Assert.Equal($"pk_{tabela}", pk.GetName());

            if (ExcecoesPk.Contains(tabela))
            {
                continue;
            }

            IProperty coluna = Assert.Single(pk.Properties);
            Assert.StartsWith("Id", coluna.GetColumnName(), StringComparison.Ordinal);
            Assert.False(coluna.GetColumnName().StartsWith("IdUnico", StringComparison.Ordinal));
            Assert.Equal(typeof(Guid), coluna.ClrType);
            Assert.Equal(ValueGenerated.Never, coluna.ValueGenerated);
        }
    }

    [Fact]
    public void TodaPkGuidTemGeracaoDoProviderDesligada()
    {
        IEnumerable<IProperty> pksGuid = Modelo.GetEntityTypes()
            .SelectMany(e => e.FindPrimaryKey()!.Properties)
            .Where(p => p.ClrType == typeof(Guid) && !p.IsForeignKey());

        Assert.All(pksGuid, p => Assert.Equal(ValueGenerated.Never, p.ValueGenerated));
    }

    [Fact]
    public void TodaFkEhFkIdApontandoParaId()
    {
        foreach (IForeignKey fk in Modelo.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
        {
            Assert.All(fk.Properties, p => Assert.StartsWith("FkId", p.GetColumnName(), StringComparison.Ordinal));
            Assert.All(fk.PrincipalKey.Properties, p => Assert.StartsWith("Id", p.GetColumnName(), StringComparison.Ordinal));
        }
    }

    [Fact]
    public void RestricoesEIndicesSeguemPadraoEMaximo63()
    {
        List<string> nomes = [];
        foreach (IEntityType entidade in Modelo.GetEntityTypes())
        {
            nomes.AddRange(entidade.GetKeys().Select(k => k.GetName()!));
            nomes.AddRange(entidade.GetForeignKeys().Select(f => f.GetConstraintName()!));
            nomes.AddRange(entidade.GetIndexes().Select(i => i.GetDatabaseName()!));
            nomes.AddRange(entidade.GetCheckConstraints().Select(c => c.Name!));
        }

        Assert.NotEmpty(nomes);
        Assert.All(nomes, nome =>
        {
            Assert.Matches(NomeRestricao, nome);
            Assert.True(nome.Length <= 63, $"{nome} excede 63 caracteres.");
        });
        Assert.Equal(nomes.Count, nomes.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TodaColunaETabelaTemComentario()
    {
        foreach (IEntityType entidade in Modelo.GetEntityTypes())
        {
            Assert.False(string.IsNullOrWhiteSpace(entidade.GetComment()), $"Tabela {entidade.GetTableName()} sem comentário.");
            foreach (IProperty propriedade in entidade.GetProperties())
            {
                Assert.False(
                    string.IsNullOrWhiteSpace(propriedade.GetComment()),
                    $"Coluna {entidade.GetTableName()}.{propriedade.GetColumnName()} sem HasComment.");
            }
        }
    }

    [Fact]
    public void NenhumaColunaUsaDefaultSqlOuDataComDefault()
    {
        foreach (IProperty propriedade in Modelo.GetEntityTypes().SelectMany(e => e.GetProperties()))
        {
            Assert.Null(propriedade.GetDefaultValueSql());
            if (propriedade.ClrType == typeof(DateTimeOffset) || propriedade.ClrType == typeof(DateTimeOffset?))
            {
                Assert.Null(propriedade.FindAnnotation(RelationalAnnotationNames.DefaultValue));
                Assert.Equal("timestamp with time zone", propriedade.GetColumnType());
            }
        }
    }

    [Fact]
    public void ColunasDoIdentityMapeadasParaOPadrao()
    {
        IEntityType usuarios = Modelo.GetEntityTypes().Single(e => e.GetTableName() == "usuarios");
        Assert.Equal("IdUsuario", usuarios.FindProperty("Id")!.GetColumnName());

        foreach (string auxiliar in new[] { "reivindicacoes_usuario", "logins_usuario", "tokens_usuario" })
        {
            IEntityType entidade = Modelo.GetEntityTypes().Single(e => e.GetTableName() == auxiliar);
            Assert.Equal("FkIdUsuario", entidade.FindProperty("UserId")!.GetColumnName());
        }

        IEntityType reivindicacoes = Modelo.GetEntityTypes().Single(e => e.GetTableName() == "reivindicacoes_usuario");
        Assert.Equal("IdReivindicacaoUsuario", reivindicacoes.FindProperty("Id")!.GetColumnName());
    }

    private static IModel CarregarModelo()
    {
        DbContextOptionsBuilder<AcessoTenantDbContext> construtor = new();
        construtor.UseNpgsql("Host=localhost", npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "foundation"));
        using AcessoTenantDbContext contexto = new(construtor.Options);
        return contexto.GetService<IDesignTimeModel>().Model;
    }
}
