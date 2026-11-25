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
/// L2.1 (D33): o registro de uso descarta conflito; revogações e troca no login que conflitam só
/// com registro de uso (sessão ainda ativa ao recarregar) repetem até
/// <see cref="IRepositorioSessoes.MaximoTentativasRevogacao"/> vezes; esgotado → 503.
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

        for (int tentativa = 1; ; tentativa++)
        {
            SessaoAutenticacao? sessaoAnterior = null;
            SessaoAutenticacao? sessao = null;
            await using IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync(tokenCancelamento);
            try
            {
                if (anterior is not null)
                {
                    sessaoAnterior = await contexto.Sessoes
                        .FirstOrDefaultAsync(s => s.IdSessao == anterior.IdSessao, tokenCancelamento);

                    // Revogada/expirada (disputa real): regra L2. Versão diferente com a sessão ainda
                    // ativa só pode vir de registro de uso concorrente (D33): segue com a versão atual.
                    if (sessaoAnterior is null || sessaoAnterior.FkIdUsuario != anterior.IdUsuario || !sessaoAnterior.EstaAtivaEm(agora))
                    {
                        await DesfazerAsync(transacao);
                        Desanexar(sessaoAnterior);
                        return ErrosAcesso.ConflitoSessao;
                    }

                    sessaoAnterior.RevogadaEm = agora;
                    sessaoAnterior.TicketProtegido = null;
                }

                sessao = new SessaoAutenticacao
                {
                    IdSessao = nova.IdSessao,
                    FkIdUsuario = nova.IdUsuario,
                    EpocaAcesso = nova.EpocaAcesso,
                    TetoAbsolutoEm = nova.TetoAbsolutoEm,
                    JanelaInatividadeMinutos = nova.JanelaInatividadeMinutos,
                    TicketProtegido = nova.TicketProtegido,

                    // Mesmo InstanteOperacao que o interceptador grava em CriadoEm (UltimoUsoEm = CriadoEm na emissão).
                    CriadoEm = agora,
                    UltimoUsoEm = agora,
                };
                sessao.Validar();
                contexto.Sessoes.Add(sessao);

                await contexto.SaveChangesAsync(tokenCancelamento);
                await transacao.CommitAsync(tokenCancelamento);
                return new SessaoEmitida(nova.IdSessao, nova.IdUsuario, instalacao.Value.IdTenant, nova.EpocaAcesso, nova.TetoAbsolutoEm, nova.JanelaInatividadeMinutos);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Rollback total (a nova sessão nunca fica); a próxima tentativa recarrega a anterior.
                await DesfazerAsync(transacao);
                Desanexar(sessaoAnterior, sessao);
                if (anterior is null)
                {
                    return ErrosAcesso.ConflitoSessao;
                }

                if (tentativa >= IRepositorioSessoes.MaximoTentativasRevogacao)
                {
                    throw new ExcecaoDependenciaIndisponivel("Troca de sessão esgotou as tentativas por conflito com registro de uso.");
                }
            }
            catch (DbUpdateException ex)
            {
                await DesfazerAsync(transacao);
                Desanexar(sessaoAnterior, sessao);
                throw new ExcecaoDependenciaIndisponivel("Falha ao persistir a troca de sessão.", ex);
            }
        }
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
                s.TetoAbsolutoEm,
                s.JanelaInatividadeMinutos,
                s.UltimoUsoEm,
                s.RevogadaEm,
                s.Versao,
                s.TicketProtegido,
                s.Usuario!.EstaAtivo,
                s.Usuario.LockoutEnd,
                s.Usuario.LockoutEnabled,
                s.Usuario.SecurityStamp))
            .FirstOrDefaultAsync(tokenCancelamento);

    /// <inheritdoc />
    public async Task<bool> RegistrarUsoAsync(Guid idSessao, long versaoEsperada, DateTimeOffset instante, CancellationToken tokenCancelamento)
    {
        SessaoAutenticacao? sessao = await contexto.Sessoes.FirstOrDefaultAsync(s => s.IdSessao == idSessao, tokenCancelamento);
        if (sessao is null
            || sessao.Versao != versaoEsperada
            || !sessao.EstaAtivaEm(instante)
            || !ExpiracaoSessao.DeveRegistrarUso(instante, sessao.UltimoUsoEm))
        {
            Desanexar(sessao);
            return false;
        }

        sessao.RegistrarUso(instante);
        try
        {
            await contexto.SaveChangesAsync(tokenCancelamento);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra requisição da mesma sessão já gravou (ou revogou): descartado, sem repetir.
            Desanexar(sessao);
            return false;
        }
        catch (DbUpdateException ex)
        {
            Desanexar(sessao);
            throw new ExcecaoDependenciaIndisponivel("Falha ao registrar o uso da sessão.", ex);
        }
    }

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
        DateTimeOffset agora = operacao.InstanteOperacao;
        for (int tentativa = 1; ; tentativa++)
        {
            SessaoAutenticacao? sessao = await contexto.Sessoes.FirstOrDefaultAsync(s => s.IdSessao == idSessao, tokenCancelamento);

            // Revogada/expirada: regra L2. Versão diferente da esperada com a sessão ainda ativa só
            // pode vir de registro de uso concorrente (D33): segue com a versão recarregada.
            if (sessao is null || !sessao.EstaAtivaEm(agora))
            {
                Desanexar(sessao);
                return ErrosAcesso.AutenticacaoNecessaria;
            }

            sessao.RevogadaEm = agora;
            sessao.TicketProtegido = null;
            try
            {
                await contexto.SaveChangesAsync(tokenCancelamento);
                return Resultado.Ok();
            }
            catch (DbUpdateConcurrencyException)
            {
                Desanexar(sessao);
                if (tentativa >= IRepositorioSessoes.MaximoTentativasRevogacao)
                {
                    throw new ExcecaoDependenciaIndisponivel("Revogação esgotou as tentativas por conflito com registro de uso.");
                }
            }
            catch (DbUpdateException ex)
            {
                Desanexar(sessao);
                throw new ExcecaoDependenciaIndisponivel("Falha ao revogar a sessão.", ex);
            }
        }
    }

    /// <inheritdoc />
    public async Task<int> RevogarTodasDaContaAsync(Guid idUsuario, CancellationToken tokenCancelamento)
    {
        DateTimeOffset agora = operacao.InstanteOperacao;
        for (int tentativa = 1; ; tentativa++)
        {
            List<SessaoAutenticacao> candidatas = await contexto.Sessoes
                .Where(s => s.FkIdUsuario == idUsuario && s.RevogadaEm == null && s.TetoAbsolutoEm > agora)
                .ToListAsync(tokenCancelamento);
            List<SessaoAutenticacao> ativas = candidatas.Where(s => s.EstaAtivaEm(agora)).ToList();

            foreach (SessaoAutenticacao sessao in ativas)
            {
                sessao.RevogadaEm = agora;
                sessao.TicketProtegido = null;
            }

            if (ativas.Count == 0)
            {
                return 0;
            }

            try
            {
                await contexto.SaveChangesAsync(tokenCancelamento);
                return ativas.Count;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Recarrega na próxima tentativa (dentro da transação do chamador): as já revogadas
                // ficam de fora; as que só tiveram registro de uso concorrente são revogadas de novo.
                foreach (SessaoAutenticacao sessao in candidatas)
                {
                    Desanexar(sessao);
                }

                if (tentativa >= IRepositorioSessoes.MaximoTentativasRevogacao)
                {
                    throw new ExcecaoDependenciaIndisponivel("Revogação das sessões da conta esgotou as tentativas por conflito com registro de uso.");
                }
            }
        }
    }

    private static async Task DesfazerAsync(IDbContextTransaction transacao)
    {
        // Rollback sem token: a desistência do cliente não pode impedir desfazer a transação.
        await transacao.RollbackAsync(CancellationToken.None);
    }

    private void Desanexar(params SessaoAutenticacao?[] sessoes)
    {
        foreach (SessaoAutenticacao? sessao in sessoes)
        {
            if (sessao is not null)
            {
                contexto.Entry(sessao).State = EntityState.Detached;
            }
        }
    }
}
