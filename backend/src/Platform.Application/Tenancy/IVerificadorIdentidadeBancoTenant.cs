namespace Platform.Application.Tenancy;

/// <summary>
/// Contrato para verificação da identidade e versão de migração do banco de dados da instalação do tenant.
/// </summary>
public interface IVerificadorIdentidadeBancoTenant
{
    /// <summary>
    /// Realiza a leitura assíncrona do snapshot de identidade do banco de dados e valida se as migrações estão em dia.
    /// </summary>
    /// <param name="tokenCancelamento">Token de cancelamento da operação assíncrona.</param>
    /// <returns>Registro contendo o snapshot dos dados de identidade do banco de dados.</returns>
    Task<IdentidadeBancoTenantSnapshot> LerAsync(CancellationToken tokenCancelamento = default);
}

/// <summary>
/// Snapshot imutável contendo os dados de identidade do banco de dados do tenant e o estado das migrações.
/// </summary>
/// <param name="IdTenant">Identificador do tenant registrado no banco.</param>
/// <param name="IdAmbiente">Identificador do ambiente registrado no banco.</param>
/// <param name="EpocaAcesso">Identificador de época de acesso do banco.</param>
/// <param name="PossuiVersaoMigracaoEsperada">Indica se não há migrações pendentes na base.</param>
public sealed record IdentidadeBancoTenantSnapshot(
    Guid IdTenant,
    Guid IdAmbiente,
    Guid EpocaAcesso,
    bool PossuiVersaoMigracaoEsperada);
