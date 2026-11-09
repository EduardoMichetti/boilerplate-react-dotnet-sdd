using System.Reflection;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using App.Api.Diagnostics;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Access;
using Platform.Domain.Resultados;
using Xunit;

namespace Platform.ContractTests.Architecture;

/// <summary>
/// T082: regras de arquitetura da ADR 0003 — validador por <c>Requisicao*</c>, validadores sem I/O,
/// <c>JsonUnmappedMemberHandling.Skip</c> isolado em <c>*.Integracoes.*</c>, códigos de erro únicos
/// entre catálogos, declarados uma única vez e <see cref="TipoErro.NaoAutenticado"/> só em <c>ErrosAcesso</c>.
/// </summary>
public sealed class TestesArquiteturaValidacaoErros
{
    private static readonly Regex PadraoCodigo = new("^[A-Z][A-Z0-9_]+$", RegexOptions.CultureInvariant);

    private static readonly Assembly[] AssembliesProducao =
    [
        typeof(Erro).Assembly,
        typeof(ErrosAcesso).Assembly,
        typeof(Platform.Infrastructure.Persistence.TenantAccess.AcessoTenantDbContext).Assembly,
        typeof(ErrosPlataforma).Assembly,
    ];

    [Fact]
    public void TodaRequisicaoTemValidadorRegistradoEComNomePadrao()
    {
        ServiceCollection servicos = new();
        servicos.AdicionarValidacaoRequisicoes();
        using ServiceProvider provedor = servicos.BuildServiceProvider();

        List<Type> requisicoes = TiposProducao()
            .Where(t => t.IsClass && !t.IsAbstract && t.Name.StartsWith(FiltroValidacaoRequisicao.PrefixoRequisicao, StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(requisicoes);
        Assert.All(requisicoes, requisicao =>
        {
            Assert.NotNull(provedor.GetService(typeof(IValidator<>).MakeGenericType(requisicao)));
            Assert.Contains(TiposProducao(), t => t.Name == $"Validador{requisicao.Name}" && typeof(IValidator<>).MakeGenericType(requisicao).IsAssignableFrom(t));
        });
    }

    [Fact]
    public void ValidadoresNaoDependemDeBancoRepositorioOuRelogio()
    {
        List<Type> validadores = TiposProducao()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IValidator).IsAssignableFrom(t))
            .ToList();

        Assert.NotEmpty(validadores);
        Assert.All(validadores.SelectMany(v => v.GetConstructors()).SelectMany(c => c.GetParameters()), parametro =>
        {
            Assert.False(typeof(DbContext).IsAssignableFrom(parametro.ParameterType), $"Validador depende de DbContext: {parametro.Member.DeclaringType}");
            Assert.False(typeof(TimeProvider).IsAssignableFrom(parametro.ParameterType), $"Validador depende de TimeProvider: {parametro.Member.DeclaringType}");
            Assert.DoesNotContain("Repositorio", parametro.ParameterType.Name, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ValidadoresNaoUsamRegrasAssincronas()
    {
        Regex assincrono = new(@"\b(MustAsync|CustomAsync|WhenAsync|UnlessAsync|SetAsyncValidator)\b");
        List<string> violacoes = ArquivosProducao()
            .Where(a => Path.GetFileName(a).StartsWith("Validador", StringComparison.Ordinal))
            .Where(a => assincrono.IsMatch(File.ReadAllText(a)))
            .ToList();

        Assert.Empty(violacoes);
    }

    [Fact]
    public void JsonUnmappedMemberHandlingSkipSomenteEmIntegracoes()
    {
        IEnumerable<Type> comSkip = TiposProducao().Where(t =>
            t.GetCustomAttribute<JsonUnmappedMemberHandlingAttribute>()?.UnmappedMemberHandling == JsonUnmappedMemberHandling.Skip);

        Assert.All(comSkip, t => Assert.Contains(".Integracoes.", t.Namespace + ".", StringComparison.Ordinal));
    }

    [Fact]
    public void CodigosDosCatalogosSaoUnicosEntreCatalogosENoPadrao()
    {
        List<(string Catalogo, string Codigo, TipoErro? Tipo)> codigos = CodigosDosCatalogos().ToList();

        Assert.Contains(codigos, c => c.Catalogo == nameof(ErrosAcesso));
        Assert.Contains(codigos, c => c.Catalogo == nameof(ErrosPlataforma));
        Assert.All(codigos, c => Assert.Matches(PadraoCodigo, c.Codigo));
        List<string> duplicados = codigos.GroupBy(c => c.Codigo).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(duplicados.Count == 0, $"Códigos duplicados: {string.Join(", ", duplicados)}");
    }

    [Fact]
    public void NaoAutenticadoSoNoCatalogoErrosAcesso()
    {
        Assert.All(
            CodigosDosCatalogos().Where(c => c.Tipo == TipoErro.NaoAutenticado),
            c => Assert.Equal(nameof(ErrosAcesso), c.Catalogo));

        List<string> usosEmCatalogos = ArquivosProducao()
            .Where(a => Path.GetFileName(a).StartsWith("Erros", StringComparison.Ordinal) && Path.GetFileName(a) != "ErrosAcesso.cs")
            .Where(a => File.ReadAllText(a).Contains("TipoErro.NaoAutenticado", StringComparison.Ordinal))
            .ToList();
        Assert.Empty(usosEmCatalogos);
    }

    [Fact]
    public void CodigosDeErroNaoSaoDeclaradosForaDosCatalogos()
    {
        HashSet<string> codigos = CodigosDosCatalogos().Select(c => c.Codigo).ToHashSet(StringComparer.Ordinal);
        HashSet<string> arquivosCatalogo = TiposCatalogo().Select(t => t.Name + ".cs").ToHashSet(StringComparer.Ordinal);
        Regex literal = new("\"([A-Z][A-Z0-9_]+)\"");
        List<string> violacoes = [];

        foreach (string arquivo in ArquivosProducao().Where(a => !arquivosCatalogo.Contains(Path.GetFileName(a))))
        {
            string conteudo = File.ReadAllText(arquivo);
            violacoes.AddRange(literal.Matches(conteudo)
                .Select(m => m.Groups[1].Value)
                .Where(codigos.Contains)
                .Select(c => $"{Path.GetFileName(arquivo)}: {c}"));

            if (Regex.IsMatch(conteudo, @"\[""code""\]\s*=\s*""", RegexOptions.CultureInvariant))
            {
                violacoes.Add($"{Path.GetFileName(arquivo)}: literal atribuído a code");
            }
        }

        Assert.True(violacoes.Count == 0, string.Join("; ", violacoes));
    }

    private static IEnumerable<Type> TiposProducao() => AssembliesProducao.Distinct().SelectMany(a => a.GetTypes());

    private static IEnumerable<Type> TiposCatalogo() => TiposProducao()
        .Where(t => t.IsClass && t.IsAbstract && t.IsSealed && Regex.IsMatch(t.Name, "^Erros[A-Z]"));

    private static IEnumerable<(string Catalogo, string Codigo, TipoErro? Tipo)> CodigosDosCatalogos()
    {
        foreach (Type catalogo in TiposCatalogo())
        {
            foreach (FieldInfo campo in catalogo.GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (campo.FieldType == typeof(Erro) && campo.GetValue(null) is Erro erro)
                {
                    yield return (catalogo.Name, erro.Codigo, erro.Tipo);
                }
                else if (campo.IsLiteral && campo.FieldType == typeof(string) && campo.GetRawConstantValue() is string codigo)
                {
                    yield return (catalogo.Name, codigo, null);
                }
            }

            foreach (PropertyInfo propriedade in catalogo.GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == typeof(Erro)))
            {
                Erro erro = (Erro)propriedade.GetValue(null)!;
                yield return (catalogo.Name, erro.Codigo, erro.Tipo);
            }
        }
    }

    private static IEnumerable<string> ArquivosProducao()
    {
        string raizSrc = Path.Combine(RaizBackend(), "src");
        string separador = Path.DirectorySeparatorChar.ToString();
        return Directory.EnumerateFiles(raizSrc, "*.cs", SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{separador}Migrations{separador}", StringComparison.Ordinal)
                && !a.Contains($"{separador}obj{separador}", StringComparison.Ordinal)
                && !a.Contains($"{separador}bin{separador}", StringComparison.Ordinal));
    }

    private static string RaizBackend()
    {
        string? diretorio = AppContext.BaseDirectory;
        while (diretorio is not null)
        {
            string candidato = Path.Combine(diretorio, "backend");
            if (File.Exists(Path.Combine(candidato, "Boilerplate.slnx")))
            {
                return candidato;
            }

            diretorio = Directory.GetParent(diretorio)?.FullName;
        }

        throw new InvalidOperationException("Raiz backend não localizada.");
    }
}
