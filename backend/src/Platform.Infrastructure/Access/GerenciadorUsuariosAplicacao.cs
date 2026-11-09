namespace Platform.Infrastructure.Access;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Platform.Domain.Tempo;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// <see cref="UserManager{TUser}"/> da conta local com lockout pelo <see cref="TimeProvider"/> (G13):
/// o <c>UserManager</c> 10.0.12 lê o relógio do sistema em <c>AccessFailedAsync</c> e
/// <c>IsLockedOutAsync</c>. Aqui o fim do bloqueio é gravado em UTC com microssegundos
/// (<see cref="InstanteUtc"/>) e o bloqueio vale enquanto <c>LockoutEnd &gt; agora</c>.
/// </summary>
public sealed class GerenciadorUsuariosAplicacao : UserManager<UsuarioAplicacao>
{
    private readonly TimeProvider _relogio;

    /// <summary>
    /// Cria o gerenciador com as dependências do Identity e o relógio do DI.
    /// </summary>
    /// <param name="store">Store EF da conta.</param>
    /// <param name="optionsAccessor">Opções do Identity.</param>
    /// <param name="passwordHasher">Hasher nativo.</param>
    /// <param name="userValidators">Validadores de conta.</param>
    /// <param name="passwordValidators">Validadores de senha.</param>
    /// <param name="keyNormalizer">Normalizador de chaves.</param>
    /// <param name="errors">Descritor de erros.</param>
    /// <param name="services">Provedor de serviços.</param>
    /// <param name="logger">Registrador.</param>
    /// <param name="relogio">Fonte única do instante atual.</param>
    public GerenciadorUsuariosAplicacao(
        IUserStore<UsuarioAplicacao> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<UsuarioAplicacao> passwordHasher,
        IEnumerable<IUserValidator<UsuarioAplicacao>> userValidators,
        IEnumerable<IPasswordValidator<UsuarioAplicacao>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<UsuarioAplicacao>> logger,
        TimeProvider relogio)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
    {
        _relogio = relogio;
    }

    /// <summary>
    /// Incrementa a contagem de falhas; ao atingir o limite grava <c>LockoutEnd = agora + prazo</c>
    /// pelo <see cref="TimeProvider"/> e zera a contagem, numa única atualização com o
    /// <c>ConcurrencyStamp</c> esperado.
    /// </summary>
    /// <param name="user">Conta.</param>
    /// <returns>Resultado da atualização (pode ser <c>ConcurrencyFailure</c>).</returns>
    public override async Task<IdentityResult> AccessFailedAsync(UsuarioAplicacao user)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);
        IUserLockoutStore<UsuarioAplicacao> store = ObterStoreBloqueio();

        int falhas = await store.IncrementAccessFailedCountAsync(user, CancellationToken);
        if (falhas < Options.Lockout.MaxFailedAccessAttempts)
        {
            return await UpdateUserAsync(user);
        }

        DateTimeOffset agora = InstanteUtc.Normalizar(_relogio.GetUtcNow());
        await store.SetLockoutEndDateAsync(user, agora.Add(Options.Lockout.DefaultLockoutTimeSpan), CancellationToken);
        await store.ResetAccessFailedCountAsync(user, CancellationToken);
        return await UpdateUserAsync(user);
    }

    /// <summary>
    /// Indica bloqueio vigente pelo <see cref="TimeProvider"/>: <c>LockoutEnd &gt; agora</c>.
    /// </summary>
    /// <param name="user">Conta.</param>
    /// <returns><c>true</c> enquanto o bloqueio vigora.</returns>
    public override async Task<bool> IsLockedOutAsync(UsuarioAplicacao user)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);
        IUserLockoutStore<UsuarioAplicacao> store = ObterStoreBloqueio();

        if (!await store.GetLockoutEnabledAsync(user, CancellationToken))
        {
            return false;
        }

        DateTimeOffset? fim = await store.GetLockoutEndDateAsync(user, CancellationToken);
        return fim is { } fimBloqueio && fimBloqueio > InstanteUtc.Normalizar(_relogio.GetUtcNow());
    }

    private IUserLockoutStore<UsuarioAplicacao> ObterStoreBloqueio() =>
        Store as IUserLockoutStore<UsuarioAplicacao>
            ?? throw new NotSupportedException("O store da conta não suporta lockout.");
}
