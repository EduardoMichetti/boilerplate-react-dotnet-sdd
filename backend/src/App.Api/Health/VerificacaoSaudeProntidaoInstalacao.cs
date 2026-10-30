namespace App.Api.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Configuration;
using Platform.Application.Tenancy;
using Platform.Domain.Tenancy;

/// <summary>
/// Verificação de saúde de prontidão (readiness) da instalação do inquilino.
/// Valida a integridade da configuração em runtime e a identidade/versão de migração do banco de dados configurado,
/// sem executar migrações no startup ou expor detalhes sensíveis.
/// </summary>
public sealed class VerificacaoSaudeProntidaoInstalacao(
    IOptions<OpcoesInquilinoInstalacao> opcoes,
    ISondaIdentidadeBancoInquilino sonda,
    ILogger<VerificacaoSaudeProntidaoInstalacao> registrador) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            OpcoesInquilinoInstalacao config = opcoes.Value;
            config.Validar();

            if (config.EstadoInquilino != EstadoInquilino.Ativo)
            {
                registrador.LogWarning("Prontidão não atendida: o inquilino está configurado como Inativo.");
                return HealthCheckResult.Unhealthy("O inquilino está configurado como inativo.");
            }

            InstantaneoIdentidadeBancoInquilino instantaneo = await sonda.LerAsync(cancellationToken);

            if (instantaneo.IdInquilino != config.IdInquilino)
            {
                registrador.LogError("Prontidão falhou: IdInquilino do banco diverge da configuração da instalação.");
                return HealthCheckResult.Unhealthy("Identidade do inquilino diverge da esperada.");
            }

            if (instantaneo.IdAmbiente != config.IdAmbiente)
            {
                registrador.LogError("Prontidão falhou: IdAmbiente do banco diverge da configuração da instalação.");
                return HealthCheckResult.Unhealthy("Identidade do ambiente diverge da esperada.");
            }

            if (!instantaneo.PossuiVersaoMigracaoEsperada)
            {
                registrador.LogError("Prontidão falhou: existem migrações pendentes no banco de dados.");
                return HealthCheckResult.Unhealthy("O banco de dados possui migrações pendentes.");
            }

            return HealthCheckResult.Healthy("Instalação pronta.");
        }
        catch (ExcecaoConfiguracaoInstalacao ex)
        {
            registrador.LogError(ex, "Prontidão falhou: erro de configuração da instalação.");
            return HealthCheckResult.Unhealthy("Configuração inválida.");
        }
        catch (ExcecaoIsolamentoInquilino ex)
        {
            registrador.LogError(ex, "Prontidão falhou: violação de isolamento de banco ou marcador ausente.");
            return HealthCheckResult.Unhealthy("Falha de isolamento ou banco sem marcador.");
        }
        catch (Exception ex)
        {
            registrador.LogError(ex, "Prontidão falhou: erro ao comunicar com banco de dados.");
            return HealthCheckResult.Unhealthy("Dependência obrigatória indisponível.");
        }
    }
}

/// <summary>
/// Alias em inglês para VerificacaoSaudeProntidaoInstalacao conforme exigido pela especificação T026.
/// </summary>
public sealed class InstallationReadinessHealthCheck(
    IOptions<OpcoesInquilinoInstalacao> opcoes,
    ISondaIdentidadeBancoInquilino sonda,
    ILogger<VerificacaoSaudeProntidaoInstalacao> registrador) : IHealthCheck
{
    private readonly VerificacaoSaudeProntidaoInstalacao _implementacao = new(opcoes, sonda, registrador);

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        => _implementacao.CheckHealthAsync(context, cancellationToken);
}
