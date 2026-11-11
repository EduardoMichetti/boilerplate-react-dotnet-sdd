namespace Platform.Application.Access.Operacoes.Portas;

using Platform.Domain.Access;

/// <summary>
/// Dados para gravação no histórico append-only <c>access.operacoes_acesso</c>.
/// </summary>
public sealed record DadosRegistroOperacao(
    Guid IdOperacao,
    Guid IdTenant,
    Guid IdAmbiente,
    Guid? IdUsuarioAlvo,
    string LoginOperador,
    TipoOperacaoAcesso TipoOperacao,
    string? CodigoPermissao,
    DateTimeOffset RegistradaEm,
    ResultadoOperacaoAcesso Resultado,
    string CodigoResultado,
    int? RetencaoDias = null,
    DateTimeOffset? InstanteCorte = null,
    int? QuantidadeRemovida = null);

/// <summary>
/// Transação atômica que engloba a mutação da operação e o registro de auditoria de sucesso.
/// </summary>
public interface ITransacaoOperacaoAcesso : IAsyncDisposable
{
    /// <summary>
    /// Adiciona o registro de sucesso na transação e realiza o commit atômico de todas as mutações.
    /// </summary>
    Task RegistrarSucessoEConfirmarAsync(DadosRegistroOperacao registro, CancellationToken tokenCancelamento = default);

    /// <summary>
    /// Desfaz a transação em caso de erro ou cancelamento.
    /// </summary>
    Task DesfazerAsync(CancellationToken tokenCancelamento = default);
}

/// <summary>
/// Porta para gravação no histórico append-only de auditoria de operações restritas.
/// </summary>
public interface IRegistroOperacoesAcesso
{
    /// <summary>
    /// Inicia uma transação no banco da instalação para coordenar a mutação e a auditoria de sucesso.
    /// </summary>
    Task<ITransacaoOperacaoAcesso> IniciarTransacaoAsync(CancellationToken tokenCancelamento = default);

    /// <summary>
    /// Registra um evento de Falha ou Negação em transação independente (quando o alvo já foi validado).
    /// </summary>
    Task RegistrarFalhaOuNegadoEmTransacaoSeparadaAsync(DadosRegistroOperacao registro, CancellationToken tokenCancelamento = default);
}
