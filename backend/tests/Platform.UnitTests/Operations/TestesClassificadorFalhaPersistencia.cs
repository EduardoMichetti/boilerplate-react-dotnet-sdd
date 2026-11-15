namespace Platform.UnitTests.Operations;

using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Application.Access.Operacoes;
using Platform.Infrastructure.Access.Operacoes;
using Xunit;

/// <summary>
/// T133 (G29): classificação de exceções de persistência em conflito concorrente real ou falha interna.
/// </summary>
public sealed class TestesClassificadorFalhaPersistencia
{
    private readonly ClassificadorFalhaPersistencia _classificador = new();

    [Fact]
    public void ConcorrenciaOtimista_EhConflito() =>
        Assert.True(_classificador.EhConflitoConcorrente(new DbUpdateConcurrencyException("x")));

    [Theory]
    [InlineData(PostgresErrorCodes.UniqueViolation)]
    [InlineData(PostgresErrorCodes.SerializationFailure)]
    [InlineData(PostgresErrorCodes.DeadlockDetected)]
    public void EstadoPostgresDeConcorrencia_EmExcecaoInterna_EhConflito(string sqlState) =>
        Assert.True(_classificador.EhConflitoConcorrente(new DbUpdateException("x", CriarPostgres(sqlState))));

    [Theory]
    [InlineData(PostgresErrorCodes.CheckViolation)]
    [InlineData(PostgresErrorCodes.NotNullViolation)]
    [InlineData(PostgresErrorCodes.InsufficientPrivilege)]
    [InlineData(PostgresErrorCodes.StringDataRightTruncation)]
    public void OutroEstadoPostgres_NaoEhConflito(string sqlState) =>
        Assert.False(_classificador.EhConflitoConcorrente(new DbUpdateException("x", CriarPostgres(sqlState))));

    [Fact]
    public void ExcecaoSemRelacaoComPersistencia_NaoEhConflito() =>
        Assert.False(_classificador.EhConflitoConcorrente(new InvalidOperationException("x")));

    [Fact]
    public void FalhaClassificada_Conflito_UsaCodigoConflitoSemDetalheDoProvedor()
    {
        FalhaOperacaoClassificada falha = FalhaOperacaoClassificada.De(
            new DbUpdateException("SQL sensível", CriarPostgres(PostgresErrorCodes.UniqueViolation)), _classificador);

        Assert.Equal("CONFLITO_CONCORRENTE", falha.Codigo);
        Assert.DoesNotContain("SQL sensível", falha.MensagemDiagnostico, StringComparison.Ordinal);
    }

    [Fact]
    public void FalhaClassificada_Inesperada_UsaFalhaInternaComTipoEMensagemNoDiagnostico()
    {
        FalhaOperacaoClassificada falha = FalhaOperacaoClassificada.De(
            new InvalidOperationException("causa real"), _classificador);

        Assert.Equal("FALHA_INTERNA", falha.Codigo);
        Assert.Contains(nameof(InvalidOperationException), falha.MensagemDiagnostico, StringComparison.Ordinal);
        Assert.Contains("causa real", falha.MensagemDiagnostico, StringComparison.Ordinal);
    }

    private static PostgresException CriarPostgres(string sqlState) =>
        new("mensagem sintética", "ERROR", "ERROR", sqlState);
}
