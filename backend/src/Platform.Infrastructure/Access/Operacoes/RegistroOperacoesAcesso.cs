namespace Platform.Infrastructure.Access.Operacoes;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Gravação de eventos no histórico append-only <c>access.operacoes_acesso</c>.
/// </summary>
public sealed class RegistroOperacoesAcesso(AcessoTenantDbContext contexto) : IRegistroOperacoesAcesso
{
    /// <inheritdoc />
    public async Task<ITransacaoOperacaoAcesso> IniciarTransacaoAsync(CancellationToken tokenCancelamento = default)
    {
        IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync(tokenCancelamento);
        return new TransacaoOperacaoAcesso(contexto, transacao);
    }

    /// <inheritdoc />
    public async Task RegistrarFalhaOuNegadoEmTransacaoSeparadaAsync(DadosRegistroOperacao registro, CancellationToken tokenCancelamento = default)
    {
        contexto.ChangeTracker.Clear();

        await using IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync(tokenCancelamento);
        try
        {
            RegistroOperacaoAcesso entidade = Converter(registro);
            entidade.Validar();
            contexto.OperacoesAcesso.Add(entidade);
            await contexto.SaveChangesAsync(tokenCancelamento);
            await transacao.CommitAsync(tokenCancelamento);
        }
        catch
        {
            await transacao.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static RegistroOperacaoAcesso Converter(DadosRegistroOperacao dados) =>
        new()
        {
            IdOperacao = dados.IdOperacao,
            IdTenant = dados.IdTenant,
            IdAmbiente = dados.IdAmbiente,
            IdUsuarioAlvo = dados.IdUsuarioAlvo,
            LoginOperador = dados.LoginOperador,
            TipoOperacao = dados.TipoOperacao,
            CodigoPermissao = dados.CodigoPermissao,
            RegistradaEm = dados.RegistradaEm,
            Resultado = dados.Resultado,
            CodigoResultado = dados.CodigoResultado,
            RetencaoDias = dados.RetencaoDias,
            InstanteCorte = dados.InstanteCorte,
            QuantidadeRemovida = dados.QuantidadeRemovida,
        };

    private sealed class TransacaoOperacaoAcesso(
        AcessoTenantDbContext contexto,
        IDbContextTransaction transacao) : ITransacaoOperacaoAcesso
    {
        public async Task RegistrarSucessoEConfirmarAsync(DadosRegistroOperacao registro, CancellationToken tokenCancelamento = default)
        {
            RegistroOperacaoAcesso entidade = Converter(registro);
            entidade.Validar();
            contexto.OperacoesAcesso.Add(entidade);
            await contexto.SaveChangesAsync(tokenCancelamento);
            await transacao.CommitAsync(tokenCancelamento);
        }

        public async Task DesfazerAsync(CancellationToken tokenCancelamento = default)
        {
            await transacao.RollbackAsync(CancellationToken.None);
        }

        public async ValueTask DisposeAsync()
        {
            await transacao.DisposeAsync();
        }
    }
}
