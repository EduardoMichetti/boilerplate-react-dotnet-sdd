namespace App.Api.Access;

using Platform.Application.Access.CasosUso;

/// <summary>
/// G31/D33: último filtro antes do handler das operações protegidas. A requisição que chega aqui
/// passou por tenant, marcador, sessão, autorização, instalação ativa e permissão: é “aceita” e
/// renova a inatividade via <see cref="CasoUsoRegistrarUsoSessao"/> (no máximo 1 escrita/minuto,
/// nunca <c>Set-Cookie</c>). Conflito é descartado; falha de banco propaga como
/// <c>ExcecaoDependenciaIndisponivel</c> (503 pelo <c>ConfiguracaoProblemDetails</c>) sem executar o handler.
/// </summary>
/// <param name="estado">Estado da requisição (sessão autenticada).</param>
/// <param name="registroUso">Caso de uso do registro de uso.</param>
public sealed class FiltroRegistroUsoSessao(EstadoRequisicaoAcesso estado, CasoUsoRegistrarUsoSessao registroUso) : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(next);
        if (estado.Sessao is not { } sessao)
        {
            return RegistroNegacaoSessao.Resultado(context.HttpContext);
        }

        _ = await registroUso.ExecutarAsync(sessao, context.HttpContext.RequestAborted);
        return await next(context);
    }
}
