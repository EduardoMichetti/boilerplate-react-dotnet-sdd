namespace Platform.UnitTests.Operations;

using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Auditoria;
using Platform.Domain.Auditoria;
using Platform.Operations;
using Xunit;

/// <summary>
/// Autoria do host operacional: o autor só existe depois do login verificado e a composição do
/// host expõe a mesma instância como contexto e como registro do operador.
/// </summary>
public sealed class TestesContextoOperacaoOperador
{
    [Fact]
    public void NovoContexto_TipoOperadorSemAutorAteVerificacao()
    {
        ContextoOperacaoOperador contexto = new(TimeProvider.System);

        Assert.Equal(TipoAutor.Operador, contexto.TipoAutor);
        Assert.Null(contexto.Autor);
        Assert.Equal(7, contexto.IdOperacao.Version);
    }

    [Fact]
    public void DefinirOperadorVerificado_DefineAutorEAceitaRepeticaoDoMesmoLogin()
    {
        ContextoOperacaoOperador contexto = new(TimeProvider.System);

        contexto.DefinirOperadorVerificado("appdb_op_um");
        contexto.DefinirOperadorVerificado("appdb_op_um");

        Assert.Equal("appdb_op_um", contexto.Autor);
    }

    [Fact]
    public void DefinirOperadorVerificado_ComOutroLogin_Falha()
    {
        ContextoOperacaoOperador contexto = new(TimeProvider.System);
        contexto.DefinirOperadorVerificado("appdb_op_um");

        Assert.Throws<InvalidOperationException>(() => contexto.DefinirOperadorVerificado("appdb_op_dois"));
        Assert.Equal("appdb_op_um", contexto.Autor);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void DefinirOperadorVerificado_Vazio_Falha(string login)
    {
        ContextoOperacaoOperador contexto = new(TimeProvider.System);

        Assert.Throws<ArgumentException>(() => contexto.DefinirOperadorVerificado(login));
        Assert.Null(contexto.Autor);
    }

    [Fact]
    public void ComposicaoDoHost_ExpoeMesmaInstanciaComoContextoERegistro()
    {
        ServiceCollection servicos = new();
        servicos.AddSingleton(TimeProvider.System);
        servicos.AdicionarServicosHostOperacoes();
        using ServiceProvider provedor = servicos.BuildServiceProvider();
        using IServiceScope escopo = provedor.CreateScope();

        IRegistroOperadorVerificado registro = escopo.ServiceProvider.GetRequiredService<IRegistroOperadorVerificado>();
        IContextoOperacao contexto = escopo.ServiceProvider.GetRequiredService<IContextoOperacao>();
        registro.DefinirOperadorVerificado("appdb_op_um");

        Assert.Same(registro, contexto);
        Assert.Equal("appdb_op_um", contexto.Autor);
    }
}
