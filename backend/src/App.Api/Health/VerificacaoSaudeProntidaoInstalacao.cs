namespace App.Api.Health;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Application.Configuration;
using Platform.Application.Tenancy;
using Platform.Domain.Tenancy;

/// <summary>
/// Verificação de saúde de prontidão (readiness) da instalação do tenant.
/// Valida a integridade da configuração em runtime e a identidade/versão de migração do banco de dados configurado,
/// sem executar migrações no startup ou expor detalhes sensíveis.
/// </summary>
public sealed class VerificacaoSaudeProntidaoInstalacao(
    IOptions<OpcoesInstalacaoTenant> opcoes,
    IVerificadorIdentidadeBancoTenant verificador,
    ILogger<VerificacaoSaudeProntidaoInstalacao> registrador) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            OpcoesInstalacaoTenant config = opcoes.Value;
            config.Validar();

            if (config.EstadoTenant != EstadoTenant.Ativo)
            {
                registrador.LogWarning("Prontidão não atendida: o tenant está configurado como Inativo.");
                return HealthCheckResult.Unhealthy("O tenant está configurado como inativo.");
            }

            IdentidadeBancoTenantSnapshot snapshot = await verificador.LerAsync(cancellationToken);

            if (snapshot.IdTenant != config.IdTenant)
            {
                registrador.LogError("Prontidão falhou: IdTenant do banco diverge da configuração da instalação.");
                return HealthCheckResult.Unhealthy("Identidade do tenant diverge da esperada.");
            }

            if (snapshot.IdAmbiente != config.IdAmbiente)
            {
                registrador.LogError("Prontidão falhou: IdAmbiente do banco diverge da configuração da instalação.");
                return HealthCheckResult.Unhealthy("Identidade do ambiente diverge da esperada.");
            }

            if (!snapshot.PossuiVersaoMigracaoEsperada)
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
        catch (ExcecaoIsolamentoTenant ex)
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

