namespace App.Api.Access;

using System.Reflection;
using App.Api.Diagnostics;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.Extensions.Options;
using Platform.Application.Access;
using Platform.Application.Access.CasosUso;
using Platform.Application.Access.Contratos;
using Platform.Application.Access.Portas;
using Platform.Application.Configuration;
using Platform.Domain.Resultados;

/// <summary>
/// As cinco rotas L2 de <c>/api/v1</c> (G15). Ordem G2: limiter (login) → tenant → marcador →
/// autenticação/autorização → corpo + FluentValidation (login) → CSRF → estado → credenciais.
/// </summary>
public static class EndpointsAcesso
{
    /// <summary>
    /// Versão informativa do <c>App.Api</c> publicada em <c>applicationVersion</c>.
    /// </summary>
    public static readonly string VersaoAplicacao =
        typeof(EndpointsAcesso).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(EndpointsAcesso).Assembly.GetName().Version?.ToString()
        ?? "0.0.0";

    private const string ValorNoStore = "no-store";

    /// <summary>
    /// Registra os casos de uso das rotas L2.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarCasosUsoAcesso(this IServiceCollection servicos)
    {
        servicos.AddScoped<CasoUsoLogin>();
        servicos.AddScoped<CasoUsoLogout>();
        servicos.AddScoped<CasoUsoConsultarIdentidade>();
        servicos.AddScoped<CasoUsoConsultarContexto>();
        return servicos;
    }

