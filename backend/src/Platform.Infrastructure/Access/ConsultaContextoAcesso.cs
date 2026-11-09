namespace Platform.Infrastructure.Access;

using Microsoft.EntityFrameworkCore;
using Platform.Application.Access.Portas;
using Platform.Infrastructure.Persistence.TenantAccess;

/// <summary>
/// Conta ativa e concessões lidas no banco a cada chamada, sem claims nem cache: revogar uma
/// concessão vale na próxima operação após o commit.
/// </summary>
/// <param name="contexto">DbContext da requisição.</param>
public sealed class ConsultaContextoAcesso(AcessoTenantDbContext contexto) : IConsultaContextoAcesso
{
    /// <inheritdoc />
    public async Task<DadosContaAcesso?> ObterContaAsync(Guid idUsuario, CancellationToken tokenCancelamento)
    {
        var conta = await contexto.Users
            .AsNoTracking()
            .Where(u => u.Id == idUsuario && u.EstaAtivo)
            .Select(u => new
            {
                u.Id,
                u.Email,
                u.NomeExibicao,
                Permissoes = u.Permissoes.Select(p => p.CodigoPermissao).ToList(),
            })
            .FirstOrDefaultAsync(tokenCancelamento);

        if (conta is null || string.IsNullOrEmpty(conta.Email))
        {
            return null;
        }

        List<string> permissoes = conta.Permissoes.Order(StringComparer.Ordinal).ToList();
        return new DadosContaAcesso(conta.Id, conta.Email, conta.NomeExibicao, permissoes);
    }
}
