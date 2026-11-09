namespace Platform.Infrastructure.Persistence.TenantAccess;

using Microsoft.EntityFrameworkCore;
using Platform.Application.Tenancy;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Implementação do verificador de identidade do banco de dados do tenant utilizando o AcessoTenantDbContext.
/// </summary>
public sealed class VerificadorIdentidadeBancoTenant(AcessoTenantDbContext contexto)
    : IVerificadorIdentidadeBancoTenant
{
    /// <inheritdoc />
    public async Task<IdentidadeBancoTenantSnapshot> LerAsync(CancellationToken tokenCancelamento = default)
    {
        IdentidadeBancoTenant? identidade = await contexto.IdentidadeBanco
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdUnico == IdentidadeBancoTenant.IdUnicoPadrao, tokenCancelamento);

        if (identidade is null)
        {
            throw new ExcecaoIsolamentoTenant();
        }

        // Runtime/readiness aceitam somente a cadeia conhecida completa e na ordem: pendente
        // ou ID desconhecido (banco de versão futura) é versão incompatível.
        IEnumerable<string> aplicadas = await contexto.Database.GetAppliedMigrationsAsync(tokenCancelamento);
        bool possuiVersaoMigracaoEsperada = aplicadas.SequenceEqual(contexto.Database.GetMigrations(), StringComparer.Ordinal);

        return new IdentidadeBancoTenantSnapshot(
            identidade.IdTenant,
            identidade.IdAmbiente,
            identidade.EpocaAcesso,
            possuiVersaoMigracaoEsperada);
    }
}
