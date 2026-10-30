using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Configuration;
using Platform.Domain.Tenancy;

namespace App.Api.Diagnostics;

/// <summary>
/// Configuração de tratamento seguro de erros e formatação padronizada ProblemDetails conforme a RFC 9110.
/// </summary>
public static class ConfiguracaoProblemDetails
{
    /// <summary>
    /// Registra o middleware de tratamento de exceções seguras e ProblemDetails nos serviços.
    /// </summary>
    public static IServiceCollection AdicionarProblemDetailsSeguro(this IServiceCollection servicos)
    {
        servicos.AddProblemDetails();
        servicos.AddExceptionHandler<ManipuladorExcecaoSeguro>();
        return servicos;
    }

    /// <summary>
    /// Ativa o middleware de tratamento de exceções seguras e páginas de código de status na aplicação.
    /// </summary>
    public static IApplicationBuilder UsarProblemDetailsSeguro(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // O manipulador registra apenas metadados seguros, sem expor mensagens de exceções ou SQL.
            SuppressDiagnosticsCallback = static _ => true
        });
        app.UseStatusCodePages(async contexto =>
        {
            if (contexto.HttpContext.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                await EscreverAsync(contexto.HttpContext, StatusCodes.Status404NotFound);
            }
        });
        return app;
    }

    /// <summary>
    /// Constrói um objeto ProblemDetails com código e mensagem seguros para o status HTTP informado.
    /// </summary>
    public static ProblemDetails Criar(HttpContext contexto, int codigoStatus)
    {
        (string codigo, string titulo, string secao) = codigoStatus switch
        {
            StatusCodes.Status404NotFound => ("NOT_FOUND", "Recurso não encontrado.", "15.5.5"),
            StatusCodes.Status503ServiceUnavailable => ("DEPENDENCY_UNAVAILABLE", "Uma dependência obrigatória está indisponível.", "15.6.4"),
            StatusCodes.Status500InternalServerError => ("INTERNAL_ERROR", "Ocorreu um erro inesperado.", "15.6.1"),
            _ => throw new ArgumentOutOfRangeException(nameof(codigoStatus))
        };
        ActivityTraceId idRastreio = Activity.Current?.TraceId ?? default;
        if (idRastreio == default)
        {
            idRastreio = ActivityTraceId.CreateRandom();
        }

        return new ProblemDetails
        {
            Status = codigoStatus,
            Type = $"https://www.rfc-editor.org/rfc/rfc9110#section-{secao}",
            Title = titulo,
            Extensions =
            {
                ["code"] = codigo,
                ["traceId"] = idRastreio.ToHexString()
            }
        };
    }

    /// <summary>
    /// Escreve uma resposta HTTP padronizada em formato application/problem+json.
    /// </summary>
    public static Task EscreverAsync(HttpContext contexto, int codigoStatus)
    {
        contexto.Response.StatusCode = codigoStatus;
        return contexto.Response.WriteAsJsonAsync(
            Criar(contexto, codigoStatus),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: contexto.RequestAborted);
    }

    private sealed class ManipuladorExcecaoSeguro(ILogger<ManipuladorExcecaoSeguro> registrador) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext contextoHttp, Exception excecao, CancellationToken tokenCancelamento)
        {
            int codigoStatus = excecao is ExcecaoConfiguracaoInstalacao or ExcecaoIsolamentoInquilino
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;
            ProblemDetails problema = Criar(contextoHttp, codigoStatus);
            registrador.LogError(
                new EventId(1000, "RequisicaoFalhou"),
                "Falha na requisição. Código: {Codigo}; TraceId: {TraceId}; TipoExcecao: {TipoExcecao}",
                problema.Extensions["code"], problema.Extensions["traceId"], excecao.GetType().FullName);
            contextoHttp.Response.StatusCode = codigoStatus;
            await contextoHttp.Response.WriteAsJsonAsync(
                problema,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: tokenCancelamento);
            return true;
        }
    }
}
