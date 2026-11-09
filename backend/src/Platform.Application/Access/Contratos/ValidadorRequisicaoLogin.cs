namespace Platform.Application.Access.Contratos;

using FluentValidation;

/// <summary>
/// Formato de <see cref="RequisicaoLogin"/>: sem banco, relógio ou regra assíncrona. A política de
/// senha de criação não é aplicada aqui (senha fora da política resulta em 401 genérico).
/// </summary>
public sealed class ValidadorRequisicaoLogin : AbstractValidator<RequisicaoLogin>
{
    /// <summary>
    /// Declara as regras do contrato com mensagens pt-BR que não ecoam valores.
    /// </summary>
    public ValidadorRequisicaoLogin()
    {
        RuleFor(r => r.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("O email deve ser informado.")
            .MaximumLength(254).WithMessage("O email deve ter no máximo 254 caracteres.")
            .EmailAddress().WithMessage("O email deve ter formato válido.");

        RuleFor(r => r.Senha)
            .Length(1, 128).WithMessage("A senha deve ter de 1 a 128 caracteres.");
    }
}
