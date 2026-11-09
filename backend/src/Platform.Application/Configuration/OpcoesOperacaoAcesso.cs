namespace Platform.Application.Configuration;

using Microsoft.Extensions.Options;

/// <summary>
/// Opções do host operacional (FR-033): login individual autorizado e retenção de
/// sessões encerradas para <c>sessao limpar</c>.
/// </summary>
public sealed class OpcoesOperacaoAcesso
{
    /// <summary>
    /// Chave do login PostgreSQL individual autorizado.
    /// </summary>
    public const string ChaveLoginAutorizado = "OperacaoAcesso:LoginAutorizado";

    /// <summary>
    /// Chave da retenção, em dias.
    /// </summary>
    public const string ChaveRetencaoSessoesDias = "OperacaoAcesso:RetencaoSessoesDias";

    /// <summary>
    /// Retenção padrão, em dias, quando ausente ou vazia.
    /// </summary>
    public const int RetencaoPadraoDias = 30;

    /// <summary>
    /// Retenção mínima inclusiva, em dias.
    /// </summary>
    public const int RetencaoMinimaDias = 1;

    /// <summary>
    /// Retenção máxima inclusiva, em dias.
    /// </summary>
    public const int RetencaoMaximaDias = 365;

    /// <summary>
    /// Login PostgreSQL individual do operador autorizado (não o grupo <c>&lt;banco&gt;_operador</c>).
    /// </summary>
    public string? LoginAutorizado { get; set; }

    /// <summary>
    /// Texto bruto de <c>OperacaoAcesso:RetencaoSessoesDias</c> (nulo ou vazio = ausente).
    /// </summary>
    public string? RetencaoSessoesDiasBruto { get; set; }

    /// <summary>
    /// Retenção efetiva em dias (1–365, padrão 30).
    /// </summary>
    /// <exception cref="InvalidOperationException">Valor configurado inválido.</exception>
    public int RetencaoSessoesDias =>
        LeitorInteiroConfiguracao.TentarLer(RetencaoSessoesDiasBruto, RetencaoMinimaDias, RetencaoMaximaDias, RetencaoPadraoDias, out int valor)
            ? valor
            : throw new InvalidOperationException($"{ChaveRetencaoSessoesDias} inválido.");

    /// <summary>
    /// Indica se a retenção configurada é válida.
    /// </summary>
    public bool RetencaoValida =>
        LeitorInteiroConfiguracao.TentarLer(RetencaoSessoesDiasBruto, RetencaoMinimaDias, RetencaoMaximaDias, RetencaoPadraoDias, out _);
}

/// <summary>
/// Valida <see cref="OpcoesOperacaoAcesso"/> antes de conectar, com diagnóstico seguro (sem valores).
/// </summary>
public sealed class ValidadorOpcoesOperacaoAcesso : IValidateOptions<OpcoesOperacaoAcesso>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, OpcoesOperacaoAcesso options)
    {
        ArgumentNullException.ThrowIfNull(options);
        List<string> erros = [];

        if (string.IsNullOrWhiteSpace(options.LoginAutorizado) || options.LoginAutorizado.Length > 128)
        {
            erros.Add($"{OpcoesOperacaoAcesso.ChaveLoginAutorizado} é obrigatório (login PostgreSQL individual, 1 a 128 caracteres).");
        }

        if (!options.RetencaoValida)
        {
            erros.Add(
                $"{OpcoesOperacaoAcesso.ChaveRetencaoSessoesDias} deve ser um inteiro decimal entre " +
                $"{OpcoesOperacaoAcesso.RetencaoMinimaDias}–{OpcoesOperacaoAcesso.RetencaoMaximaDias} (inclusive) ou ausente (padrão {OpcoesOperacaoAcesso.RetencaoPadraoDias}).");
        }

        return erros.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(erros);
    }
}
