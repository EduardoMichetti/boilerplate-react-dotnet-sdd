namespace Platform.Infrastructure.Access.Operacoes;

using Microsoft.EntityFrameworkCore;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Repositório para remoção rastreada em lote de sessões encerradas além da retenção (<c>sessao limpar</c>).
/// Elegibilidade pela expiração efetiva (L2.1, D31) ou pela revogação; projeta somente <c>IdSessao</c> e
/// <c>Versao</c>, sem carregar tickets ou contas, e remove via entidades anexadas com a versão esperada.
/// </summary>
public sealed class RepositorioLimpezaSessoes(AcessoTenantDbContext contexto) : IRepositorioLimpezaSessoes
{
    /// <inheritdoc />
    /// <remarks>
    /// O mínimo <c>LEAST(UltimoUsoEm + JanelaInatividadeMinutos, TetoAbsolutoEm)</c> é calculado no SQL com
    /// <paramref name="instanteCorte"/> como parâmetro (sem relógio do banco). Para sessões não revogadas,
    /// <c>UltimoUsoEm &lt; corte</c> é condição necessária (a janela é positiva) e serve de pré-filtro indexado
    /// (<c>ix_sessoes_autenticacao_ultimo_uso_em</c>); revogadas usam <c>ix_sessoes_autenticacao_revogada_em</c>.
    /// A remoção é rastreada com <c>Versao</c> esperada (nunca exclusão em lote direta no banco): uma sessão alterada entre a
    /// leitura e o commit gera conflito de concorrência e nada é confirmado.
    /// </remarks>
    public async Task<int> LimparSessoesEncerradasAsync(DateTimeOffset instanteCorte, CancellationToken tokenCancelamento = default)
    {
        var stubs = await contexto.Sessoes
            .AsNoTracking()
            .Where(s => (s.RevogadaEm != null && s.RevogadaEm < instanteCorte)
                     || (s.RevogadaEm == null
                         && s.UltimoUsoEm < instanteCorte
                         && EF.Functions.Least(
                                s.UltimoUsoEm.AddMinutes(s.JanelaInatividadeMinutos),
                                s.TetoAbsolutoEm) < instanteCorte))
            .Select(s => new { s.IdSessao, s.Versao })
            .ToListAsync(tokenCancelamento);

        if (stubs.Count == 0)
        {
            return 0;
        }

        foreach (var stub in stubs)
        {
            SessaoAutenticacao entidade = new()
            {
                IdSessao = stub.IdSessao,
                Versao = stub.Versao,
            };

            contexto.Sessoes.Attach(entidade);
            contexto.Entry(entidade).Property(s => s.Versao).OriginalValue = stub.Versao;
            contexto.Sessoes.Remove(entidade);
        }

        return await contexto.SaveChangesAsync(tokenCancelamento);
    }
}
