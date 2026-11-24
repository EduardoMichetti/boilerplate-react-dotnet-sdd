namespace App.Api.Access;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Platform.Application;
using Platform.Application.Access.CasosUso;
using Platform.Application.Access.Portas;
using Platform.Application.Auditoria;
using Platform.Application.Configuration;

/// <summary>
/// Referência protegida pelo handler nativo no cookie: <c>IdTenant/EpocaAcesso/IdSessao</c>.
/// </summary>
/// <param name="IdTenant">Tenant.</param>
/// <param name="EpocaAcesso">Época na emissão.</param>
/// <param name="IdSessao">Sessão.</param>
public sealed record ReferenciaSessao(Guid IdTenant, Guid EpocaAcesso, Guid IdSessao)
{
    /// <summary>
    /// Formata a referência gravada no cookie.
    /// </summary>
    /// <returns>Texto <c>IdTenant/EpocaAcesso/IdSessao</c>.</returns>
    public string Formatar() => $"{IdTenant:D}/{EpocaAcesso:D}/{IdSessao:D}";

    /// <summary>
    /// Lê a referência; formato inesperado não autentica.
    /// </summary>
    /// <param name="texto">Chave vinda do cookie.</param>
    /// <param name="referencia">Referência lida.</param>
    /// <returns><c>true</c> quando bem formada.</returns>
    public static bool TentarLer(string? texto, out ReferenciaSessao? referencia)
    {
        referencia = null;
        string[] partes = texto?.Split('/') ?? [];
        if (partes.Length != 3
            || !Guid.TryParseExact(partes[0], "D", out Guid tenant)
            || !Guid.TryParseExact(partes[1], "D", out Guid epoca)
            || !Guid.TryParseExact(partes[2], "D", out Guid sessao)
            || tenant == Guid.Empty || epoca == Guid.Empty || sessao == Guid.Empty)
        {
            return false;
        }

        referencia = new ReferenciaSessao(tenant, epoca, sessao);
        return true;
    }
}

/// <summary>
/// <see cref="ITicketStore"/> das sessões persistidas. Escopo resolvido por chamada pelo
/// <see cref="HttpContext"/> (sem capturar DbContext nas options). <c>RetrieveAsync</c> valida
/// referência, sessão, conta, lockout, stamp, época e <c>TetoAbsolutoEm</c> a cada operação; erros de
/// dependência propagam (nunca viram <c>null</c>). <c>RenewAsync</c> nunca faz upsert, reativação
/// ou extensão; <c>RemoveAsync</c> revoga. <c>StoreAsync</c> não é usado: a emissão é exclusiva do
/// login, com troca atômica e cookie escrito após o commit (G21).
/// </summary>
/// <param name="acessorContexto">Acesso ao contexto para as sobrecargas sem <see cref="HttpContext"/>.</param>
public sealed class ArmazenamentoTicketsSessao(IHttpContextAccessor acessorContexto) : ITicketStore
{
    /// <inheritdoc />
    public Task<string> StoreAsync(AuthenticationTicket ticket) =>
        throw new InvalidOperationException("Sessões são emitidas somente pelo login (troca atômica, G21).");

    /// <inheritdoc />
    public Task<string> StoreAsync(AuthenticationTicket ticket, CancellationToken cancellationToken) => StoreAsync(ticket);

    /// <inheritdoc />
    public Task<string> StoreAsync(AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken) => StoreAsync(ticket);

    /// <inheritdoc />
    public Task<AuthenticationTicket?> RetrieveAsync(string key) => RetrieveAsync(key, ContextoAtual(), CancellationToken.None);

    /// <inheritdoc />
    public Task<AuthenticationTicket?> RetrieveAsync(string key, CancellationToken cancellationToken) =>
        RetrieveAsync(key, ContextoAtual(), cancellationToken);

    /// <inheritdoc />
    public async Task<AuthenticationTicket?> RetrieveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        IServiceProvider servicos = httpContext.RequestServices;
        OpcoesInstalacaoTenant instalacao = servicos.GetRequiredService<IOptions<OpcoesInstalacaoTenant>>().Value;
        if (!ReferenciaSessao.TentarLer(key, out ReferenciaSessao? referencia) || referencia!.IdTenant != instalacao.IdTenant)
        {
            return null;
        }

