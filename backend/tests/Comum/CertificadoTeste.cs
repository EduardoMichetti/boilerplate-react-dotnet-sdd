namespace Platform.Testes.Comum;

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

/// <summary>
/// Certificado PKCS#12 autoassinado gerado na execução (nunca versionado) para o key ring em
/// Production, com senha aleatória gravada num arquivo próprio.
/// </summary>
public sealed class CertificadoTeste : IDisposable
{
    private readonly string _diretorio;

    /// <summary>
    /// Gera o certificado e os arquivos num diretório temporário exclusivo.
    /// </summary>
    public CertificadoTeste()
    {
        _diretorio = Directory.CreateTempSubdirectory("bps-certificado-").FullName;
        string senha = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        using RSA chave = RSA.Create(2048);
        CertificateRequest pedido = new("CN=bps-key-ring-teste", chave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        DateTimeOffset referencia = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using X509Certificate2 certificado = pedido.CreateSelfSigned(referencia, referencia.AddYears(5));

        ArquivoCertificado = Path.Combine(_diretorio, "key-ring.pfx");
        ArquivoSenha = Path.Combine(_diretorio, "key-ring.senha");
        File.WriteAllBytes(ArquivoCertificado, certificado.Export(X509ContentType.Pfx, senha));
        File.WriteAllText(ArquivoSenha, senha + "\n");
        Senha = senha;
    }

    /// <summary>Caminho do PKCS#12.</summary>
    public string ArquivoCertificado { get; }

    /// <summary>Caminho do arquivo da senha.</summary>
    public string ArquivoSenha { get; }

    /// <summary>Senha (somente para verificar ausência em diagnósticos).</summary>
    public string Senha { get; }

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            Directory.Delete(_diretorio, recursive: true);
        }
        catch (IOException)
        {
            // Diretório temporário: limpeza best effort.
        }
    }
}
