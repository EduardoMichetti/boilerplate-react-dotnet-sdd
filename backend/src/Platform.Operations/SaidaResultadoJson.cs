namespace Platform.Operations;

using System.Text.Json;
using System.Text.Json.Serialization;
using Platform.Application.Access.Operacoes;
using Platform.Domain.Access;

/// <summary>
/// Formatador e emissor de saída em formato JSON estrito para stdout e diagnósticos para stderr (FR-033).
/// </summary>
public static class SaidaResultadoJson
{
    private static readonly JsonSerializerOptions OpcoesSerializacao = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    /// <summary>
    /// Formata o resultado da operação em JSON.
    /// </summary>
    /// <param name="resultado">Resultado da operação.</param>
    /// <returns>String JSON serializada.</returns>
    public static string Formatar(ResultadoOperacao resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        string resultadoTexto = resultado.Resultado switch
        {
            ResultadoOperacaoAcesso.Sucesso => "sucesso",
            ResultadoOperacaoAcesso.Negado => "negado",
            ResultadoOperacaoAcesso.Falha => "falha",
            _ => "falha"
        };

        DtoResultadoJson dto = new(
            resultado.IdOperacao,
            resultado.IdTenant,
            resultado.IdAmbiente,
            resultado.IdUsuario,
            resultado.LoginOperador,
            resultado.Operacao.ToString(),
            resultadoTexto,
            resultado.Codigo,
            resultado.RegistradaEm,
            resultado.RetencaoDias,
            resultado.InstanteCorte,
            resultado.QuantidadeRemovida);

        return JsonSerializer.Serialize(dto, OpcoesSerializacao);
    }

    /// <summary>
    /// Escreve o JSON em stdout e mensagem de diagnóstico em stderr se houver falha/negação.
    /// </summary>
    /// <param name="resultado">Resultado da operação.</param>
    public static void Escrever(ResultadoOperacao resultado)
    {
        ArgumentNullException.ThrowIfNull(resultado);

        Console.Out.WriteLine(Formatar(resultado));

        if (!resultado.Sucesso && !string.IsNullOrWhiteSpace(resultado.MensagemDiagnostico))
        {
            Console.Error.WriteLine($"[{resultado.Codigo}] {resultado.MensagemDiagnostico}");
        }
    }

    private sealed record DtoResultadoJson(
        Guid IdOperacao,
        Guid IdTenant,
        Guid IdAmbiente,
        Guid? IdUsuario,
        string LoginOperador,
        string Operacao,
        string Resultado,
        string Codigo,
        DateTimeOffset RegistradaEm,
        int? RetencaoDias,
        DateTimeOffset? InstanteCorte,
        int? QuantidadeRemovida);
}
