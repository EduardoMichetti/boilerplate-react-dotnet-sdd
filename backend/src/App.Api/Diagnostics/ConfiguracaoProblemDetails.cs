using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Platform.Application;
using Platform.Application.Auditoria;
using Platform.Application.Configuration;
using Platform.Domain.Resultados;
using Platform.Domain.Tenancy;

namespace App.Api.Diagnostics;

/// <summary>
/// Tratamento seguro de erros em Problem Details (RFC 9457): fábricas por status, ponto único de
/// tradução <see cref="TipoErro"/> → status (<see cref="DeErro"/>), <c>Cache-Control: no-store</c>
/// em todo erro e conversão de entrada malformada em 400 <c>INPUT_INVALID</c> (ADR 0003).
/// </summary>
public static class ConfiguracaoProblemDetails
{
    /// <summary>
    /// Chave de <c>errors</c> para falha do corpo inteiro, sem campo identificável (G17).
    /// </summary>
    public const string ChaveCorpo = "$";

    private const string TipoConteudo = "application/problem+json";
    private const string MensagemCampoInvalido = "Valor ausente, inválido ou não reconhecido para este campo.";
    private const string MensagemCorpoInvalido = "O corpo da requisição não é um JSON válido para esta operação.";

    /// <summary>
    /// Registra Problem Details e o manipulador seguro de exceções.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarProblemDetailsSeguro(this IServiceCollection servicos)
    {
        servicos.AddProblemDetails();
        servicos.AddExceptionHandler<ManipuladorExcecaoSeguro>();
        return servicos;
    }

    /// <summary>
    /// Ativa o manipulador de exceções e a página de status 404 em Problem Details.
    /// </summary>
    /// <param name="app">Pipeline.</param>
    /// <returns>O mesmo pipeline.</returns>
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
    /// Problem Details de plataforma para 404, 500 ou 503.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <param name="codigoStatus">404, 500 ou 503.</param>
    /// <returns>Problema seguro.</returns>
    public static ProblemDetails Criar(HttpContext contexto, int codigoStatus) => codigoStatus switch
    {
        StatusCodes.Status404NotFound => Criar(codigoStatus, ErrosPlataforma.NaoEncontrado, "Recurso não encontrado."),
        StatusCodes.Status503ServiceUnavailable => Criar(codigoStatus, ErrosPlataforma.DependenciaIndisponivel, "Uma dependência obrigatória está indisponível."),
        StatusCodes.Status500InternalServerError => Criar(codigoStatus, ErrosPlataforma.ErroInterno, "Ocorreu um erro inesperado."),
        _ => throw new ArgumentOutOfRangeException(nameof(codigoStatus))
    };

    /// <summary>
    /// 400 <c>INPUT_INVALID</c> com <c>errors</c> por campo (chaves camelCase, sem <c>$.</c>).
    /// </summary>
    /// <param name="erros">Mensagens por campo, sem valores de entrada.</param>
    /// <returns>Problema.</returns>
    public static ProblemDetails EntradaInvalida(IDictionary<string, string[]> erros)
    {
        ProblemDetails problema = Criar(StatusCodes.Status400BadRequest, ErrosPlataforma.EntradaInvalida, "Entrada inválida.");
        problema.Extensions["errors"] = erros;
        return problema;
    }

    /// <summary>
    /// 400 <c>TENANT_REQUIRED</c>: header <c>X-Tenant-Id</c> ausente.
    /// </summary>
    /// <returns>Problema.</returns>
    public static ProblemDetails TenantObrigatorio() =>
        Criar(StatusCodes.Status400BadRequest, ErrosPlataforma.TenantObrigatorio, "O header X-Tenant-Id é obrigatório.");

    /// <summary>
    /// 400 <c>CSRF_INVALID</c>: token antiforgery ausente ou inválido para a identidade corrente.
    /// </summary>
    /// <returns>Problema.</returns>
    public static ProblemDetails CsrfInvalido() =>
        Criar(StatusCodes.Status400BadRequest, ErrosPlataforma.CsrfInvalido, "Token antiforgery ausente ou inválido.");

