using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using App.Api.Diagnostics;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Platform.Application.Configuration;
using Platform.Domain.Tenancy;
using Xunit;

namespace Platform.ContractTests.Diagnostics;

public sealed class ProblemDetailsSetupTests
{
    [Fact]
    public async Task ApplicationSkeletonDoesNotExposeLaterPhaseEndpoints()
    {
        await using WebApplicationFactory<Program> application = new();
        using HttpClient client = application.CreateClient();

        foreach (string path in new[] { "/health/live", "/health/ready", "/openapi/v1.json", "/api/v1/auth/login" })
        {
            using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        }
    }

    [Theory]
    [InlineData("/missing", HttpStatusCode.NotFound, "NOT_FOUND")]
    [InlineData("/failure", HttpStatusCode.InternalServerError, "INTERNAL_ERROR")]
    [InlineData("/isolation", HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE")]
    [InlineData("/configuration", HttpStatusCode.ServiceUnavailable, "DEPENDENCY_UNAVAILABLE")]
    public async Task ErrorsUseSafeProblemDetailsEvenForHtmlAccept(
        string path, HttpStatusCode expectedStatus, string expectedCode)
    {
        const string privateValue = "synthetic-secret-must-not-leak";
        CapturingLoggerProvider logs = new();
        WebApplicationBuilder builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Production"
        });
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders().AddProvider(logs);
        builder.Services.AddSafeProblemDetails();
        await using WebApplication app = builder.Build();
        app.UseSafeProblemDetails();
        app.Run(context =>
        {
            switch (context.Request.Path.Value)
            {
                case "/failure":
                    throw new InvalidOperationException(privateValue);
                case "/isolation":
                    throw new TenantIsolationException();
                case "/configuration":
                    throw new InstallationConfigurationException(["ApplicationId is required."]);
                default:
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                    return Task.CompletedTask;
            }
        });
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = app.GetTestClient();
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        using HttpResponseMessage response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement problem = document.RootElement;

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal((int)expectedStatus, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.True(Uri.IsWellFormedUriString(problem.GetProperty("type").GetString(), UriKind.Absolute));
        Assert.Matches("^[0-9a-f]{32}$", problem.GetProperty("traceId").GetString()!);
        Assert.DoesNotContain(privateValue, body, StringComparison.Ordinal);
        Assert.False(problem.TryGetProperty("detail", out _));
        Assert.False(problem.TryGetProperty("exception", out _));
        Assert.All(logs.Messages, message => Assert.DoesNotContain(privateValue, message, StringComparison.Ordinal));

        if (expectedStatus != HttpStatusCode.NotFound)
        {
            Assert.Contains(logs.Messages, message => message.Contains(expectedCode, StringComparison.Ordinal));
        }
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

        public void Dispose() { }

        private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                messages.Enqueue(formatter(state, exception) + exception);
            }
        }
    }
}
