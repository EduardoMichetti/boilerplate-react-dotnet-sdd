namespace Platform.Application.Access.Operacoes;

using Platform.Domain.Access;

/// <summary>
/// Resultado da execução de um comando operacional no host Platform.Operations (FR-033).
/// </summary>
public sealed record ResultadoOperacao(
    Guid IdOperacao,
    Guid IdTenant,
    Guid IdAmbiente,
    Guid? IdUsuario,
    string LoginOperador,
    TipoOperacaoAcesso Operacao,
    ResultadoOperacaoAcesso Resultado,
    string Codigo,
    DateTimeOffset RegistradaEm,
    int? RetencaoDias = null,
    DateTimeOffset? InstanteCorte = null,
    int? QuantidadeRemovida = null,
    string? MensagemDiagnostico = null)
{
    /// <summary>
    /// Indica se a operação foi executada com sucesso e persistida/auditada.
    /// </summary>
    public bool Sucesso => Resultado == ResultadoOperacaoAcesso.Sucesso;

    /// <summary>
    /// Fábrica para resultado de sucesso.
    /// </summary>
    public static ResultadoOperacao CriarSucesso(
        Guid idOperacao,
        Guid idTenant,
        Guid idAmbiente,
        Guid? idUsuario,
        string loginOperador,
        TipoOperacaoAcesso operacao,
        DateTimeOffset registradaEm,
        int? retencaoDias = null,
        DateTimeOffset? instanteCorte = null,
        int? quantidadeRemovida = null) =>
        new(
            idOperacao,
            idTenant,
            idAmbiente,
            idUsuario,
            loginOperador,
            operacao,
            ResultadoOperacaoAcesso.Sucesso,
            "SUCESSO",
            registradaEm,
            retencaoDias,
            instanteCorte,
            quantidadeRemovida);

    /// <summary>
    /// Fábrica para resultado negado antes de mutação.
    /// </summary>
    public static ResultadoOperacao CriarNegado(
        Guid idOperacao,
        Guid idTenant,
        Guid idAmbiente,
        Guid? idUsuario,
        string loginOperador,
        TipoOperacaoAcesso operacao,
        string codigo,
        DateTimeOffset registradaEm,
        string mensagemDiagnostico) =>
        new(
            idOperacao,
            idTenant,
            idAmbiente,
            idUsuario,
            loginOperador,
            operacao,
            ResultadoOperacaoAcesso.Negado,
            codigo,
            registradaEm,
            MensagemDiagnostico: mensagemDiagnostico);

    /// <summary>
    /// Fábrica para resultado com falha na execução.
    /// </summary>
    public static ResultadoOperacao CriarFalha(
        Guid idOperacao,
        Guid idTenant,
        Guid idAmbiente,
        Guid? idUsuario,
        string loginOperador,
        TipoOperacaoAcesso operacao,
        string codigo,
        DateTimeOffset registradaEm,
        string mensagemDiagnostico,
        int? retencaoDias = null,
        DateTimeOffset? instanteCorte = null,
        int? quantidadeRemovida = null) =>
        new(
            idOperacao,
            idTenant,
            idAmbiente,
            idUsuario,
            loginOperador,
            operacao,
            ResultadoOperacaoAcesso.Falha,
            codigo,
            registradaEm,
            retencaoDias,
            instanteCorte,
            quantidadeRemovida,
            mensagemDiagnostico);
}
