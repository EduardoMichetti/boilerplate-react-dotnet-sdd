namespace Platform.Infrastructure.Access;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Platform.Application;
using Platform.Application.Access;
using Platform.Application.Access.Portas;
using Platform.Application.Auditoria;
using Platform.Application.Configuration;
using Platform.Domain.Resultados;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Sessões persistidas com alterações rastreadas e <c>Versao</c> esperada (sem upsert nem
/// atualização em lote). Conflito de versão vira erro esperado; falha de banco vira 503.
/// </summary>
/// <param name="contexto">DbContext da requisição/comando.</param>
/// <param name="operacao">Instante único da operação.</param>
/// <param name="instalacao">Configuração da instalação.</param>
public sealed class RepositorioSessoes(
    AcessoTenantDbContext contexto,
    IContextoOperacao operacao,
    IOptions<OpcoesInstalacaoTenant> instalacao) : IRepositorioSessoes
{
    /// <inheritdoc />
    public async Task<Guid> ObterEpocaAcessoAsync(CancellationToken tokenCancelamento)
    {
        List<Guid> epocas = await contexto.IdentidadeBanco
            .AsNoTracking()
            .Where(e => e.IdUnico == IdentidadeBancoTenant.IdUnicoPadrao)
            .Select(e => e.EpocaAcesso)
            .ToListAsync(tokenCancelamento);

        return epocas.Count == 1 ? epocas[0] : throw new ExcecaoIsolamentoTenant();
    }

    /// <inheritdoc />
    public async Task<Resultado<SessaoEmitida>> EmitirAsync(NovaSessao nova, SessaoCorrente? anterior, CancellationToken tokenCancelamento)
    {
        ArgumentNullException.ThrowIfNull(nova);
        DateTimeOffset agora = operacao.InstanteOperacao;

        await using IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync(tokenCancelamento);
        try
        {
            if (anterior is not null)
            {
                SessaoAutenticacao? sessaoAnterior = await contexto.Sessoes
                    .FirstOrDefaultAsync(s => s.IdSessao == anterior.IdSessao, tokenCancelamento);
                if (sessaoAnterior is null || sessaoAnterior.Versao != anterior.Versao || !sessaoAnterior.EstaAtivaEm(agora))
                {
                    await DesfazerAsync(transacao);
                    return ErrosAcesso.ConflitoSessao;
                }

                contexto.Entry(sessaoAnterior).Property(s => s.Versao).OriginalValue = anterior.Versao;
                sessaoAnterior.RevogadaEm = agora;
                sessaoAnterior.TicketProtegido = null;
            }

            SessaoAutenticacao sessao = new()
            {
                IdSessao = nova.IdSessao,
                FkIdUsuario = nova.IdUsuario,
                EpocaAcesso = nova.EpocaAcesso,
                ExpiraEm = nova.ExpiraEm,
                TicketProtegido = nova.TicketProtegido,
            };
            sessao.Validar();
            contexto.Sessoes.Add(sessao);

            await contexto.SaveChangesAsync(tokenCancelamento);
            await transacao.CommitAsync(tokenCancelamento);
        }
        catch (DbUpdateConcurrencyException)
        {
            await DesfazerAsync(transacao);
            return ErrosAcesso.ConflitoSessao;
        }
        catch (DbUpdateException ex)
        {
            await DesfazerAsync(transacao);
            throw new ExcecaoDependenciaIndisponivel("Falha ao persistir a troca de sessão.", ex);
        }

        return new SessaoEmitida(nova.IdSessao, nova.IdUsuario, instalacao.Value.IdTenant, nova.EpocaAcesso, nova.ExpiraEm);
    }

    /// <inheritdoc />
    public Task<EstadoSessaoPersistida?> ObterAsync(Guid idSessao, CancellationToken tokenCancelamento) =>
        contexto.Sessoes
            .AsNoTracking()
            .Where(s => s.IdSessao == idSessao)
            .Select(s => new EstadoSessaoPersistida(
                s.IdSessao,
                s.FkIdUsuario,
                s.EpocaAcesso,
                s.ExpiraEm,
                s.RevogadaEm,
                s.Versao,
                s.TicketProtegido,
                s.Usuario!.EstaAtivo,
                s.Usuario.LockoutEnd,
                s.Usuario.LockoutEnabled,
                s.Usuario.SecurityStamp))
            .FirstOrDefaultAsync(tokenCancelamento);

    /// <inheritdoc />
    public async Task<bool> RenovarAsync(Guid idSessao, long versaoEsperada, byte[] ticketProtegido, CancellationToken tokenCancelamento)
    {
        ArgumentNullException.ThrowIfNull(ticketProtegido);
        SessaoAutenticacao? sessao = await contexto.Sessoes.FirstOrDefaultAsync(s => s.IdSessao == idSessao, tokenCancelamento);
        if (sessao is null || sessao.Versao != versaoEsperada || !sessao.EstaAtivaEm(operacao.InstanteOperacao))
        {
            return false;
        }

        if (sessao.TicketProtegido is not null && sessao.TicketProtegido.AsSpan().SequenceEqual(ticketProtegido))
        {
            return true;
        }

        contexto.Entry(sessao).Property(s => s.Versao).OriginalValue = versaoEsperada;
        sessao.TicketProtegido = ticketProtegido;
        try
        {
            await contexto.SaveChangesAsync(tokenCancelamento);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            contexto.Entry(sessao).State = EntityState.Detached;
            return false;
        }
        catch (DbUpdateException ex)
        {
            throw new ExcecaoDependenciaIndisponivel("Falha ao renovar a sessão.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<Resultado> RevogarAsync(Guid idSessao, long versaoEsperada, CancellationToken tokenCancelamento)
    {
        SessaoAutenticacao? sessao = await contexto.Sessoes.FirstOrDefaultAsync(s => s.IdSessao == idSessao, tokenCancelamento);
        if (sessao is null || sessao.Versao != versaoEsperada || !sessao.EstaAtivaEm(operacao.InstanteOperacao))
        {
            return ErrosAcesso.AutenticacaoNecessaria;
        }

        contexto.Entry(sessao).Property(s => s.Versao).OriginalValue = versaoEsperada;
        sessao.RevogadaEm = operacao.InstanteOperacao;
        sessao.TicketProtegido = null;
        try
        {
            await contexto.SaveChangesAsync(tokenCancelamento);
            return Resultado.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            contexto.Entry(sessao).State = EntityState.Detached;
            return ErrosAcesso.AutenticacaoNecessaria;
        }
        catch (DbUpdateException ex)
        {
            contexto.Entry(sessao).State = EntityState.Detached;
            throw new ExcecaoDependenciaIndisponivel("Falha ao revogar a sessão.", ex);
        }
    }

    /// <inheritdoc />
    public async Task<int> RevogarTodasDaContaAsync(Guid idUsuario, CancellationToken tokenCancelamento)
    {
        DateTimeOffset agora = operacao.InstanteOperacao;
        List<SessaoAutenticacao> ativas = await contexto.Sessoes
            .Where(s => s.FkIdUsuario == idUsuario && s.RevogadaEm == null && s.ExpiraEm > agora)
            .ToListAsync(tokenCancelamento);

        foreach (SessaoAutenticacao sessao in ativas)
        {
            sessao.RevogadaEm = agora;
            sessao.TicketProtegido = null;
        }

        if (ativas.Count > 0)
        {
            await contexto.SaveChangesAsync(tokenCancelamento);
        }

        return ativas.Count;
    }

    private static async Task DesfazerAsync(IDbContextTransaction transacao)
    {
        // Rollback sem token: a desistência do cliente não pode impedir desfazer a transação.
        await transacao.RollbackAsync(CancellationToken.None);
    }
}
