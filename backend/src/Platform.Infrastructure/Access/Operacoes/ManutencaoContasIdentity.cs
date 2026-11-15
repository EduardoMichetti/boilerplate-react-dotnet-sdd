namespace Platform.Infrastructure.Access.Operacoes;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Access.Portas;
using Platform.Application.Configuration;
using Platform.Domain.Access;
using Platform.Infrastructure.Access;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Implementação de manutenção de contas e permissões baseada no ASP.NET Core Identity
/// (<see cref="GerenciadorUsuariosAplicacao"/>) e no <see cref="AcessoTenantDbContext"/>.
/// </summary>
public sealed class ManutencaoContasIdentity(
    AcessoTenantDbContext contexto,
    GerenciadorUsuariosAplicacao gerenciador,
    IRepositorioSessoes repositorioSessoes,
    IOptions<OpcoesInstalacaoTenant> instalacao,
    IClassificadorFalhaPersistencia classificadorFalha) : IManutencaoContas
{
    /// <inheritdoc />
    public async Task<ResultadoAcaoConta> CriarContaAsync(
        Guid idUsuario,
        string email,
        string nomeExibicao,
        string senha,
        CancellationToken tokenCancelamento = default)
    {
        UsuarioAplicacao usuario = new()
        {
            Id = idUsuario,
            UserName = email,
            Email = email,
            NomeExibicao = nomeExibicao,
            FkIdTenant = instalacao.Value.IdTenant,
            EstaAtivo = true,
            LockoutEnabled = true,
        };

        IdentityResult resultado = await gerenciador.CreateAsync(usuario, senha);
        if (resultado.Succeeded)
        {
            return ResultadoAcaoConta.Ok();
        }

        IdentityError? erroSenha = resultado.Errors.FirstOrDefault(e =>
            e.Code.StartsWith("Password", StringComparison.OrdinalIgnoreCase));
        if (erroSenha is not null)
        {
            return ResultadoAcaoConta.SenhaForaDaPolitica(erroSenha.Description);
        }

        IdentityError? erroDuplicado = resultado.Errors.FirstOrDefault(e =>
            e.Code.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
        if (erroDuplicado is not null)
        {
            return ResultadoAcaoConta.EmailJaCadastrado();
        }

        IdentityError? erroEmail = resultado.Errors.FirstOrDefault(e =>
            e.Code is nameof(IdentityErrorDescriber.InvalidEmail) or nameof(IdentityErrorDescriber.InvalidUserName));
        if (erroEmail is not null)
        {
            return ResultadoAcaoConta.EmailInvalido(erroEmail.Description);
        }

        return MapearFalhaIdentity(resultado);
    }

    /// <summary>
    /// Somente <c>ConcurrencyFailure</c> do Identity é conflito concorrente real (FR-033);
    /// qualquer outro erro não previsto é <c>FALHA_INTERNA</c>, com o código do Identity no diagnóstico.
    /// </summary>
    private static ResultadoAcaoConta MapearFalhaIdentity(IdentityResult resultado)
    {
        IdentityError erro = resultado.Errors.FirstOrDefault(e =>
                e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure))
            ?? resultado.Errors.FirstOrDefault()
            ?? new IdentityError { Code = "Desconhecido", Description = "Falha sem detalhe do Identity." };

        return erro.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)
            ? ResultadoAcaoConta.ConflitoConcorrente(erro.Description)
            : ResultadoAcaoConta.FalhaInterna($"Identity {erro.Code}: {erro.Description}");
    }

    /// <inheritdoc />
    public async Task<ResultadoAcaoConta> DesativarContaAsync(
        Guid idUsuario,
        CancellationToken tokenCancelamento = default)
    {
        UsuarioAplicacao? usuario = await gerenciador.FindByIdAsync(idUsuario.ToString());
        if (usuario is null)
        {
            return ResultadoAcaoConta.UsuarioNaoEncontrado();
        }

        usuario.EstaAtivo = false;
        await repositorioSessoes.RevogarTodasDaContaAsync(idUsuario, tokenCancelamento);

        IdentityResult resultado = await gerenciador.UpdateSecurityStampAsync(usuario);
        if (!resultado.Succeeded)
        {
            return MapearFalhaIdentity(resultado);
        }

        return ResultadoAcaoConta.Ok();
    }

    /// <inheritdoc />
    public async Task<ResultadoAcaoConta> ReativarContaAsync(
        Guid idUsuario,
        CancellationToken tokenCancelamento = default)
    {
        UsuarioAplicacao? usuario = await gerenciador.FindByIdAsync(idUsuario.ToString());
        if (usuario is null)
        {
            return ResultadoAcaoConta.UsuarioNaoEncontrado();
        }

        usuario.EstaAtivo = true;
        IdentityResult resultado = await gerenciador.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            return MapearFalhaIdentity(resultado);
        }

        return ResultadoAcaoConta.Ok();
    }

    /// <inheritdoc />
    public async Task<ResultadoAcaoConta> RedefinirSenhaAsync(
        Guid idUsuario,
        string novaSenha,
        CancellationToken tokenCancelamento = default)
    {
        UsuarioAplicacao? usuario = await gerenciador.FindByIdAsync(idUsuario.ToString());
        if (usuario is null)
        {
            return ResultadoAcaoConta.UsuarioNaoEncontrado();
        }

        foreach (IPasswordValidator<UsuarioAplicacao> validador in gerenciador.PasswordValidators)
        {
            IdentityResult validacao = await validador.ValidateAsync(gerenciador, usuario, novaSenha);
            if (!validacao.Succeeded)
            {
                return ResultadoAcaoConta.SenhaForaDaPolitica(validacao.Errors.First().Description);
            }
        }

        usuario.PasswordHash = gerenciador.PasswordHasher.HashPassword(usuario, novaSenha);
        await gerenciador.ResetAccessFailedCountAsync(usuario);
        await gerenciador.SetLockoutEndDateAsync(usuario, null);
        await repositorioSessoes.RevogarTodasDaContaAsync(idUsuario, tokenCancelamento);
        await gerenciador.UpdateSecurityStampAsync(usuario);

        IdentityResult resultado = await gerenciador.UpdateAsync(usuario);
        if (!resultado.Succeeded)
        {
            return MapearFalhaIdentity(resultado);
        }

        return ResultadoAcaoConta.Ok();
    }

    /// <inheritdoc />
    public async Task<ResultadoAcaoConta> RevogarSessoesAsync(
        Guid idUsuario,
        CancellationToken tokenCancelamento = default)
    {
        UsuarioAplicacao? usuario = await gerenciador.FindByIdAsync(idUsuario.ToString());
        if (usuario is null)
        {
            return ResultadoAcaoConta.UsuarioNaoEncontrado();
        }

        await repositorioSessoes.RevogarTodasDaContaAsync(idUsuario, tokenCancelamento);

        IdentityResult resultado = await gerenciador.UpdateSecurityStampAsync(usuario);
        if (!resultado.Succeeded)
        {
            return MapearFalhaIdentity(resultado);
        }

        return ResultadoAcaoConta.Ok();
    }

    /// <inheritdoc />
    public async Task<ResultadoAcaoConta> ConcederPermissaoAsync(
        Guid idUsuario,
        string codigoPermissao,
        CancellationToken tokenCancelamento = default)
    {
        if (!CodigosPermissao.EhConhecido(codigoPermissao))
        {
            return ResultadoAcaoConta.PermissaoDesconhecida(codigoPermissao);
        }

        UsuarioAplicacao? usuario = await gerenciador.FindByIdAsync(idUsuario.ToString());
        if (usuario is null)
        {
            return ResultadoAcaoConta.UsuarioNaoEncontrado();
        }

        bool jaPossui = await contexto.Permissoes
            .AnyAsync(p => p.FkIdUsuario == idUsuario && p.CodigoPermissao == codigoPermissao, tokenCancelamento);
        if (jaPossui)
        {
            return ResultadoAcaoConta.Ok();
        }

        PermissaoUsuario nova = new()
        {
            FkIdUsuario = idUsuario,
            CodigoPermissao = codigoPermissao,
        };

        contexto.Permissoes.Add(nova);
        try
        {
            await contexto.SaveChangesAsync(tokenCancelamento);
            return ResultadoAcaoConta.Ok();
        }
        catch (DbUpdateException ex) when (classificadorFalha.EhConflitoConcorrente(ex))
        {
            contexto.Entry(nova).State = EntityState.Detached;
            return ResultadoAcaoConta.ConflitoConcorrente("Violação concorrente de chave primária ao conceder permissão.");
        }
    }

    /// <inheritdoc />
    public async Task<ResultadoAcaoConta> RevogarPermissaoAsync(
        Guid idUsuario,
        string codigoPermissao,
        CancellationToken tokenCancelamento = default)
    {
        if (!CodigosPermissao.EhConhecido(codigoPermissao))
        {
            return ResultadoAcaoConta.PermissaoDesconhecida(codigoPermissao);
        }

        UsuarioAplicacao? usuario = await gerenciador.FindByIdAsync(idUsuario.ToString());
        if (usuario is null)
        {
            return ResultadoAcaoConta.UsuarioNaoEncontrado();
        }

        PermissaoUsuario? existente = await contexto.Permissoes
            .FirstOrDefaultAsync(p => p.FkIdUsuario == idUsuario && p.CodigoPermissao == codigoPermissao, tokenCancelamento);
        if (existente is null)
        {
            return ResultadoAcaoConta.Ok();
        }

        contexto.Permissoes.Remove(existente);
        try
        {
            int linhas = await contexto.SaveChangesAsync(tokenCancelamento);
            if (linhas == 0)
            {
                return ResultadoAcaoConta.ConflitoConcorrente("Nenhuma linha afetada na revogação concorrente de permissão.");
            }

            return ResultadoAcaoConta.Ok();
        }
        catch (DbUpdateConcurrencyException)
        {
            contexto.Entry(existente).State = EntityState.Detached;
            return ResultadoAcaoConta.ConflitoConcorrente("Conflito de concorrência ao revogar permissão.");
        }
    }
}
