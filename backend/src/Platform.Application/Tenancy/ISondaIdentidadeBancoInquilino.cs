namespace Platform.Application.Tenancy;

/// <summary>
/// Contrato para sondagem de identidade e versão de migração do banco de dados da instalação.
/// </summary>
public interface ISondaIdentidadeBancoInquilino
{
    /// <summary>
    /// Realiza a leitura assíncrona do snapshot de identidade do banco de dados e valida se as migrações estão em dia.
    /// </summary>
    /// <param name="tokenCancelamento">Token de cancelamento da operação assíncrona.</param>
    /// <returns>Registro contendo a foto dos dados de identidade do banco de dados.</returns>
    Task<InstantaneoIdentidadeBancoInquilino> LerAsync(CancellationToken tokenCancelamento = default);
}

/// <summary>
/// Instantâneo imutável contendo os dados de identidade do banco de dados e o estado das migrações.
/// </summary>
/// <param name="IdInquilino">Identificador do inquilino registrado no banco.</param>
/// <param name="IdAmbiente">Identificador do ambiente registrado no banco.</param>
/// <param name="EpocaAcesso">Identificador de época de acesso do banco.</param>
/// <param name="PossuiVersaoMigracaoEsperada">Indica se não há migrações pendentes na base.</param>
public sealed record InstantaneoIdentidadeBancoInquilino(
    Guid IdInquilino,
    Guid IdAmbiente,
    Guid EpocaAcesso,
    bool PossuiVersaoMigracaoEsperada);