    /// <summary>
    /// 429 <c>RATE_LIMITED</c>.
    /// </summary>
    /// <returns>Problema.</returns>
    public static ProblemDetails LimiteExcedido() =>
        Criar(StatusCodes.Status429TooManyRequests, ErrosPlataforma.LimiteExcedido, "Muitas tentativas; aguarde para tentar novamente.");

    /// <summary>
    /// Ponto único de tradução de <see cref="Erro"/> em Problem Details:
    /// NaoAutenticado → 401, AcessoNegado → 403, NaoEncontrado → 404, Conflito → 409, RegraNegocio → 422.
    /// </summary>
    /// <param name="erro">Erro esperado do catálogo.</param>
    /// <returns>Problema.</returns>
    public static ProblemDetails DeErro(Erro erro)
    {
        ArgumentNullException.ThrowIfNull(erro);
        int status = erro.Tipo switch
        {
            TipoErro.NaoAutenticado => StatusCodes.Status401Unauthorized,
            TipoErro.AcessoNegado => StatusCodes.Status403Forbidden,
            TipoErro.NaoEncontrado => StatusCodes.Status404NotFound,
            TipoErro.Conflito => StatusCodes.Status409Conflict,
            TipoErro.RegraNegocio => StatusCodes.Status422UnprocessableEntity,
            _ => throw new ArgumentOutOfRangeException(nameof(erro))
        };
        return Criar(status, erro.Codigo, erro.Mensagem);
    }

    /// <summary>
    /// Resultado de endpoint que escreve o problema com <c>Cache-Control: no-store</c>.
    /// </summary>
    /// <param name="problema">Problema.</param>
    /// <param name="segundosRetry">Valor de <c>Retry-After</c>, quando aplicável (429).</param>
    /// <returns>Resultado HTTP.</returns>
    public static IResult Resultado(ProblemDetails problema, int? segundosRetry = null) =>
        new ResultadoProblema(problema, segundosRetry);

    /// <summary>
    /// Resultado de endpoint para um erro do catálogo.
    /// </summary>
    /// <param name="erro">Erro esperado.</param>
    /// <returns>Resultado HTTP.</returns>
    public static IResult Resultado(Erro erro) => Resultado(DeErro(erro));

    /// <summary>
    /// Escreve um problema de plataforma (404, 500 ou 503).
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <param name="codigoStatus">Status.</param>
    /// <returns>Tarefa da escrita.</returns>
    public static Task EscreverAsync(HttpContext contexto, int codigoStatus) =>
        EscreverAsync(contexto, Criar(contexto, codigoStatus));

    /// <summary>
    /// Escreve um problema com status, <c>Cache-Control: no-store</c> e <c>Retry-After</c> opcional.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <param name="problema">Problema.</param>
    /// <param name="segundosRetry">Segundos para <c>Retry-After</c>.</param>
    /// <returns>Tarefa da escrita.</returns>
    public static Task EscreverAsync(HttpContext contexto, ProblemDetails problema, int? segundosRetry = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ArgumentNullException.ThrowIfNull(problema);
        contexto.Response.StatusCode = problema.Status ?? StatusCodes.Status500InternalServerError;
        contexto.Response.Headers.CacheControl = "no-store";
        if (segundosRetry is { } segundos)
        {
            contexto.Response.Headers.RetryAfter = segundos.ToString(CultureInfo.InvariantCulture);
        }

        return contexto.Response.WriteAsJsonAsync(problema, options: null, contentType: TipoConteudo, cancellationToken: contexto.RequestAborted);
    }

