namespace Platform.Infrastructure.Access;

using Microsoft.AspNetCore.Identity;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Complementa a política nativa (12 caracteres mínimos, maiúscula, minúscula, dígito e não
/// alfanumérico) com o máximo de 128 caracteres na criação e na redefinição de senha.
/// </summary>
public sealed class ValidadorTamanhoMaximoSenha : IPasswordValidator<UsuarioAplicacao>
{
    /// <summary>
    /// Tamanho máximo aceito.
    /// </summary>
    public const int TamanhoMaximo = 128;

    /// <inheritdoc />
    public Task<IdentityResult> ValidateAsync(UserManager<UsuarioAplicacao> manager, UsuarioAplicacao user, string? password) =>
        Task.FromResult(password is { Length: > TamanhoMaximo }
            ? IdentityResult.Failed(new IdentityError { Code = "PasswordTooLong", Description = $"A senha deve ter no máximo {TamanhoMaximo} caracteres." })
            : IdentityResult.Success);
}
