namespace App.Api.OpenApi;

using System.Reflection;
using System.Text.Json.Nodes;
using App.Api.Access;
using App.Api.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using Platform.Application.Access;
using Platform.Domain.Resultados;

/// <summary>
/// Documento OpenAPI servido em <c>/openapi/v1.json</c> alinhado ao contrato L2 promovido
/// (<c>contracts/openapi.json</c>): título/versão L2, esquema de segurança por cookie,
/// <c>ProblemDetails</c> com o enum de códigos da plataforma e headers <c>X-Tenant-Id</c>/<c>X-CSRF-TOKEN</c>.
/// </summary>
public sealed class TransformadorDocumentoOpenApi : IOpenApiDocumentTransformer, IOpenApiOperationTransformer
{
    /// <summary>Nome do esquema de segurança por cookie.</summary>
    public const string EsquemaSeguranca = "SessionCookie";

    /// <summary>Nome do schema de erro.</summary>
    public const string SchemaProblema = "ProblemDetails";

    /// <summary>Versão do contrato L2 publicado.</summary>
    public const string VersaoContrato = "2.1.0";

    /// <summary>
    /// Códigos de erro emitidos pelas rotas do contrato: <see cref="ErrosAcesso"/> ∪ <see cref="ErrosPlataforma"/>.
    /// </summary>
    public static IReadOnlyList<string> CodigosPlataforma { get; } = LerCodigos();

    /// <inheritdoc />
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        document.Info.Title = "boilerplate-react-dotnet-sdd - L2 Tenant Access API";
        document.Info.Version = VersaoContrato;
        document.Info.Description =
            "Autenticação e acesso local ao tenant (L2). Rotas REST de recurso: GET /api/v1/antiforgery-token, " +
            "POST /api/v1/sessions (login, 201), DELETE /api/v1/sessions/current (logout, 204), GET /api/v1/me e GET /api/v1/context. " +
            "As cinco operações de acesso exigem X-Tenant-Id coincidente com a instalação; o header nunca seleciona conexão. " +
            "Sessão por cookie HttpOnly. Expiração por inatividade (padrão 30 min, 5–240) ou teto absoluto (padrão 600 min, 30–720), o que ocorrer primeiro, " +
            "ambos fixados na emissão; o uso renova só a inatividade. CSRF explícito e concessões lidas a cada operação.";
        document.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        document.Extensions["x-increment"] = new JsonNodeExtension(JsonValue.Create("I2"));

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[EsquemaSeguranca] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Cookie,
            Name = ConfiguracaoAutenticacaoSessao.CookieSessaoProducao,
            Description = "Referência tenant/epoch/session protegida pelo handler nativo, HttpOnly, Secure, SameSite=Lax, Path=/ e sem Domain. Somente Development HTTP localhost usa bps-session sem Secure.",
        };
        document.Security = [new OpenApiSecurityRequirement { [new OpenApiSecuritySchemeReference(EsquemaSeguranca, document)] = [] }];

        document.Components.Schemas ??= new Dictionary<string, IOpenApiSchema>();
        document.Components.Schemas[SchemaProblema] = CriarSchemaProblema();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);
        IList<object> metadados = context.Description.ActionDescriptor.EndpointMetadata;

        bool exigeSessao = metadados.OfType<IAuthorizeData>().Any();
        if (!exigeSessao)
        {
            operation.Security = [];
        }

        if (metadados.OfType<RotaAcessoL2>().FirstOrDefault() is { } rota)
        {
            operation.Parameters ??= [];
            operation.Parameters.Add(new OpenApiParameter
            {
                Name = MiddlewareCabecalhoTenant.NomeCabecalho,
                In = ParameterLocation.Header,
                Required = true,
                Description = "Um único UUID não vazio, igual ao tenant da instalação. Nunca seleciona conexão.",
                Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
            });

            bool exigeCsrf = rota.EhLogin || string.Equals(context.Description.HttpMethod, HttpMethods.Delete, StringComparison.OrdinalIgnoreCase);
            if (exigeCsrf)
            {
                operation.Parameters.Add(new OpenApiParameter
                {
                    Name = ConfiguracaoAutenticacaoSessao.CabecalhoCsrf,
                    In = ParameterLocation.Header,
                    Required = true,
                    Description = "Token obtido em GET /api/v1/antiforgery-token para a identidade corrente.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = 8192 },
                });
            }
        }

        operation.Responses ??= [];
        operation.Responses["default"] = new OpenApiResponse
        {
            Description = "500: INTERNAL_ERROR; falha registrada com correlação, sem detalhe interno.",
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["application/problem+json"] = new() { Schema = new OpenApiSchemaReference(SchemaProblema, context.Document) },
            },
        };
        return Task.CompletedTask;
    }

    private static OpenApiSchema CriarSchemaProblema()
    {
        OpenApiSchema texto(int? maximo = null) => new() { Type = JsonSchemaType.String, MaxLength = maximo };

        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            AdditionalPropertiesAllowed = false,
            Required = new HashSet<string>(StringComparer.Ordinal) { "type", "title", "status", "code", "traceId" },
            Properties = new Dictionary<string, IOpenApiSchema>(StringComparer.Ordinal)
            {
                ["type"] = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uri" },
                ["title"] = new OpenApiSchema { Type = JsonSchemaType.String, MinLength = 1, MaxLength = 200 },
                ["status"] = new OpenApiSchema { Type = JsonSchemaType.Integer, Minimum = "400", Maximum = "599" },
                ["detail"] = texto(1000),
                ["instance"] = texto(1000),
                ["code"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Description = "Catálogo de plataforma (ADR 0003): estável, nunca reaproveitado nem com significado alterado.",
                    Enum = CodigosPlataforma.Select(c => (JsonNode)JsonValue.Create(c)).ToList(),
                },
                ["traceId"] = new OpenApiSchema { Type = JsonSchemaType.String, Pattern = "^[0-9a-f]{32}$" },
                ["errors"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Object,
                    Description = "Somente em 400 INPUT_INVALID: caminho do campo em camelCase sem $.; corpo inteiro usa a chave \"$\".",
                    AdditionalProperties = new OpenApiSchema { Type = JsonSchemaType.Array, Items = texto() },
                },
            },
        };
    }

    private static List<string> LerCodigos()
    {
        IEnumerable<string> acesso = typeof(ErrosAcesso)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(c => c.FieldType == typeof(Erro))
            .Select(c => ((Erro)c.GetValue(null)!).Codigo);
        IEnumerable<string> plataforma = typeof(ErrosPlataforma)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(c => c.IsLiteral && c.FieldType == typeof(string))
            .Select(c => (string)c.GetRawConstantValue()!);
        return acesso.Concat(plataforma).Distinct(StringComparer.Ordinal).ToList();
    }
}

/// <summary>
/// Registro do documento OpenAPI L2.
/// </summary>
public static class ConfiguracaoOpenApiAcesso
{
    /// <summary>
    /// Registra o documento <c>v1</c> em OpenAPI 3.0 com o transformador L2.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarOpenApiAcesso(this IServiceCollection servicos)
    {
        servicos.AddOpenApi("v1", opcoes =>
        {
            opcoes.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
            opcoes.AddDocumentTransformer<TransformadorDocumentoOpenApi>();
            opcoes.AddOperationTransformer<TransformadorDocumentoOpenApi>();
        });
        return servicos;
    }
}