        IRepositorioSessoes sessoes = servicos.GetRequiredService<IRepositorioSessoes>();
        EstadoSessaoPersistida? estado = await sessoes.ObterAsync(referencia.IdSessao, cancellationToken);
        if (estado?.TicketProtegido is null)
        {
            return null;
        }

        AuthenticationTicket ticket = servicos.GetRequiredService<ProtetorTicketSessao>().Desproteger(estado.TicketProtegido)
            ?? throw new ExcecaoDependenciaIndisponivel("O ticket persistido não pôde ser desprotegido pelo key ring da instalação.");

        EstadoRequisicaoAcesso estadoRequisicao = servicos.GetRequiredService<EstadoRequisicaoAcesso>();
        Guid epocaMarcador = estadoRequisicao.Marcador?.EpocaAcesso ?? await sessoes.ObterEpocaAcessoAsync(cancellationToken);
        DateTimeOffset agora = servicos.GetRequiredService<IContextoOperacao>().InstanteOperacao;

        ClaimsPrincipal principal = ticket.Principal;
        bool referenciasConferem =
            string.Equals(principal.FindFirstValue(ClaimsSessao.Sessao), referencia.IdSessao.ToString(), StringComparison.Ordinal)
            && string.Equals(principal.FindFirstValue(ClaimsSessao.Usuario), estado.IdUsuario.ToString(), StringComparison.Ordinal)
            && string.Equals(principal.FindFirstValue(ClaimsSessao.Tenant), instalacao.IdTenant.ToString(), StringComparison.Ordinal);

        if (!referenciasConferem
            || !ValidacaoSessaoAtual.EhValida(estado, referencia.EpocaAcesso, epocaMarcador, principal.FindFirstValue(ClaimsSessao.Carimbo), agora))
        {
            return null;
        }

        estadoRequisicao.Sessao = new SessaoCorrente(estado.IdSessao, estado.IdUsuario, estado.Versao, estado.TetoAbsolutoEm);
        return ticket;
    }

    /// <inheritdoc />
    public Task RenewAsync(string key, AuthenticationTicket ticket) => RenewAsync(key, ticket, ContextoAtual(), CancellationToken.None);

    /// <inheritdoc />
    public Task RenewAsync(string key, AuthenticationTicket ticket, CancellationToken cancellationToken) =>
        RenewAsync(key, ticket, ContextoAtual(), cancellationToken);

    /// <inheritdoc />
    public async Task RenewAsync(string key, AuthenticationTicket ticket, HttpContext httpContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(httpContext);
        IServiceProvider servicos = httpContext.RequestServices;
        SessaoCorrente? atual = servicos.GetRequiredService<EstadoRequisicaoAcesso>().Sessao;
        if (!ReferenciaSessao.TentarLer(key, out ReferenciaSessao? referencia) || atual is null || atual.IdSessao != referencia!.IdSessao)
        {
            return;
        }

        // Prazo é sempre o gravado: o ticket renovado nunca estende TetoAbsolutoEm.
        ticket.Properties.ExpiresUtc = atual.TetoAbsolutoEm;
        byte[] protegido = servicos.GetRequiredService<ProtetorTicketSessao>().ProtegerTicket(ticket);
        await servicos.GetRequiredService<IRepositorioSessoes>().RenovarAsync(atual.IdSessao, atual.Versao, protegido, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveAsync(string key) => RemoveAsync(key, ContextoAtual(), CancellationToken.None);

    /// <inheritdoc />
    public Task RemoveAsync(string key, CancellationToken cancellationToken) => RemoveAsync(key, ContextoAtual(), cancellationToken);

    /// <inheritdoc />
    public async Task RemoveAsync(string key, HttpContext httpContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        IServiceProvider servicos = httpContext.RequestServices;
        SessaoCorrente? atual = servicos.GetRequiredService<EstadoRequisicaoAcesso>().Sessao;
        if (!ReferenciaSessao.TentarLer(key, out ReferenciaSessao? referencia) || atual is null || atual.IdSessao != referencia!.IdSessao)
        {
            return;
        }

        _ = await servicos.GetRequiredService<IRepositorioSessoes>().RevogarAsync(atual.IdSessao, atual.Versao, cancellationToken);
    }

    private HttpContext ContextoAtual() =>
        acessorContexto.HttpContext ?? throw new InvalidOperationException("O store de sessões exige uma requisição HTTP.");
}
