namespace App.Api.Access;

using App.Api.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Platform.Application.Access;
using Platform.Application.Access.Portas;
using Platform.Application.Auditoria;

/// <summary>
/// Implementação scoped de <see cref="IContextoNegacaoSessao"/>: guarda o motivo da negação da
/// sessão na requisição corrente.
/// </summary>
public sealed class ContextoNegacaoSessao : IContextoNegacaoSessao
{
    /// <inheritdoc />
    public MotivoNegacaoSessao? Motivo { get; private set; }

    /// <inheritdoc />
    public void Registrar(MotivoNegacaoSessao motivo) => Motivo = motivo;
}

/// <summary>
/// Ponto único da resposta 401 <c>AUTH_REQUIRED</c> de sessão (D37): resposta genérica idêntica em
/// todos os motivos e <c>EventId(1002, "SessaoNegada")</c> <c>Information</c> com <c>Motivo</c>,
/// <c>TraceId</c> (o mesmo da resposta) e <c>IdOperacao</c>, sem id de sessão/conta, ticket, cookie,
/// email ou IP.
/// </summary>
public static partial class RegistroNegacaoSessao
{
    /// <summary>
    /// Escreve o 401 genérico e registra o motivo.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <param name="motivoPadrao">Motivo quando a autenticação não registrou nenhum (ex.: mudança posterior detectada pelo caso de uso).</param>
    /// <returns>Tarefa da escrita.</returns>
    public static Task EscreverAsync(HttpContext contexto, MotivoNegacaoSessao? motivoPadrao = null)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        ProblemDetails problema = Registrar(contexto, motivoPadrao);
        return ConfiguracaoProblemDetails.EscreverAsync(contexto, problema);
    }

    /// <summary>
    /// Resultado de endpoint com o 401 genérico; registra o motivo.
    /// </summary>
    /// <param name="contexto">Contexto HTTP.</param>
    /// <param name="motivoPadrao">Motivo quando a autenticação não registrou nenhum.</param>
    /// <returns>Resultado HTTP.</returns>
    public static IResult Resultado(HttpContext contexto, MotivoNegacaoSessao? motivoPadrao = MotivoNegacaoSessao.Invalida)
    {
        ArgumentNullException.ThrowIfNull(contexto);
        return ConfiguracaoProblemDetails.Resultado(Registrar(contexto, motivoPadrao));
    }

    private static ProblemDetails Registrar(HttpContext contexto, MotivoNegacaoSessao? motivoPadrao)
    {
        IServiceProvider servicos = contexto.RequestServices;
        MotivoNegacaoSessao motivo = servicos.GetRequiredService<IContextoNegacaoSessao>().Motivo
            ?? motivoPadrao
            ?? (PossuiCookieSessao(contexto) ? MotivoNegacaoSessao.Invalida : MotivoNegacaoSessao.Ausente);

        ProblemDetails problema = ConfiguracaoProblemDetails.DeErro(ErrosAcesso.AutenticacaoNecessaria);
        Guid? idOperacao = servicos.GetService<IContextoOperacao>()?.IdOperacao;
        ILogger registrador = servicos.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(RegistroNegacaoSessao).FullName!);
        RegistrarSessaoNegada(registrador, motivo, (string)problema.Extensions["traceId"]!, idOperacao);
        return problema;
    }

    private static bool PossuiCookieSessao(HttpContext contexto) =>
        contexto.Request.Cookies.ContainsKey(ConfiguracaoAutenticacaoSessao.CookieSessaoProducao)
        || contexto.Request.Cookies.ContainsKey(ConfiguracaoAutenticacaoSessao.CookieSessaoDesenvolvimento);

    [LoggerMessage(EventId = 1002, EventName = "SessaoNegada", Level = LogLevel.Information,
        Message = "Sessão negada. Motivo: {Motivo}; TraceId: {TraceId}; IdOperacao: {IdOperacao}")]
    private static partial void RegistrarSessaoNegada(ILogger registrador, MotivoNegacaoSessao motivo, string traceId, Guid? idOperacao);
}
