namespace Platform.Infrastructure.Access;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Platform.Application;
using Platform.Application.Access.Portas;
using Platform.Application.Auditoria;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Verificação de credenciais pelo <see cref="GerenciadorUsuariosAplicacao"/> (G13). Conflito de
/// <c>ConcurrencyStamp</c> recarrega a conta e reavalia até três vezes; esgotado, falha de
/// dependência (503). Ao confirmar o bloqueio, revoga todas as sessões e troca o stamp na mesma
/// transação. Sem fábrica de claims nem tabelas auxiliares do Identity.
/// </summary>
/// <param name="gerenciador">Gerenciador da conta local.</param>
/// <param name="contexto">DbContext compartilhado com o store do Identity.</param>
/// <param name="sessoes">Revogação das sessões da conta.</param>
/// <param name="registroUsuario">Autoria após credencial verificada.</param>
public sealed class ServicoCredenciaisIdentity(
    GerenciadorUsuariosAplicacao gerenciador,
    AcessoTenantDbContext contexto,
    IRepositorioSessoes sessoes,
    IRegistroUsuarioVerificado registroUsuario) : IServicoCredenciais
{
    /// <summary>
    /// Máximo de avaliações por conflito de concorrência antes de responder 503.
    /// </summary>
    public const int MaximoTentativas = 3;

    private const string CodigoConflitoIdentity = nameof(IdentityErrorDescriber.ConcurrencyFailure);

    /// <inheritdoc />
    public async Task<ResultadoCredenciais> VerificarAsync(string email, string senha, CancellationToken tokenCancelamento)
    {
        for (int tentativa = 1; ; tentativa++)
        {
            ResultadoCredenciais? resultado = await AvaliarAsync(email, senha, tokenCancelamento);
            if (resultado is not null)
            {
                return resultado;
            }

            if (tentativa >= MaximoTentativas)
            {
                throw new ExcecaoDependenciaIndisponivel("Conflitos de concorrência sucessivos ao atualizar a conta.");
            }

            contexto.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Uma avaliação completa; nulo indica conflito de concorrência (recarregar e reavaliar).
    /// </summary>
    private async Task<ResultadoCredenciais?> AvaliarAsync(string email, string senha, CancellationToken tokenCancelamento)
    {
        UsuarioAplicacao? usuario = await gerenciador.FindByEmailAsync(email);
        if (usuario is null || !usuario.EstaAtivo || await gerenciador.IsLockedOutAsync(usuario))
        {
            return ResultadoCredenciais.Invalida;
        }

        if (!await gerenciador.CheckPasswordAsync(usuario, senha))
        {
            return await RegistrarFalhaAsync(usuario, tokenCancelamento) ? ResultadoCredenciais.Invalida : null;
        }

        registroUsuario.DefinirUsuarioVerificado(usuario.Id);
        if (await gerenciador.GetAccessFailedCountAsync(usuario) > 0)
        {
            IdentityResult zerada = await gerenciador.ResetAccessFailedCountAsync(usuario);
            if (!zerada.Succeeded)
            {
                return EhConflito(zerada) ? null : throw new ExcecaoDependenciaIndisponivel("Falha ao atualizar a conta.");
            }
        }

        return new ResultadoCredenciais(true, usuario.Id, usuario.SecurityStamp);
    }

    /// <summary>
    /// Incrementa a falha (autoria anônima); se bloqueou, revoga sessões e troca o stamp na mesma transação.
    /// </summary>
    /// <returns><c>false</c> quando houve conflito de concorrência (nada confirmado).</returns>
    private async Task<bool> RegistrarFalhaAsync(UsuarioAplicacao usuario, CancellationToken tokenCancelamento)
    {
        await using IDbContextTransaction transacao = await contexto.Database.BeginTransactionAsync(tokenCancelamento);
        try
        {
            IdentityResult falha = await gerenciador.AccessFailedAsync(usuario);
            if (!falha.Succeeded)
            {
                return EhConflito(falha) ? await DesfazerAsync(transacao) : throw new ExcecaoDependenciaIndisponivel("Falha ao registrar tentativa.");
            }

            if (await gerenciador.IsLockedOutAsync(usuario))
            {
                await sessoes.RevogarTodasDaContaAsync(usuario.Id, tokenCancelamento);
                IdentityResult carimbo = await gerenciador.UpdateSecurityStampAsync(usuario);
                if (!carimbo.Succeeded)
                {
                    return EhConflito(carimbo) ? await DesfazerAsync(transacao) : throw new ExcecaoDependenciaIndisponivel("Falha ao confirmar o bloqueio.");
                }
            }

            await transacao.CommitAsync(tokenCancelamento);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            return await DesfazerAsync(transacao);
        }
    }

    private static async Task<bool> DesfazerAsync(IDbContextTransaction transacao)
    {
        await transacao.RollbackAsync(CancellationToken.None);
        return false;
    }

    private static bool EhConflito(IdentityResult resultado) =>
        resultado.Errors.Any(e => string.Equals(e.Code, CodigoConflitoIdentity, StringComparison.Ordinal));
}
