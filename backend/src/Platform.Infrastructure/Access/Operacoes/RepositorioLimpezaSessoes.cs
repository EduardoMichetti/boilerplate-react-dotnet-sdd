namespace Platform.Infrastructure.Access.Operacoes;

using Microsoft.EntityFrameworkCore;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Repositório para remoção rastreada em lote de sessões encerradas além da retenção (<c>sessao limpar</c>).
/// Projeta somente <c>IdSessao</c> e <c>Versao</c>, sem carregar tickets ou contas, e remove via entidades anexadas.
/// </summary>
public sealed class RepositorioLimpezaSessoes(AcessoTenantDbContext contexto) : IRepositorioLimpezaSessoes
{
    /// <inheritdoc />
    public async Task<int> LimparSessoesEncerradasAsync(DateTimeOffset instanteCorte, CancellationToken tokenCancelamento = default)
    {
        var stubs = await contexto.Sessoes
            .AsNoTracking()
            .Where(s => (s.RevogadaEm != null && s.RevogadaEm < instanteCorte)
                     || (s.RevogadaEm == null && s.ExpiraEm < instanteCorte))
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
