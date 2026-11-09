using FluentValidation;
using FluentValidation.Results;

namespace App.Api.Diagnostics;

/// <summary>
/// Filtro do grupo <c>/api/v1</c>: valida cada argumento <c>Requisicao*</c> pelo seu
/// <see cref="IValidator{T}"/> e responde 400 <c>INPUT_INVALID</c> com <c>errors</c> por campo,
/// antes de qualquer lookup de conta. Requisição sem validador registrado é falha de composição.
/// </summary>
public sealed class FiltroValidacaoRequisicao : IEndpointFilter
{
    /// <summary>
    /// Prefixo que identifica DTOs de entrada (ADR 0003 D2).
    /// </summary>
    public const string PrefixoRequisicao = "Requisicao";

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);

        foreach (object? argumento in context.Arguments)
        {
            if (argumento is null || !argumento.GetType().Name.StartsWith(PrefixoRequisicao, StringComparison.Ordinal))
            {
                continue;
            }

            Type tipoValidador = typeof(IValidator<>).MakeGenericType(argumento.GetType());
            IValidator validador = context.HttpContext.RequestServices.GetService(tipoValidador) as IValidator
                ?? throw new InvalidOperationException($"Nenhum validador registrado para {argumento.GetType().Name}.");

            ValidationResult resultado = await validador.ValidateAsync(
                new ValidationContext<object>(argumento),
                context.HttpContext.RequestAborted);

            if (!resultado.IsValid)
            {
                Dictionary<string, string[]> erros = resultado.Errors
                    .GroupBy(e => e.PropertyName, StringComparer.Ordinal)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).Distinct(StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
                return ConfiguracaoProblemDetails.Resultado(ConfiguracaoProblemDetails.EntradaInvalida(erros));
            }
        }

        return await next(context);
    }
}
