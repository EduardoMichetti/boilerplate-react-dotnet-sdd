namespace Platform.UnitTests.Access;

using System.Text.RegularExpressions;
using Platform.Domain.Access;
using Xunit;

/// <summary>
/// T053: catálogo fechado de códigos de permissão (exceção registrada na Constituição IX).
/// </summary>
public sealed class TestesCodigosPermissao
{
    private static readonly Regex Formato = new(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*)+$", RegexOptions.CultureInvariant);

    [Fact]
    public void Catalogo_TemCodigosUnicos()
    {
        Assert.Equal(CodigosPermissao.Todos.Count, CodigosPermissao.Todos.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Catalogo_TodosCasamComFormatoETamanho()
    {
        Assert.NotEmpty(CodigosPermissao.Todos);
        Assert.All(CodigosPermissao.Todos, codigo =>
        {
            Assert.Matches(Formato, codigo);
            Assert.InRange(codigo.Length, 1, 128);
            Assert.True(CodigosPermissao.FormatoValido(codigo));
        });
    }

    [Fact]
    public void Catalogo_ContemFundacaoContextoLer()
    {
        Assert.Equal("foundation.context.read", CodigosPermissao.FundacaoContextoLer);
        Assert.Contains("foundation.context.read", CodigosPermissao.Todos);
        Assert.True(CodigosPermissao.EhConhecido("foundation.context.read"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("foundation.context.write")]
    [InlineData("FOUNDATION.CONTEXT.READ")]
    [InlineData("foundation")]
    public void EhConhecido_RejeitaCodigoDesconhecido(string? codigo)
    {
        Assert.False(CodigosPermissao.EhConhecido(codigo));
    }

    [Theory]
    [InlineData("foundation")]
    [InlineData("Foundation.context")]
    [InlineData("foundation..read")]
    [InlineData("1foundation.read")]
    [InlineData("foundation.read.")]
    [InlineData("foundation-context.read")]
    public void FormatoValido_RejeitaForaDoPadrao(string codigo)
    {
        Assert.False(CodigosPermissao.FormatoValido(codigo));
    }

    [Fact]
    public void PadraoFormato_DoCatalogoIgualAoDaDecisaoG7()
    {
        Assert.Equal(CodigosPermissao.PadraoFormato, Formato.ToString());
    }
}
