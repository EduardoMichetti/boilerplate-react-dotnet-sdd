namespace Platform.UnitTests.Architecture;

using System.Text.RegularExpressions;
using Xunit;

/// <summary>
/// T054: varredura textual dos fontes de produção (<c>backend/src/**</c>, exceto migrations)
/// barrando relógio estático, geração de Guid fora do <c>IdentificadorV7</c>, operações em lote
/// que não passam pelo interceptor de auditoria e defaults SQL de data (Constituição IX).
/// </summary>
public sealed class TestesArquiteturaRelogioEIdentificadores
{
    private static readonly (string Nome, Regex Padrao)[] Proibidos =
    [
        ("DateTime.Now", new Regex(@"\bDateTime\.Now\b")),
        ("DateTime.UtcNow", new Regex(@"\bDateTime\.UtcNow\b")),
        ("DateTime.Today", new Regex(@"\bDateTime\.Today\b")),
        ("DateTimeOffset.Now", new Regex(@"\bDateTimeOffset\.Now\b")),
        ("DateTimeOffset.UtcNow", new Regex(@"\bDateTimeOffset\.UtcNow\b")),
        ("Guid.NewGuid(", new Regex(@"\bGuid\.NewGuid\s*\(")),
        ("Guid.CreateVersion7() sem argumento", new Regex(@"\bGuid\.CreateVersion7\s*\(\s*\)")),
        ("ExecuteUpdate", new Regex(@"\bExecuteUpdate(Async)?\b")),
        ("ExecuteDelete", new Regex(@"\bExecuteDelete(Async)?\b")),
        ("HasDefaultValueSql", new Regex(@"\bHasDefaultValueSql\b")),
    ];

    public static TheoryData<string> PadroesProibidos()
    {
        TheoryData<string> dados = [];
        foreach ((string nome, _) in Proibidos)
        {
            dados.Add(nome);
        }

        return dados;
    }

    [Theory]
    [MemberData(nameof(PadroesProibidos))]
    public void CodigoDeProducao_NaoUsaApiProibida(string nomePadrao)
    {
        Regex padrao = Proibidos.Single(p => p.Nome == nomePadrao).Padrao;
        List<string> violacoes = [];

        foreach (string arquivo in ArquivosDeProducao())
        {
            string[] linhas = File.ReadAllLines(arquivo);
            for (int i = 0; i < linhas.Length; i++)
            {
                if (padrao.IsMatch(linhas[i]))
                {
                    violacoes.Add($"{Path.GetRelativePath(CaminhosRepositorio.RaizBackend, arquivo)}:{i + 1}");
                }
            }
        }

        Assert.True(violacoes.Count == 0, $"Uso proibido de {nomePadrao}: {string.Join(", ", violacoes)}");
    }

    [Fact]
    public void Varredura_EncontraFontesDeTodosOsProjetosDeProducao()
    {
        string[] projetos = ["Platform.Domain", "Platform.Application", "Platform.Infrastructure", "App.Api", "Platform.Operations"];
        List<string> arquivos = ArquivosDeProducao().ToList();

        Assert.All(projetos, projeto => Assert.Contains(
            arquivos,
            a => a.Contains(Path.DirectorySeparatorChar + projeto + Path.DirectorySeparatorChar, StringComparison.Ordinal)));
    }

    private static IEnumerable<string> ArquivosDeProducao()
    {
        string raizSrc = Path.Combine(CaminhosRepositorio.RaizBackend, "src");
        string separador = Path.DirectorySeparatorChar.ToString();

        return Directory.EnumerateFiles(raizSrc, "*.cs", SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{separador}Migrations{separador}", StringComparison.Ordinal)
                && !a.Contains($"{separador}obj{separador}", StringComparison.Ordinal)
                && !a.Contains($"{separador}bin{separador}", StringComparison.Ordinal));
    }
}

/// <summary>
/// Localiza a raiz <c>backend</c> do repositório a partir do diretório de execução dos testes.
/// </summary>
internal static class CaminhosRepositorio
{
    /// <summary>
    /// Caminho absoluto da pasta <c>backend</c> (contém <c>Boilerplate.slnx</c>).
    /// </summary>
    public static readonly string RaizBackend = Localizar();

    private static string Localizar()
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

        throw new InvalidOperationException("Raiz backend não localizada a partir do diretório de execução dos testes.");
    }
}
