namespace Platform.Application.Configuration;

using Microsoft.Extensions.Options;

/// <summary>
/// Opções da sessão autenticada da API (D21). O prazo é aplicado somente na emissão:
/// <c>ExpiraEm = CriadoEm + PrazoAbsolutoMinutos</c>.
/// </summary>
public sealed class OpcoesSessao
{
    /// <summary>
    /// Chave de configuração do prazo absoluto.
    /// </summary>
    public const string ChavePrazoAbsolutoMinutos = "Sessao:PrazoAbsolutoMinutos";

    /// <summary>
    /// Prazo padrão, em minutos, quando ausente ou vazio.
    /// </summary>
    public const int PrazoPadraoMinutos = 30;

    /// <summary>
    /// Prazo mínimo inclusivo, em minutos.
    /// </summary>
    public const int PrazoMinimoMinutos = 5;

    /// <summary>
    /// Prazo máximo inclusivo, em minutos.
    /// </summary>
    public const int PrazoMaximoMinutos = 480;

    /// <summary>
    /// Texto bruto de <c>Sessao:PrazoAbsolutoMinutos</c> (nulo ou vazio = ausente).
    /// </summary>
    public string? PrazoAbsolutoMinutosBruto { get; set; }

    /// <summary>
    /// Prazo absoluto efetivo em minutos (5–480, padrão 30).
    /// </summary>
    /// <exception cref="InvalidOperationException">Valor configurado inválido (a validação na inicialização impede este caso).</exception>
    public int PrazoAbsolutoMinutos =>
        LeitorInteiroConfiguracao.TentarLer(PrazoAbsolutoMinutosBruto, PrazoMinimoMinutos, PrazoMaximoMinutos, PrazoPadraoMinutos, out int valor)
            ? valor
            : throw new InvalidOperationException($"{ChavePrazoAbsolutoMinutos} inválido.");
}

/// <summary>
/// Valida <see cref="OpcoesSessao"/> na inicialização com diagnóstico seguro (cita só a chave e a faixa).
/// </summary>
public sealed class ValidadorOpcoesSessao : IValidateOptions<OpcoesSessao>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpcoesSessao options)
    {
        ArgumentNullException.ThrowIfNull(options);

        bool valido = LeitorInteiroConfiguracao.TentarLer(
            options.PrazoAbsolutoMinutosBruto,
            OpcoesSessao.PrazoMinimoMinutos,
            OpcoesSessao.PrazoMaximoMinutos,
            OpcoesSessao.PrazoPadraoMinutos,
            out _);

        return valido
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"{OpcoesSessao.ChavePrazoAbsolutoMinutos} deve ser um inteiro decimal entre " +
                $"{OpcoesSessao.PrazoMinimoMinutos}–{OpcoesSessao.PrazoMaximoMinutos} (inclusive) ou ausente (padrão {OpcoesSessao.PrazoPadraoMinutos}).");
    }
}
