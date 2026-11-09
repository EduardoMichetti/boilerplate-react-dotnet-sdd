namespace App.Api.Diagnostics;

/// <summary>
/// Catálogo único dos códigos de transporte/plataforma sem <c>TipoErro</c> (Constituição V:
/// declarados uma única vez). Usado por fábricas de Problem Details, middlewares e limiter.
/// </summary>
public static class ErrosPlataforma
{
    /// <summary>Entrada malformada (desserialização ou validação), 400 com <c>errors</c>.</summary>
    public const string EntradaInvalida = "INPUT_INVALID";

    /// <summary>Header <c>X-Tenant-Id</c> ausente, 400.</summary>
    public const string TenantObrigatorio = "TENANT_REQUIRED";

    /// <summary>Token antiforgery ausente ou inválido, 400.</summary>
    public const string CsrfInvalido = "CSRF_INVALID";

    /// <summary>Limite de tentativas de login excedido, 429 com <c>Retry-After</c>.</summary>
    public const string LimiteExcedido = "RATE_LIMITED";

    /// <summary>Dependência obrigatória indisponível ou marcador/versão divergente, 503.</summary>
    public const string DependenciaIndisponivel = "DEPENDENCY_UNAVAILABLE";

    /// <summary>Rota ou recurso inexistente, 404.</summary>
    public const string NaoEncontrado = "NOT_FOUND";

    /// <summary>Falha inesperada, 500.</summary>
    public const string ErroInterno = "INTERNAL_ERROR";
}
