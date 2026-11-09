namespace Platform.UnitTests.Tenancy;

using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.DesignTime;
using Xunit;

public sealed class TestesValidadorPreMigracao
{
    private static readonly Guid IdTenantAlvo = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid IdAmbienteAlvo = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private const string StringConexaoValida = "Host=localhost;Database=teste;Username=postgres";

    [Fact]
    public void ValidarOpcoes_AceitaEstadoInativoEIdentidadeCoincidente()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            idTenantCli: IdTenantAlvo,
            idAmbienteCli: IdAmbienteAlvo);

        PreflightMigracaoTenant.ValidarOpcoes(opcoes);
    }

    [Fact]
    public void ValidarOpcoes_RejeitaEstadoAtivoParaDDL()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(estadoTenant: EstadoTenant.Ativo);

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreflightMigracaoTenant.ValidarOpcoes(opcoes));

        Assert.Contains("Inativo", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarOpcoes_RejeitaIdTenantDivergenteNaCli()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            idTenantCli: Guid.NewGuid()); // divergente

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreflightMigracaoTenant.ValidarOpcoes(opcoes));

        Assert.Contains("IdTenant", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarOpcoes_RejeitaIdAmbienteDivergenteNaCli()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            idAmbienteCli: Guid.NewGuid()); // divergente

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreflightMigracaoTenant.ValidarOpcoes(opcoes));

        Assert.Contains("IdAmbiente", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("booleano-invalido")]
    [InlineData("123")]
    [InlineData("sim")]
    public void AnalisarPermissaoInicializacao_RejeitaSintaxeBooleanaInvalida(string valorBruto)
    {
        ArgumentException excecao = Assert.Throws<ArgumentException>(
            () => PreflightMigracaoTenant.AnalisarPermissaoInicializacao(valorBruto));

        Assert.Contains("PermitirInicializarBancoVazio", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnalisarPermissaoInicializacao_RetornaFalsoQuandoAusenteOuVazio(string? valorBruto)
    {
        bool resultado = PreflightMigracaoTenant.AnalisarPermissaoInicializacao(valorBruto);
        Assert.False(resultado);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    public void AnalisarPermissaoInicializacao_ConverteCorretamenteTextosValidos(string valorBruto, bool esperado)
    {
        bool resultado = PreflightMigracaoTenant.AnalisarPermissaoInicializacao(valorBruto);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoVazio_RejeitaQuandoNaoAutorizado()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            permissaoInicializarBruta: "false");

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreflightMigracaoTenant.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 0, marcador: null));

        Assert.Contains("não autorizada", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoVazio_PermiteQuandoAutorizado()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            permissaoInicializarBruta: "true");

        PreflightMigracaoTenant.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 0, marcador: null);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_RejeitaQuandoSemTabelaMarcador()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            permissaoInicializarBruta: "true");

        // Tabelas existentes mas sem a tabela de marcador
        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreflightMigracaoTenant.AvaliarEstadoBanco(
                opcoes,
                totalTabelasUsuario: 5,
                marcador: new MarcadorBancoExistente(TabelaExiste: false, LinhaExiste: false, Guid.Empty, Guid.Empty)));

        Assert.Contains("sem marcador", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_RejeitaQuandoLinhaDoMarcadorAusente()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            permissaoInicializarBruta: "true");

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreflightMigracaoTenant.AvaliarEstadoBanco(
                opcoes,
                totalTabelasUsuario: 5,
                marcador: new MarcadorBancoExistente(TabelaExiste: true, LinhaExiste: false, Guid.Empty, Guid.Empty)));

        Assert.Contains("sem marcador", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_RejeitaQuandoMarcadorDivergeDoTenant()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            permissaoInicializarBruta: "true");

        MarcadorBancoExistente marcadorDivergente = new(
            TabelaExiste: true,
            LinhaExiste: true,
            IdTenant: Guid.NewGuid(), // outro tenant
            IdAmbiente: IdAmbienteAlvo);

        Assert.Throws<ExcecaoIsolamentoTenant>(
            () => PreflightMigracaoTenant.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 5, marcador: marcadorDivergente));
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_RejeitaQuandoMarcadorDivergeDoAmbiente()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            permissaoInicializarBruta: "true");

        MarcadorBancoExistente marcadorDivergente = new(
            TabelaExiste: true,
            LinhaExiste: true,
            IdTenant: IdTenantAlvo,
            IdAmbiente: Guid.NewGuid()); // outro ambiente

        Assert.Throws<ExcecaoIsolamentoTenant>(
            () => PreflightMigracaoTenant.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 5, marcador: marcadorDivergente));
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_PermiteQuandoMarcadorConfereMesmoComPermissaoInicializarFalsa()
    {
        OpcoesPreflightMigracao opcoes = CriarOpcoes(
            estadoTenant: EstadoTenant.Inativo,
            permissaoInicializarBruta: "false"); // allowInit é false, mas a base já possui marcador válido

        MarcadorBancoExistente marcadorCorrespondente = new(
            TabelaExiste: true,
            LinhaExiste: true,
            IdTenant: IdTenantAlvo,
            IdAmbiente: IdAmbienteAlvo);

        PreflightMigracaoTenant.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 5, marcador: marcadorCorrespondente);
    }

    private static OpcoesPreflightMigracao CriarOpcoes(
        EstadoTenant estadoTenant = EstadoTenant.Inativo,
        string? permissaoInicializarBruta = null,
        Guid? idTenantCli = null,
        Guid? idAmbienteCli = null)
    {
        return new OpcoesPreflightMigracao(
            CodigoAplicacao: "produto-teste",
            IdAmbiente: IdAmbienteAlvo,
            IdTenant: IdTenantAlvo,
            EstadoTenant: estadoTenant,
            StringConexao: StringConexaoValida,
            PermissaoInicializarBancoVazioBruta: permissaoInicializarBruta,
            IdTenantCli: idTenantCli,
            IdAmbienteCli: idAmbienteCli);
    }
}
