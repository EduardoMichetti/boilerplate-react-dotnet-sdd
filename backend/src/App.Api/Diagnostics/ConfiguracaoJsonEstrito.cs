using System.Globalization;
using System.Text.Json.Serialization;
using FluentValidation;
using Microsoft.AspNetCore.Http.Json;
using Platform.Application.Access.Contratos;
using Platform.Application.Validacao;

namespace App.Api.Diagnostics;

/// <summary>
/// Desserialização estrita (ADR 0003 D7) e registro da validação de entrada (D2) em todos os ambientes.
/// </summary>
public static class ConfiguracaoJsonEstrito
{
    /// <summary>
    /// JSON estrito: campo desconhecido, tipo incompatível e <c>null</c> em campo obrigatório viram 400;
    /// falha de binding lança <see cref="BadHttpRequestException"/> também fora de Development.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarJsonEstrito(this IServiceCollection servicos)
    {
        servicos.Configure<JsonOptions>(opcoes =>
        {
            opcoes.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
            opcoes.SerializerOptions.RespectNullableAnnotations = true;
            opcoes.SerializerOptions.RespectRequiredConstructorParameters = true;
        });
        servicos.Configure<RouteHandlerOptions>(opcoes => opcoes.ThrowOnBadRequest = true);
        return servicos;
    }

    /// <summary>
    /// Registra os validadores <c>Validador&lt;Requisicao*&gt;</c> e o resolvedor de nomes JSON das chaves de <c>errors</c>.
    /// Deve ser chamado antes de qualquer validador ser construído (o nome é fixado na criação da regra).
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarValidacaoRequisicoes(this IServiceCollection servicos)
    {
        ValidatorOptions.Global.PropertyNameResolver = ResolvedorNomePropriedadeJson.Resolver;
        ValidatorOptions.Global.LanguageManager.Culture = CultureInfo.GetCultureInfo("pt-BR");
        servicos.AddValidatorsFromAssemblyContaining<ValidadorRequisicaoLogin>(ServiceLifetime.Singleton);
        return servicos;
    }
}
