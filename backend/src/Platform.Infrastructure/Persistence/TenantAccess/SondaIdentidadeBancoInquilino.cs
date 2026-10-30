namespace Platform.Infrastructure.Persistence.TenantAccess;

using Microsoft.EntityFrameworkCore;
using Platform.Application.Tenancy;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Implementação da sonda de identidade do banco de dados do inquilino utilizando o contexto do EF Core.
/// </summary>
public sealed class SondaIdentidadeBancoInquilino(ContextoBancoAcessoInquilino contexto)
    : ISondaIdentidadeBancoInquilino
{
    /// <inheritdoc />
    public async Task<InstantaneoIdentidadeBancoInquilino> LerAsync(CancellationToken tokenCancelamento = default)
    {
        IdentidadeBancoInquilino? identidade = await contexto.IdentidadeBanco
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdUnico == IdentidadeBancoInquilino.IdUnicoPadrao, tokenCancelamento);

        if (identidade is null)
        {
            throw new ExcecaoIsolamentoInquilino();
        }

        IEnumerable<string> migracoesPendentes = await contexto.Database.GetPendingMigrationsAsync(tokenCancelamento);
        bool possuiVersaoMigracaoEsperada = !migracoesPendentes.Any();

        return new InstantaneoIdentidadeBancoInquilino(
            identidade.IdInquilino,
            identidade.IdAmbiente,
            identidade.EpocaAcesso,
            possuiVersaoMigracaoEsperada);
    }
}
