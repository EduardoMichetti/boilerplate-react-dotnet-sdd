namespace App.Api.Health;

using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

/// <summary>
/// Prontidão do key ring da instalação: round-trip de proteção com o Data Protection configurado
/// (diretório persistente e certificado em produção). Chaves indisponíveis → não pronto, mesmo com
/// banco pronto. Não consulta contas nem sessões.
/// </summary>
/// <param name="provedor">Data Protection da instalação.</param>
/// <param name="registrador">Registrador.</param>
public sealed class VerificacaoProtecaoChaves(IDataProtectionProvider provedor, ILogger<VerificacaoProtecaoChaves> registrador) : IHealthCheck
{
    private const string Proposito = "App.Api.Health.ProntidaoChaves.v1";

    /// <inheritdoc />
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            IDataProtector protetor = provedor.CreateProtector(Proposito);
            byte[] amostra = RandomNumberGenerator.GetBytes(16);
            byte[] devolvida = protetor.Unprotect(protetor.Protect(amostra));
            return Task.FromResult(devolvida.AsSpan().SequenceEqual(amostra)
                ? HealthCheckResult.Healthy("Key ring disponível.")
                : HealthCheckResult.Unhealthy("Key ring inconsistente."));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            registrador.LogError(new EventId(3001, "KeyRingIndisponivel"), "Prontidão falhou: key ring indisponível. TipoExcecao: {TipoExcecao}", ex.GetType().FullName);
            return Task.FromResult(HealthCheckResult.Unhealthy("Key ring indisponível."));
        }
    }
}
