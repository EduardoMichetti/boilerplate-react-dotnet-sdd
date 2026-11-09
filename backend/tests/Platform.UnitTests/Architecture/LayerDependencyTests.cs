namespace Platform.UnitTests.Architecture;

using System.Reflection;
using System.Xml.Linq;
using Xunit;

/// <summary>
/// Testes automatizados de arquitetura (guardrails) para garantir que os limites
/// entre as camadas (Domain, Application, Infrastructure, App.Api) sejam rigorosamente preservados:
/// - Platform.Domain não referencia EF Core, Microsoft.AspNetCore.Identity, Npgsql ou HTTP;
/// - Platform.Application referencia apenas Platform.Domain e tipos base, nunca Platform.Infrastructure ou App.Api;
/// - Não existem referências cíclicas entre projetos.
/// Atende à tarefa T034 e aos requisitos da User Story 4 (FR-025, NFR-004, NFR-006).
/// </summary>
public sealed class LayerDependencyTests
{
    private static readonly string CaminhoRaizBackend = ObterCaminhoRaizBackend();

    [Fact]
    public void PlatformDomain_NaoReferenciaEFCoreIdentityOuHttp()
    {
        string caminhoCsproj = Path.Combine(CaminhoRaizBackend, "src", "Platform.Domain", "Platform.Domain.csproj");
        XDocument documento = XDocument.Load(caminhoCsproj);

        var pacotesReferenciados = documento.Descendants("PackageReference")
            .Select(p => p.Attribute("Include")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var projetosReferenciados = documento.Descendants("ProjectReference")
            .Select(p => p.Attribute("Include")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        Assert.Empty(projetosReferenciados);

        foreach (string? pacote in pacotesReferenciados)
        {
            Assert.DoesNotContain("EntityFrameworkCore", pacote, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Identity", pacote, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Npgsql", pacote, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AspNetCore", pacote, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Http", pacote, StringComparison.OrdinalIgnoreCase);
        }

        Assembly assemblyDomain = typeof(Platform.Domain.Tenancy.EstadoTenant).Assembly;
        AssemblyName[] assembliesReferenciados = assemblyDomain.GetReferencedAssemblies();

        foreach (AssemblyName referencia in assembliesReferenciados)
        {
            Assert.DoesNotContain("EntityFrameworkCore", referencia.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Identity", referencia.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Npgsql", referencia.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("AspNetCore", referencia.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void PlatformApplication_NaoReferenciaInfrastructureOuApi()
    {
        string caminhoCsproj = Path.Combine(CaminhoRaizBackend, "src", "Platform.Application", "Platform.Application.csproj");
        XDocument documento = XDocument.Load(caminhoCsproj);

        var pacotesReferenciados = documento.Descendants("PackageReference")
            .Select(p => p.Attribute("Include")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        var projetosReferenciados = documento.Descendants("ProjectReference")
            .Select(p => p.Attribute("Include")?.Value)
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

        Assert.All(projetosReferenciados, projeto =>
        {
            Assert.DoesNotContain("Platform.Infrastructure", projeto, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("App.Api", projeto, StringComparison.OrdinalIgnoreCase);
        });

        foreach (string? pacote in pacotesReferenciados)
        {
            Assert.DoesNotContain("EntityFrameworkCore", pacote, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Npgsql", pacote, StringComparison.OrdinalIgnoreCase);
        }

        Assembly assemblyApplication = typeof(Platform.Application.Configuration.OpcoesInstalacaoTenant).Assembly;
        AssemblyName[] assembliesReferenciados = assemblyApplication.GetReferencedAssemblies();

        foreach (AssemblyName referencia in assembliesReferenciados)
        {
            Assert.DoesNotContain("Platform.Infrastructure", referencia.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("App.Api", referencia.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("EntityFrameworkCore", referencia.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Npgsql", referencia.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void GrafoDeDependencias_NaoPossuiCiclosOuDependenciasInvertidas()
    {
        Dictionary<string, List<string>> grafo = new(StringComparer.OrdinalIgnoreCase);

        string[] caminhosProjetos =
        [
            Path.Combine(CaminhoRaizBackend, "src", "Platform.Domain", "Platform.Domain.csproj"),
            Path.Combine(CaminhoRaizBackend, "src", "Platform.Application", "Platform.Application.csproj"),
            Path.Combine(CaminhoRaizBackend, "src", "Platform.Infrastructure", "Platform.Infrastructure.csproj"),
            Path.Combine(CaminhoRaizBackend, "src", "App.Api", "App.Api.csproj"),
            Path.Combine(CaminhoRaizBackend, "src", "Platform.Operations", "Platform.Operations.csproj")
        ];

        foreach (string caminho in caminhosProjetos)
        {
            string nomeProjeto = Path.GetFileNameWithoutExtension(caminho);
            XDocument documento = XDocument.Load(caminho);

            var referencias = documento.Descendants("ProjectReference")
                .Select(p => Path.GetFileNameWithoutExtension(p.Attribute("Include")?.Value ?? string.Empty))
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList();

            grafo[nomeProjeto] = referencias;
        }

        Assert.True(grafo["Platform.Domain"].Count == 0, "Platform.Domain não deve referenciar nenhum projeto interno.");
        Assert.True(grafo["Platform.Application"].SequenceEqual(["Platform.Domain"]), "Platform.Application só pode referenciar Platform.Domain.");
        Assert.True(grafo["Platform.Infrastructure"].Contains("Platform.Application"), "Platform.Infrastructure deve referenciar Platform.Application.");
        Assert.False(grafo["Platform.Infrastructure"].Contains("App.Api"), "Platform.Infrastructure não pode referenciar App.Api.");
        Assert.True(grafo["App.Api"].Contains("Platform.Application") && grafo["App.Api"].Contains("Platform.Infrastructure"), "App.Api referencia Application e Infrastructure.");
        Assert.False(grafo.Values.Any(r => r.Contains("Platform.Operations")), "Nenhum projeto de produção referencia Platform.Operations.");
        Assert.False(grafo["Platform.Operations"].Contains("App.Api"), "Platform.Operations não referencia App.Api.");

        HashSet<string> visitados = [];
        HashSet<string> pilhaRecursao = [];

        foreach (string node in grafo.Keys)
        {
            Assert.False(TemCiclo(node, grafo, visitados, pilhaRecursao), $"Ciclo detectado a partir do nó {node}.");
        }
    }

    [Fact]
    public void PlatformOperations_ReferenciaSomenteApplicationEInfrastructureSemServidorHttp()
    {
        string caminhoCsproj = Path.Combine(CaminhoRaizBackend, "src", "Platform.Operations", "Platform.Operations.csproj");
        XDocument documento = XDocument.Load(caminhoCsproj);

        Assert.Equal("Microsoft.NET.Sdk", documento.Root?.Attribute("Sdk")?.Value);

        List<string> projetos = documento.Descendants("ProjectReference")
            .Select(p => Path.GetFileNameWithoutExtension(p.Attribute("Include")?.Value ?? string.Empty))
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .OrderBy(n => n, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(["Platform.Application", "Platform.Infrastructure"], projetos);

        foreach (string? pacote in documento.Descendants("PackageReference").Select(p => p.Attribute("Include")?.Value))
        {
            Assert.DoesNotContain("Microsoft.AspNetCore", pacote, StringComparison.OrdinalIgnoreCase);
        }

        Assert.DoesNotContain(
            documento.Descendants("FrameworkReference"),
            f => string.Equals(f.Attribute("Include")?.Value, "Microsoft.AspNetCore.App", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void PlatformDomain_NaoReferenciaIdentityEfOuHttpNemPorPacoteTransitivo()
    {
        string caminhoLock = Path.Combine(CaminhoRaizBackend, "src", "Platform.Domain", "packages.lock.json");
        string conteudo = File.ReadAllText(caminhoLock);

        Assert.DoesNotContain("EntityFrameworkCore", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Identity", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AspNetCore", conteudo, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TemCiclo(
        string no,
        Dictionary<string, List<string>> grafo,
        HashSet<string> visitados,
        HashSet<string> pilhaRecursao)
    {
        if (pilhaRecursao.Contains(no))
        {
            return true;
        }

        if (visitados.Contains(no))
        {
            return false;
        }

        visitados.Add(no);
        pilhaRecursao.Add(no);

        if (grafo.TryGetValue(no, out List<string>? vizinhos))
        {
            foreach (string vizinho in vizinhos)
            {
                if (TemCiclo(vizinho, grafo, visitados, pilhaRecursao))
                {
                    return true;
                }
            }
        }

        pilhaRecursao.Remove(no);
        return false;
    }

    private static string ObterCaminhoRaizBackend()
    {
        string? diretorioAtual = AppContext.BaseDirectory;
        while (diretorioAtual is not null)
        {
            string possivelCaminho = Path.Combine(diretorioAtual, "backend");
            if (Directory.Exists(possivelCaminho) && File.Exists(Path.Combine(possivelCaminho, "Boilerplate.slnx")))
            {
                return possivelCaminho;
            }

            diretorioAtual = Directory.GetParent(diretorioAtual)?.FullName;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "backend"));
    }
}
