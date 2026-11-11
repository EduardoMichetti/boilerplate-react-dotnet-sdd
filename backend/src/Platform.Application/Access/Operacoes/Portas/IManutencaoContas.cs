namespace Platform.Application.Access.Operacoes.Portas;

/// <summary>
/// Resultado da execução de uma ação de manutenção de conta ou permissão.
/// </summary>
/// <param name="Sucesso">Indica se a mutação foi aplicada ou se foi no-op idempotente auditado.</param>
/// <param name="Codigo">Código de resultado seguro (ex.: SUCESSO, USUARIO_NAO_ENCONTRADO, CONFLITO_CONCORRENTE).</param>
/// <param name="Mensagem">Mensagem de diagnóstico opcional.</param>
public sealed record ResultadoAcaoConta(
    bool Sucesso,
    string Codigo,
    string? Mensagem = null)
{
    /// <summary>Sucesso na operação.</summary>
    public static ResultadoAcaoConta Ok() => new(true, "SUCESSO");

    /// <summary>Conta não encontrada.</summary>
    public static ResultadoAcaoConta UsuarioNaoEncontrado() =>
        new(false, "USUARIO_NAO_ENCONTRADO", "Usuário não encontrado.");

    /// <summary>Senha fora da política configurada.</summary>
    public static ResultadoAcaoConta SenhaForaDaPolitica(string mensagem) =>
        new(false, "SENHA_FORA_DA_POLITICA", mensagem);

    /// <summary>Conflito concorrente (PK de permissão, ConcurrencyStamp ou zero linhas na retirada).</summary>
    public static ResultadoAcaoConta ConflitoConcorrente(string mensagem) =>
        new(false, "CONFLITO_CONCORRENTE", mensagem);

    /// <summary>Permissão desconhecida fora do catálogo.</summary>
    public static ResultadoAcaoConta PermissaoDesconhecida(string codigo) =>
        new(false, "PERMISSAO_DESCONHECIDA", $"Permissão '{codigo}' não pertence ao catálogo.");

    /// <summary>Falha genérica com código e mensagem seguros.</summary>
    public static ResultadoAcaoConta Falha(string codigo, string mensagem) =>
        new(false, codigo, mensagem);
}

/// <summary>
/// Porta para operações restritas de manutenção de contas e concessão/revogação de permissões.
/// Todas as mutações participam da transação gerenciada por <see cref="IRegistroOperacoesAcesso"/>.
/// </summary>
public interface IManutencaoContas
{
    /// <summary>
    /// Cria uma nova conta local com Id v7 pré-atribuído, sem concessão automática de permissões.
    /// </summary>
    Task<ResultadoAcaoConta> CriarContaAsync(
        Guid idUsuario,
        string email,
        string nomeExibicao,
        string senha,
        CancellationToken tokenCancelamento = default);

    /// <summary>
    /// Desativa a conta local, atualiza o SecurityStamp e revoga todas as suas sessões.
    /// </summary>
    Task<ResultadoAcaoConta> DesativarContaAsync(
        Guid idUsuario,
        CancellationToken tokenCancelamento = default);

    /// <summary>
    /// Reativa a conta local (não ressuscita sessões).
    /// </summary>
    Task<ResultadoAcaoConta> ReativarContaAsync(
        Guid idUsuario,
        CancellationToken tokenCancelamento = default);

    /// <summary>
    /// Remove o lockout e a contagem de falhas, redefine a senha pelo UserManager,
    /// atualiza o SecurityStamp e revoga todas as sessões da conta.
    /// </summary>
    Task<ResultadoAcaoConta> RedefinirSenhaAsync(
        Guid idUsuario,
        string novaSenha,
        CancellationToken tokenCancelamento = default);

    /// <summary>
    /// Revoga todas as sessões ativas da conta e atualiza o SecurityStamp.
    /// </summary>
    Task<ResultadoAcaoConta> RevogarSessoesAsync(
        Guid idUsuario,
        CancellationToken tokenCancelamento = default);

    /// <summary>
    /// Concede permissão do catálogo à conta local. Duplicatas são no-op com sucesso.
    /// </summary>
    Task<ResultadoAcaoConta> ConcederPermissaoAsync(
        Guid idUsuario,
        string codigoPermissao,
        CancellationToken tokenCancelamento = default);

    /// <summary>
    /// Revoga permissão concedida da conta local. Ausência é no-op com sucesso.
    /// </summary>
    Task<ResultadoAcaoConta> RevogarPermissaoAsync(
        Guid idUsuario,
        string codigoPermissao,
        CancellationToken tokenCancelamento = default);
}