    /// <summary>
    /// Converte o caminho de uma <see cref="JsonException"/> na chave de <c>errors</c>:
    /// <c>$.itens[0].nome</c> → <c>itens[0].nome</c>; raiz (<c>$</c>) → <see cref="ChaveCorpo"/>.
    /// </summary>
    /// <param name="caminho">Caminho da exceção.</param>
    /// <returns>Chave do campo.</returns>
    public static string ChaveDeCaminhoJson(string? caminho)
    {
        if (string.IsNullOrEmpty(caminho) || caminho == "$")
        {
            return ChaveCorpo;
        }

        return caminho.StartsWith("$.", StringComparison.Ordinal) ? caminho[2..] : caminho.TrimStart('$');
    }

    private static ProblemDetails Criar(int codigoStatus, string codigo, string titulo)
    {
        ActivityTraceId idRastreio = Activity.Current?.TraceId ?? default;
        if (idRastreio == default)
        {
            idRastreio = ActivityTraceId.CreateRandom();
        }

        return new ProblemDetails
        {
            Status = codigoStatus,
            Type = TipoProblema(codigoStatus),
            Title = titulo,
            Extensions =
            {
                ["code"] = codigo,
                ["traceId"] = idRastreio.ToHexString()
            }
        };
    }

    private static string TipoProblema(int codigoStatus) => codigoStatus switch
    {
        StatusCodes.Status400BadRequest => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.1",
        StatusCodes.Status401Unauthorized => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.2",
        StatusCodes.Status403Forbidden => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.4",
        StatusCodes.Status404NotFound => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.5",
        StatusCodes.Status409Conflict => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.10",
        StatusCodes.Status422UnprocessableEntity => "https://www.rfc-editor.org/rfc/rfc9110#section-15.5.21",
        StatusCodes.Status429TooManyRequests => "https://www.rfc-editor.org/rfc/rfc6585#section-4",
        StatusCodes.Status503ServiceUnavailable => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.4",
        _ => "https://www.rfc-editor.org/rfc/rfc9110#section-15.6.1"
    };

    private sealed class ResultadoProblema(ProblemDetails problema, int? segundosRetry) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext) => EscreverAsync(httpContext, problema, segundosRetry);
    }

    private sealed class ManipuladorExcecaoSeguro(ILogger<ManipuladorExcecaoSeguro> registrador) : IExceptionHandler
    {
        public async ValueTask<bool> TryHandleAsync(
            HttpContext contextoHttp, Exception excecao, CancellationToken tokenCancelamento)
        {
            if (excecao is BadHttpRequestException requisicaoInvalida)
            {
                // Entrada malformada: 400 sem ecoar valor nem mensagem original e sem LogError.
                string chave = requisicaoInvalida.InnerException is JsonException json
                    ? ChaveDeCaminhoJson(json.Path)
                    : ChaveCorpo;
                string mensagem = chave == ChaveCorpo ? MensagemCorpoInvalido : MensagemCampoInvalido;
                ProblemDetails entrada = EntradaInvalida(new Dictionary<string, string[]> { [chave] = [mensagem] });
                registrador.LogInformation(
                    new EventId(1001, "EntradaMalformada"),
                    "Entrada malformada rejeitada. Campo: {Campo}; TraceId: {TraceId}",
                    chave, entrada.Extensions["traceId"]);
                await EscreverAsync(contextoHttp, entrada);
                return true;
            }

            int codigoStatus = excecao is ExcecaoConfiguracaoInstalacao or ExcecaoIsolamentoTenant or ExcecaoDependenciaIndisponivel or DbException
                ? StatusCodes.Status503ServiceUnavailable
                : StatusCodes.Status500InternalServerError;
            ProblemDetails problema = Criar(contextoHttp, codigoStatus);
            Guid? idOperacao = contextoHttp.RequestServices.GetService<IContextoOperacao>()?.IdOperacao;
            registrador.LogError(
                new EventId(1000, "RequisicaoFalhou"),
                "Falha na requisição. Código: {Codigo}; TraceId: {TraceId}; IdOperacao: {IdOperacao}; TipoExcecao: {TipoExcecao}",
                problema.Extensions["code"], problema.Extensions["traceId"], idOperacao, excecao.GetType().FullName);
            await EscreverAsync(contextoHttp, problema);
            return true;
        }
    }
}
