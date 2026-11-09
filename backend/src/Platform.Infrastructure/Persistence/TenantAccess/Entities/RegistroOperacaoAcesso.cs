namespace Platform.Infrastructure.Persistence.TenantAccess.Entities;

using Platform.Domain.Access;
using Platform.Domain.Tempo;

/// <summary>
/// Registro histórico append-only de uma operação restrita (FR-033). Não herda a base
/// auditável mutável; somente INSERT.
/// </summary>
public sealed class RegistroOperacaoAcesso
{
    /// <summary>
    /// UUID v7 não vazio, PK do evento e correlação do comando.
    /// </summary>
    public Guid IdOperacao { get; set; }

    /// <summary>
    /// Tenant do alvo validado (referência sem constraint).
    /// </summary>
    public Guid IdTenant { get; set; }

    /// <summary>
    /// Ambiente do alvo validado (referência sem constraint).
    /// </summary>
    public Guid IdAmbiente { get; set; }

    /// <summary>
    /// UUID opcional; referência sem FK à conta alvo, preservada após evolução da conta.
    /// </summary>
    public Guid? IdUsuarioAlvo { get; set; }

    /// <summary>
    /// string de 1 a 128: login PostgreSQL individual autenticado do operador.
    /// </summary>
    public string LoginOperador { get; set; } = string.Empty;

    /// <summary>
    /// Tipo da operação executada.
    /// </summary>
    public TipoOperacaoAcesso TipoOperacao { get; set; }

    /// <summary>
    /// string opcional até 128: código de permissão quando relevante.
    /// </summary>
    public string? CodigoPermissao { get; set; }

    /// <summary>
    /// Instante UTC do registro, igual ao <c>InstanteOperacao</c> do comando.
    /// </summary>
    public DateTimeOffset RegistradaEm { get; set; }

    /// <summary>
    /// Resultado da operação.
    /// </summary>
    public ResultadoOperacaoAcesso Resultado { get; set; }

    /// <summary>
    /// string até 100, código estável seguro (sem mensagem SQL).
    /// </summary>
    public string CodigoResultado { get; set; } = string.Empty;

    /// <summary>
    /// int 1–365, somente <c>LimparSessoes</c>: retenção aplicada.
    /// </summary>
    public int? RetencaoDias { get; set; }

    /// <summary>
    /// Somente <c>LimparSessoes</c>: <c>RegistradaEm - RetencaoDias</c>.
    /// </summary>
    public DateTimeOffset? InstanteCorte { get; set; }

    /// <summary>
    /// int ≥ 0, somente <c>LimparSessoes</c>; 0 quando nada era elegível ou houve falha.
    /// </summary>
    public int? QuantidadeRemovida { get; set; }

    /// <summary>
    /// Valida as invariantes do registro, inclusive a coerência dos campos de limpeza.
    /// </summary>
    public void Validar()
    {
        if (IdOperacao == Guid.Empty)
        {
            throw new ArgumentException("IdOperacao deve ser um UUID não vazio.", nameof(IdOperacao));
        }

        if (IdTenant == Guid.Empty || IdAmbiente == Guid.Empty)
        {
            throw new ArgumentException("IdTenant e IdAmbiente devem ser UUIDs não vazios.", nameof(IdTenant));
        }

        if (IdUsuarioAlvo == Guid.Empty)
        {
            throw new ArgumentException("IdUsuarioAlvo, quando informado, deve ser não vazio.", nameof(IdUsuarioAlvo));
        }

        if (string.IsNullOrWhiteSpace(LoginOperador) || LoginOperador.Length > 128)
        {
            throw new ArgumentException("LoginOperador deve conter entre 1 e 128 caracteres.", nameof(LoginOperador));
        }

        if (CodigoPermissao is { Length: 0 or > 128 })
        {
            throw new ArgumentException("CodigoPermissao, quando informado, deve ter até 128 caracteres.", nameof(CodigoPermissao));
        }

        InstanteUtc.Validar(RegistradaEm, nameof(RegistradaEm));

        if (string.IsNullOrWhiteSpace(CodigoResultado) || CodigoResultado.Length > 100)
        {
            throw new ArgumentException("CodigoResultado deve conter entre 1 e 100 caracteres.", nameof(CodigoResultado));
        }

        bool ehLimpeza = TipoOperacao == TipoOperacaoAcesso.LimparSessoes;
        bool camposLimpezaPresentes = RetencaoDias is not null && InstanteCorte is not null && QuantidadeRemovida is not null;
        bool camposLimpezaAusentes = RetencaoDias is null && InstanteCorte is null && QuantidadeRemovida is null;

        if (ehLimpeza)
        {
            if (!camposLimpezaPresentes || IdUsuarioAlvo is not null || CodigoPermissao is not null)
            {
                throw new ArgumentException("LimparSessoes exige retenção, corte e quantidade, sem conta ou permissão alvo.", nameof(TipoOperacao));
            }

            if (RetencaoDias is < 1 or > 365 || QuantidadeRemovida < 0)
            {
                throw new ArgumentException("RetencaoDias deve estar em 1–365 e QuantidadeRemovida ser não negativa.", nameof(RetencaoDias));
            }

            InstanteUtc.Validar(InstanteCorte!.Value, nameof(InstanteCorte));
        }
        else if (!camposLimpezaAusentes)
        {
            throw new ArgumentException("Campos de limpeza só podem ser preenchidos em LimparSessoes.", nameof(TipoOperacao));
        }
    }
}
