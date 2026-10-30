namespace Platform.UnitTests.Tenancy;

using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.DesignTime;
using Xunit;

public sealed class TestesValidadorPreMigracao
{
    private static readonly Guid IdInquilinoAlvo = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid IdAmbienteAlvo = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private const string StringConexaoValida = "Host=localhost;Database=teste;Username=postgres";

    [Fact]
    public void ValidarOpcoes_AceitaEstadoInativoEIdentidadeCoincidente()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            idInquilinoCli: IdInquilinoAlvo,
            idAmbienteCli: IdAmbienteAlvo);

        PreValidacaoMigracaoInquilino.ValidarOpcoes(opcoes);
    }

    [Fact]
    public void ValidarOpcoes_RejeitaEstadoAtivoParaDDL()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(estadoInquilino: EstadoInquilino.Ativo);

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreValidacaoMigracaoInquilino.ValidarOpcoes(opcoes));

        Assert.Contains("Inativo", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarOpcoes_RejeitaIdInquilinoDivergenteNaCli()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            idInquilinoCli: Guid.NewGuid()); // divergente

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreValidacaoMigracaoInquilino.ValidarOpcoes(opcoes));

        Assert.Contains("IdInquilino", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarOpcoes_RejeitaIdAmbienteDivergenteNaCli()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            idAmbienteCli: Guid.NewGuid()); // divergente

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreValidacaoMigracaoInquilino.ValidarOpcoes(opcoes));

        Assert.Contains("IdAmbiente", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("booleano-invalido")]
    [InlineData("123")]
    [InlineData("sim")]
    public void AnalisarPermissaoInicializacao_RejeitaSintaxeBooleanaInvalida(string valorBruto)
    {
        ArgumentException excecao = Assert.Throws<ArgumentException>(
            () => PreValidacaoMigracaoInquilino.AnalisarPermissaoInicializacao(valorBruto));

        Assert.Contains("AllowInitializeEmptyDatabase", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AnalisarPermissaoInicializacao_RetornaFalsoQuandoAusenteOuVazio(string? valorBruto)
    {
        bool resultado = PreValidacaoMigracaoInquilino.AnalisarPermissaoInicializacao(valorBruto);
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
        bool resultado = PreValidacaoMigracaoInquilino.AnalisarPermissaoInicializacao(valorBruto);
        Assert.Equal(esperado, resultado);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoVazio_RejeitaQuandoNaoAutorizado()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            permissaoInicializarBruta: "false");

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreValidacaoMigracaoInquilino.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 0, marcador: null));

        Assert.Contains("não autorizada", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoVazio_PermiteQuandoAutorizado()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            permissaoInicializarBruta: "true");

        PreValidacaoMigracaoInquilino.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 0, marcador: null);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_RejeitaQuandoSemTabelaMarcador()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            permissaoInicializarBruta: "true");

        // Tabelas existentes mas sem a tabela de marcador
        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreValidacaoMigracaoInquilino.AvaliarEstadoBanco(
                opcoes,
                totalTabelasUsuario: 5,
                marcador: new MarcadorBancoExistente(TabelaExiste: false, LinhaExiste: false, Guid.Empty, Guid.Empty)));

        Assert.Contains("sem marcador", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_RejeitaQuandoLinhaDoMarcadorAusente()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            permissaoInicializarBruta: "true");

        InvalidOperationException excecao = Assert.Throws<InvalidOperationException>(
            () => PreValidacaoMigracaoInquilino.AvaliarEstadoBanco(
                opcoes,
                totalTabelasUsuario: 5,
                marcador: new MarcadorBancoExistente(TabelaExiste: true, LinhaExiste: false, Guid.Empty, Guid.Empty)));

        Assert.Contains("sem marcador", excecao.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_RejeitaQuandoMarcadorDivergeDoInquilino()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            permissaoInicializarBruta: "true");

        MarcadorBancoExistente marcadorDivergente = new(
            TabelaExiste: true,
            LinhaExiste: true,
            IdInquilino: Guid.NewGuid(), // outro inquilino
            IdAmbiente: IdAmbienteAlvo);

        Assert.Throws<ExcecaoIsolamentoInquilino>(
            () => PreValidacaoMigracaoInquilino.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 5, marcador: marcadorDivergente));
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_RejeitaQuandoMarcadorDivergeDoAmbiente()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            permissaoInicializarBruta: "true");

        MarcadorBancoExistente marcadorDivergente = new(
            TabelaExiste: true,
            LinhaExiste: true,
            IdInquilino: IdInquilinoAlvo,
            IdAmbiente: Guid.NewGuid()); // outro ambiente

        Assert.Throws<ExcecaoIsolamentoInquilino>(
            () => PreValidacaoMigracaoInquilino.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 5, marcador: marcadorDivergente));
    }

    [Fact]
    public void AvaliarEstadoBanco_BancoExistente_PermiteQuandoMarcadorConfereMesmoComPermissaoInicializarFalsa()
    {
        OpcoesPreValidacaoMigracao opcoes = CriarOpcoes(
            estadoInquilino: EstadoInquilino.Inativo,
            permissaoInicializarBruta: "false"); // allowInit é false, mas a base já possui marcador válido

        MarcadorBancoExistente marcadorCorrespondente = new(
            TabelaExiste: true,
            LinhaExiste: true,
            IdInquilino: IdInquilinoAlvo,
            IdAmbiente: IdAmbienteAlvo);

        PreValidacaoMigracaoInquilino.AvaliarEstadoBanco(opcoes, totalTabelasUsuario: 5, marcador: marcadorCorrespondente);
    }

    private static OpcoesPreValidacaoMigracao CriarOpcoes(
        EstadoInquilino estadoInquilino = EstadoInquilino.Inativo,
        string? permissaoInicializarBruta = null,
        Guid? idInquilinoCli = null,
        Guid? idAmbienteCli = null)
    {
        return new OpcoesPreValidacaoMigracao(
            IdAplicacao: "produto-teste",
            IdAmbiente: IdAmbienteAlvo,
            IdInquilino: IdInquilinoAlvo,
            EstadoInquilino: estadoInquilino,
            StringConexao: StringConexaoValida,
            PermissaoInicializarBancoVazioBruta: permissaoInicializarBruta,
            IdInquilinoCli: idInquilinoCli,
            IdAmbienteCli: idAmbienteCli);
    }
}