    /// <summary>
    /// Mapeia as rotas L2 no grupo <c>/api/v1</c>.
    /// </summary>
    /// <param name="grupo">Grupo <c>/api/v1</c> com o filtro de validação.</param>
    /// <returns>O mesmo grupo.</returns>
    public static RouteGroupBuilder MapearEndpointsAcesso(this RouteGroupBuilder grupo)
    {
        grupo.MapGet("/antiforgery-token", ObterTokenAntiforgery)
            .WithMetadata(new RotaAcessoL2())
            .WithName("getAntiforgeryToken")
            .WithTags("Authentication")
            .WithSummary("Obter token de requisição antiforgery")
            .Produces<RespostaTokenAntiforgery>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        grupo.MapPost("/sessions", CriarSessaoAsync)
            .WithMetadata(new RotaAcessoL2(EhLogin: true))
            .RequireRateLimiting(ConfiguracaoLimiteLogin.PoliticaLogin)
            .WithName("createSession")
            .WithTags("Authentication")
            .WithSummary("Criar sessão (login) para identidade previamente preparada pelo operador")
            .Accepts<RequisicaoLogin>("application/json")
            .Produces<RespostaLogin>(StatusCodes.Status201Created, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        grupo.MapDelete("/sessions/current", EncerrarSessaoAsync)
            .WithMetadata(new RotaAcessoL2())
            .RequireAuthorization(ConfiguracaoAutenticacaoSessao.PoliticaRecursoProprio)
            .WithName("deleteCurrentSession")
            .WithTags("Authentication")
            .WithSummary("Encerrar a sessão corrente (logout) e eliminar seu cookie")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        grupo.MapGet("/me", ConsultarIdentidadeAsync)
            .WithMetadata(new RotaAcessoL2())
            .RequireAuthorization(ConfiguracaoAutenticacaoSessao.PoliticaRecursoProprio)
            .WithName("getCurrentUser")
            .WithTags("Identity")
            .WithSummary("Consultar identidade e validade da própria sessão")
            .Produces<RespostaUsuarioAtual>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        grupo.MapGet("/context", ConsultarContextoAsync)
            .WithMetadata(new RotaAcessoL2())
            .RequireAuthorization(ConfiguracaoAutenticacaoSessao.PoliticaRecursoProprio)
            .WithName("getTenantContext")
            .WithTags("TenantContext")
            .WithSummary("Consultar o próprio contexto de acesso no tenant validado")
            .Produces<RespostaContextoTenant>(StatusCodes.Status200OK, "application/json")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return grupo;
    }

    private static IResult ObterTokenAntiforgery(HttpContext contexto, IAntiforgery antiforgery)
    {
        // Cookie inválido já foi tratado como anônimo; falha de dependência propagou como 503.
        AntiforgeryTokenSet tokens = antiforgery.GetAndStoreTokens(contexto);
        SemCache(contexto);
        return Results.Ok(new RespostaTokenAntiforgery(tokens.RequestToken!, ConfiguracaoAutenticacaoSessao.CabecalhoCsrf));
    }

    private static async Task<IResult> CriarSessaoAsync(
        RequisicaoLogin corpo,
        HttpContext contexto,
        IAntiforgery antiforgery,
        CasoUsoLogin casoUso,
        EstadoRequisicaoAcesso estado,
        EmissorCookieSessao emissor,
        CancellationToken tokenCancelamento)
    {
        if (!await CsrfValidoAsync(contexto, antiforgery))
        {
            return ConfiguracaoProblemDetails.Resultado(ConfiguracaoProblemDetails.CsrfInvalido());
        }

        Resultado<SessaoEmitida> resultado = await casoUso.ExecutarAsync(corpo, estado.Sessao, tokenCancelamento);
        if (!resultado.EhSucesso)
        {
            return ConfiguracaoProblemDetails.Resultado(resultado.Erro!);
        }

        // Somente após o commit da troca: cookie novo com a chave nova (G21).
        SessaoEmitida sessao = resultado.Valor;
        emissor.Emitir(contexto, new ReferenciaSessao(sessao.IdTenant, sessao.EpocaAcesso, sessao.IdSessao), sessao.TetoAbsolutoEm);
        SemCache(contexto);
        return Results.Created("/api/v1/sessions/current", new RespostaLogin(sessao.IdTenant, sessao.TetoAbsolutoEm));
    }

    private static async Task<IResult> EncerrarSessaoAsync(
        HttpContext contexto,
        IAntiforgery antiforgery,
        CasoUsoLogout casoUso,
        EstadoRequisicaoAcesso estado,
        EmissorCookieSessao emissor,
        CancellationToken tokenCancelamento)
    {
        if (!await CsrfValidoAsync(contexto, antiforgery))
        {
            return ConfiguracaoProblemDetails.Resultado(ConfiguracaoProblemDetails.CsrfInvalido());
        }

        if (estado.Sessao is not { } sessao)
        {
            return ConfiguracaoProblemDetails.Resultado(ErrosAcesso.AutenticacaoNecessaria);
        }

        Resultado resultado = await casoUso.ExecutarAsync(sessao, tokenCancelamento);
        if (!resultado.EhSucesso)
        {
            return ConfiguracaoProblemDetails.Resultado(resultado.Erro!);
        }

        // Revogação confirmada: só então remove o cookie e responde 204.
        emissor.Remover(contexto);
        SemCache(contexto);
        return Results.NoContent();
    }

    private static async Task<IResult> ConsultarIdentidadeAsync(
        HttpContext contexto,
        CasoUsoConsultarIdentidade casoUso,
        EstadoRequisicaoAcesso estado,
        CancellationToken tokenCancelamento)
    {
        if (estado.Sessao is not { } sessao)
        {
            return ConfiguracaoProblemDetails.Resultado(ErrosAcesso.AutenticacaoNecessaria);
        }

        Resultado<IdentidadeAtual> resultado = await casoUso.ExecutarAsync(sessao, tokenCancelamento);
        if (!resultado.EhSucesso)
        {
            return ConfiguracaoProblemDetails.Resultado(resultado.Erro!);
        }

        SemCache(contexto);
        return Results.Ok(ParaResposta(resultado.Valor));
    }

    private static async Task<IResult> ConsultarContextoAsync(
        HttpContext contexto,
        CasoUsoConsultarContexto casoUso,
        EstadoRequisicaoAcesso estado,
        CancellationToken tokenCancelamento)
    {
        if (estado.Sessao is not { } sessao)
        {
            return ConfiguracaoProblemDetails.Resultado(ErrosAcesso.AutenticacaoNecessaria);
        }

        Resultado<ContextoAcesso> resultado = await casoUso.ExecutarAsync(sessao, tokenCancelamento);
        if (!resultado.EhSucesso)
        {
            return ConfiguracaoProblemDetails.Resultado(resultado.Erro!);
        }

        ContextoAcesso dados = resultado.Valor;
        SemCache(contexto);
        return Results.Ok(new RespostaContextoTenant(
            ParaResposta(dados.Usuario),
            new RespostaResumoTenant(dados.IdTenant, dados.NomeExibicaoTenant),
            dados.Permissoes,
            VersaoAplicacao));
    }

    private static RespostaUsuarioAtual ParaResposta(IdentidadeAtual identidade) =>
        new(identidade.IdUsuario, identidade.IdTenant, identidade.Email, identidade.NomeExibicao, identidade.SessaoExpiraEm);

    private static async Task<bool> CsrfValidoAsync(HttpContext contexto, IAntiforgery antiforgery)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(contexto);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static void SemCache(HttpContext contexto) => contexto.Response.Headers.CacheControl = ValorNoStore;
}
