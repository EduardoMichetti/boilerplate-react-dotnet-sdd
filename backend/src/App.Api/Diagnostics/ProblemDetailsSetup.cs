using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Configuration;
using Platform.Domain.Tenancy;

namespace App.Api.Diagnostics;

public static class ProblemDetailsSetup
{
    public static IServiceCollection AddSafeProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails();
        services.AddExceptionHandler<SafeExceptionHandler>();
        return services;
    }

    public static IApplicationBuilder UseSafeProblemDetails(this IApplicationBuilder app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            // The handler records only safe metadata instead of exception messages or SQL.
            SuppressDiagnosticsCallback = static _ => true
        });
        app.UseStatusCodePages(async context =>
        {
            if (context.HttpContext.Response.StatusCode == StatusCodes.Status404NotFound)
            {
                await WriteAsync(context.HttpContext, StatusCodes.Status404NotFound);
            }
        });
        return app;
    }

    public static ProblemDetails Create(HttpContext context, int statusCode)
    {
        (string code, string title, string section) = statusCode switch
        {
            StatusCodes.Status404NotFound => ("NOT_FOUND", "Resource not found.", "15.5.5"),
            StatusCodes.Status503ServiceUnavailable => ("DEPENDENCY_UNAVAILABLE", "A required dependency is unavailable.", "15.6.4"),
            StatusCodes.Status500InternalServerError => ("INTERNAL_ERROR", "An unexpected error occurred.", "15.6.1"),
            _ => throw new ArgumentOutOfRangeException(nameof(statusCode))
        };
        ActivityTraceId traceId = Activity.Current?.TraceId ?? default;
        if (traceId == default)
        {
            traceId = ActivityTraceId.CreateRandom();
        }

        return new ProblemDetails
        {
            Status = statusCode,
            Type = $"https://www.rfc-editor.org/rfc/rfc9110#section-{section}",
            Title = title,
            Extensions =
            {
                ["code"] = code,
                ["traceId"] = traceId.ToHexString()
            }
        };
    }

    private static Task WriteAsync(HttpContext context, int statusCode)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(
            Create(context, statusCode),
            options: null,
            contentType: "application/problem+json",
            cancellationToken: context.RequestAborted);
    }

    private sealed class SafeExceptionHandler(ILogger<SafeExceptionHandler> logger) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            int statusCode = exception is InstallationConfigurationException or TenantIsolationException
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;
            ProblemDetails problem = Create(httpContext, statusCode);
            logger.LogError(
                new EventId(1000, "RequestFailed"),
                "Request failed. Code: {Code}; TraceId: {TraceId}; ExceptionType: {ExceptionType}",
                problem.Extensions["code"], problem.Extensions["traceId"], exception.GetType().FullName);
            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(
                problem,
                options: null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
            return true;
        }
    }
}
