namespace Platform.UnitTests.Access;

using FluentValidation;
using FluentValidation.TestHelper;
using Platform.Application.Access.Contratos;
using Platform.Application.Validacao;
using Xunit;

/// <summary>
/// T081: validação do formato da requisição de login (FluentValidation, ADR 0003 D2),
/// com chaves JSON do contrato e mensagens pt-BR que não ecoam o valor recebido.
/// </summary>
public sealed class TestesValidadorRequisicaoLogin
{
    private const string EmailSecreto = "pessoa.real@empresa.example";
    private readonly ValidadorRequisicaoLogin _validador = new();

    static TestesValidadorRequisicaoLogin()
    {
        // Antes de construir qualquer validador: o nome da propriedade é fixado na criação da regra.
        ValidatorOptions.Global.PropertyNameResolver = ResolvedorNomePropriedadeJson.Resolver;
    }

    [Fact]
    public void RequisicaoValida_NaoTemErros()
    {
        _validador.TestValidate(new RequisicaoLogin { Email = "acesso@example.invalid", Senha = "x" }).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmailObrigatorio(string email)
    {
        TestValidationResult<RequisicaoLogin> resultado = _validador.TestValidate(new RequisicaoLogin { Email = email, Senha = "x" });

        Assert.Equal("email", Assert.Single(resultado.Errors).PropertyName);
        resultado.ShouldHaveValidationErrorFor("email");
    }

    [Theory]
    [InlineData("sem-arroba")]
    [InlineData("a@")]
    [InlineData("@b")]
    public void EmailComFormatoInvalido(string email)
    {
        _validador.TestValidate(new RequisicaoLogin { Email = email, Senha = "x" }).ShouldHaveValidationErrorFor("email");
    }

    [Fact]
    public void EmailAte254Caracteres()
    {
        string email254 = new string('a', 242) + "@example.com";
        string email255 = new string('a', 243) + "@example.com";

        _validador.TestValidate(new RequisicaoLogin { Email = email254, Senha = "x" }).ShouldNotHaveValidationErrorFor("email");
        _validador.TestValidate(new RequisicaoLogin { Email = email255, Senha = "x" }).ShouldHaveValidationErrorFor("email");
    }

    [Fact]
    public void SenhaObrigatoriaDe1a128()
    {
        _validador.TestValidate(new RequisicaoLogin { Email = "a@example.com", Senha = "" }).ShouldHaveValidationErrorFor("password");
        _validador.TestValidate(new RequisicaoLogin { Email = "a@example.com", Senha = new string('s', 128) }).ShouldNotHaveValidationErrorFor("password");
        _validador.TestValidate(new RequisicaoLogin { Email = "a@example.com", Senha = new string('s', 129) }).ShouldHaveValidationErrorFor("password");
    }

    [Fact]
    public void SenhaCurtaNaoRevelaPoliticaDeCriacao()
    {
        _validador.TestValidate(new RequisicaoLogin { Email = "a@example.com", Senha = "1" }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void MensagensEmPortuguesNaoEcoamValores()
    {
        string senhaSecreta = new('S', 129);
        TestValidationResult<RequisicaoLogin> resultado = _validador.TestValidate(
            new RequisicaoLogin { Email = EmailSecreto + new string('x', 260), Senha = senhaSecreta });

        Assert.NotEmpty(resultado.Errors);
        Assert.All(resultado.Errors, erro =>
        {
            Assert.DoesNotContain(EmailSecreto, erro.ErrorMessage, StringComparison.Ordinal);
            Assert.DoesNotContain(senhaSecreta, erro.ErrorMessage, StringComparison.Ordinal);
            Assert.Contains(erro.PropertyName, new[] { "email", "password" });
        });
        Assert.Contains(resultado.Errors, e => e.ErrorMessage.Contains("deve", StringComparison.Ordinal));
    }
}
