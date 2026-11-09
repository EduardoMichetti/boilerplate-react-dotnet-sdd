namespace Platform.Application.Validacao;

using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Resolve o nome publicado de uma propriedade para as chaves de <c>errors</c> do FluentValidation:
/// <see cref="JsonPropertyNameAttribute"/> quando presente; senão camelCase (ADR 0003 D6).
/// </summary>
public static class ResolvedorNomePropriedadeJson
{
    /// <summary>
    /// Função compatível com <c>ValidatorOptions.Global.PropertyNameResolver</c>.
    /// </summary>
    /// <param name="tipo">Tipo validado.</param>
    /// <param name="membro">Membro da regra.</param>
    /// <param name="expressao">Expressão da regra.</param>
    /// <returns>Nome JSON do membro, ou nulo quando não há membro.</returns>
    public static string? Resolver(Type tipo, MemberInfo? membro, LambdaExpression? expressao)
    {
        if (membro is null)
        {
            return null;
        }

        return membro.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name
            ?? JsonNamingPolicy.CamelCase.ConvertName(membro.Name);
    }
}
