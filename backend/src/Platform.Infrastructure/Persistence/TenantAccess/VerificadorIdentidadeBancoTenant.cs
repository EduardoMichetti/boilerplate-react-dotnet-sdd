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

        IEnumerable<string> migracoesPendentes = await contexto.Database.GetPendingMigrationsAsync(tokenCancelamento);
        bool possuiVersaoMigracaoEsperada = !migracoesPendentes.Any();

        return new IdentidadeBancoTenantSnapshot(
            identidade.IdTenant,
            identidade.IdAmbiente,
            identidade.EpocaAcesso,
            possuiVersaoMigracaoEsperada);
    }
}
