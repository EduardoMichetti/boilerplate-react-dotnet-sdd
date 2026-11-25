namespace Platform.Application.Configuration;

using Microsoft.Extensions.Options;

/// <summary>
/// Opções da sessão autenticada da API (D31, L2.1): janela de inatividade e teto absoluto, ambos
/// aplicados somente na emissão (<c>TetoAbsolutoEm = CriadoEm + TetoAbsolutoMinutos</c>;
/// <c>JanelaInatividadeMinutos = InatividadeMinutos</c>). A chave L2 <c>Sessao:PrazoAbsolutoMinutos</c>
/// é obsoleta: com valor não vazio impede a inicialização e nunca é reinterpretada.
/// </summary>
public sealed class OpcoesSessao
{
    /// <summary>
    /// Chave de configuração da janela de inatividade.
    /// </summary>
    public const string ChaveInatividadeMinutos = "Sessao:InatividadeMinutos";

    /// <summary>
    /// Chave de configuração do teto absoluto.
    /// </summary>
    public const string ChaveTetoAbsolutoMinutos = "Sessao:TetoAbsolutoMinutos";

    /// <summary>
    /// Chave obsoleta da L2 (prazo absoluto único).
    /// </summary>
    public const string ChavePrazoAbsolutoMinutosObsoleta = "Sessao:PrazoAbsolutoMinutos";

    /// <summary>
    /// Janela de inatividade padrão, em minutos, quando ausente ou vazia.
    /// </summary>
    public const int InatividadePadraoMinutos = 30;

    /// <summary>
    /// Janela de inatividade mínima inclusiva, em minutos.
    /// </summary>
    public const int InatividadeMinimaMinutos = 5;

    /// <summary>
    /// Janela de inatividade máxima inclusiva, em minutos.
    /// </summary>
    public const int InatividadeMaximaMinutos = 240;

    /// <summary>
    /// Teto absoluto padrão, em minutos, quando ausente ou vazio.
    /// </summary>
    public const int TetoPadraoMinutos = 600;

    /// <summary>
    /// Teto absoluto mínimo inclusivo, em minutos.
    /// </summary>
    public const int TetoMinimoMinutos = 30;

    /// <summary>
    /// Teto absoluto máximo inclusivo, em minutos.
    /// </summary>
    public const int TetoMaximoMinutos = 720;

    /// <summary>
    /// Texto bruto de <c>Sessao:InatividadeMinutos</c> (nulo ou vazio = ausente).
    /// </summary>
    public string? InatividadeMinutosBruto { get; set; }

    /// <summary>
    /// Texto bruto de <c>Sessao:TetoAbsolutoMinutos</c> (nulo ou vazio = ausente).
    /// </summary>
    public string? TetoAbsolutoMinutosBruto { get; set; }

    /// <summary>
    /// Texto bruto da chave obsoleta <c>Sessao:PrazoAbsolutoMinutos</c>; só pode estar ausente ou vazio.
    /// </summary>
    public string? PrazoAbsolutoMinutosObsoletoBruto { get; set; }

    /// <summary>
    /// Janela de inatividade efetiva em minutos (5–240, padrão 30).
    /// </summary>
    /// <exception cref="InvalidOperationException">Valor configurado inválido (a validação na inicialização impede este caso).</exception>
    public int InatividadeMinutos =>
        LeitorInteiroConfiguracao.TentarLer(InatividadeMinutosBruto, InatividadeMinimaMinutos, InatividadeMaximaMinutos, InatividadePadraoMinutos, out int valor)
            ? valor
            : throw new InvalidOperationException($"{ChaveInatividadeMinutos} inválido.");

    /// <summary>
    /// Teto absoluto efetivo em minutos (30–720, padrão 600).
    /// </summary>
    /// <exception cref="InvalidOperationException">Valor configurado inválido (a validação na inicialização impede este caso).</exception>
    public int TetoAbsolutoMinutos =>
        LeitorInteiroConfiguracao.TentarLer(TetoAbsolutoMinutosBruto, TetoMinimoMinutos, TetoMaximoMinutos, TetoPadraoMinutos, out int valor)
            ? valor
            : throw new InvalidOperationException($"{ChaveTetoAbsolutoMinutos} inválido.");
}

/// <summary>
/// Valida <see cref="OpcoesSessao"/> na inicialização com diagnóstico seguro (cita só chaves e faixas,
/// nunca valores configurados).
/// </summary>
public sealed class ValidadorOpcoesSessao : IValidateOptions<OpcoesSessao>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpcoesSessao options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> falhas = [];

        if (!string.IsNullOrEmpty(options.PrazoAbsolutoMinutosObsoletoBruto))
        {
            falhas.Add(
                $"{OpcoesSessao.ChavePrazoAbsolutoMinutosObsoleta} é obsoleta e deve ficar ausente ou vazia; " +
                $"configure {OpcoesSessao.ChaveInatividadeMinutos} e {OpcoesSessao.ChaveTetoAbsolutoMinutos}.");
        }

        bool inatividadeValida = LeitorInteiroConfiguracao.TentarLer(
            options.InatividadeMinutosBruto,
            OpcoesSessao.InatividadeMinimaMinutos,
            OpcoesSessao.InatividadeMaximaMinutos,
            OpcoesSessao.InatividadePadraoMinutos,
            out int inatividade);
        if (!inatividadeValida)
        {
            falhas.Add(
                $"{OpcoesSessao.ChaveInatividadeMinutos} deve ser um inteiro decimal entre " +
                $"{OpcoesSessao.InatividadeMinimaMinutos}–{OpcoesSessao.InatividadeMaximaMinutos} (inclusive) ou ausente (padrão {OpcoesSessao.InatividadePadraoMinutos}).");
        }

        bool tetoValido = LeitorInteiroConfiguracao.TentarLer(
            options.TetoAbsolutoMinutosBruto,
            OpcoesSessao.TetoMinimoMinutos,
            OpcoesSessao.TetoMaximoMinutos,
            OpcoesSessao.TetoPadraoMinutos,
            out int teto);
        if (!tetoValido)
        {
            falhas.Add(
                $"{OpcoesSessao.ChaveTetoAbsolutoMinutos} deve ser um inteiro decimal entre " +
                $"{OpcoesSessao.TetoMinimoMinutos}–{OpcoesSessao.TetoMaximoMinutos} (inclusive) ou ausente (padrão {OpcoesSessao.TetoPadraoMinutos}).");
        }

        if (inatividadeValida && tetoValido && inatividade > teto)
        {
            falhas.Add($"{OpcoesSessao.ChaveInatividadeMinutos} maior que {OpcoesSessao.ChaveTetoAbsolutoMinutos}: a janela de inatividade não pode exceder o teto absoluto.");
        }

        return falhas.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(falhas);
    }
}
