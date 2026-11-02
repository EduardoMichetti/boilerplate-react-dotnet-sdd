namespace Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Entidade de registro único (singleton) gravada na base para comprovar e proteger a identidade da instalação e do tenant.
/// </summary>
public sealed class IdentidadeBancoTenant
{
    /// <summary>
    /// Valor fixo da chave primária singleton (sempre igual a 1) para garantir uma única linha de identidade na base.
    /// </summary>
    public const int IdUnicoPadrao = 1;

    /// <summary>
    /// Chave primária singleton do registro de identidade do banco de dados.
    /// </summary>
    public int IdUnico { get; set; } = IdUnicoPadrao;

    /// <summary>
    /// Identificador único e imutável do tenant proprietário desta base de dados.
    /// </summary>
    public Guid IdTenant { get; set; }

    /// <summary>
    /// Identificador único e imutável do ambiente operacional (ex.: desenvolvimento, homologação ou produção).
    /// </summary>
    public Guid IdAmbiente { get; set; }

    /// <summary>
    /// Identificador de época de acesso do banco de dados, utilizado para invalidação global de sessões e tokens.
    /// </summary>
    public Guid EpocaAcesso { get; set; }

    /// <summary>
    /// Data e hora em UTC em que a base de dados do tenant foi inicializada tecnicamente pela migration.
    /// </summary>
    public DateTimeOffset InicializadoEm { get; set; }

    /// <summary>
    /// Valida as invariantes obrigatórias dos campos do registro de identidade do banco.
    /// </summary>
    public void Validar()
    {
        if (IdUnico != IdUnicoPadrao)
        {
            throw new ArgumentException($"IdUnico deve ser {IdUnicoPadrao}.", nameof(IdUnico));
        }

        if (IdTenant == Guid.Empty)
        {
            throw new ArgumentException("IdTenant deve ser um UUID não vazio.", nameof(IdTenant));
        }

        if (IdAmbiente == Guid.Empty)
        {
            throw new ArgumentException("IdAmbiente deve ser um UUID não vazio.", nameof(IdAmbiente));
        }

        if (EpocaAcesso == Guid.Empty)
        {
            throw new ArgumentException("EpocaAcesso deve ser um UUID não vazio.", nameof(EpocaAcesso));
        }

        if (InicializadoEm == default)
        {
            throw new ArgumentException("InicializadoEm deve ser definido como um instante UTC válido.", nameof(InicializadoEm));
        }
    }
}
