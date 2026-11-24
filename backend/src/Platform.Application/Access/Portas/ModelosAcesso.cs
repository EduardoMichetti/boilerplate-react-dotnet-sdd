namespace Platform.Application.Access.Portas;

/// <summary>
/// Referência à sessão autenticada da requisição corrente, com a <c>Versao</c> lida na autenticação
/// (token esperado na troca atômica do login e na revogação do logout).
/// </summary>
/// <param name="IdSessao">Sessão do cookie recebido.</param>
/// <param name="IdUsuario">Conta dona da sessão.</param>
/// <param name="Versao">Versão lida na autenticação.</param>
/// <param name="TetoAbsolutoEm">Teto absoluto gravado na emissão.</param>
public sealed record SessaoCorrente(Guid IdSessao, Guid IdUsuario, long Versao, DateTimeOffset TetoAbsolutoEm);

/// <summary>
/// Dados da nova sessão montados pelo caso de uso de login antes da troca atômica.
/// </summary>
/// <param name="IdSessao">UUID v7 do <c>InstanteOperacao</c>.</param>
/// <param name="IdUsuario">Conta com credencial verificada.</param>
/// <param name="EpocaAcesso">Geração vigente no marcador.</param>
/// <param name="TetoAbsolutoEm"><c>InstanteOperacao + Sessao:TetoAbsolutoMinutos</c>.</param>
/// <param name="JanelaInatividadeMinutos"><c>Sessao:InatividadeMinutos</c> vigente na emissão.</param>
/// <param name="TicketProtegido">Ticket nativo protegido por Data Protection.</param>
public sealed record NovaSessao(Guid IdSessao, Guid IdUsuario, Guid EpocaAcesso, DateTimeOffset TetoAbsolutoEm, int JanelaInatividadeMinutos, byte[] TicketProtegido);

/// <summary>
/// Sessão emitida e confirmada no banco (commit antes do retorno).
/// </summary>
/// <param name="IdSessao">Sessão criada.</param>
/// <param name="IdUsuario">Conta dona da sessão.</param>
/// <param name="IdTenant">Tenant da instalação.</param>
/// <param name="EpocaAcesso">Geração gravada na sessão.</param>
/// <param name="TetoAbsolutoEm">Teto absoluto gravado.</param>
public sealed record SessaoEmitida(Guid IdSessao, Guid IdUsuario, Guid IdTenant, Guid EpocaAcesso, DateTimeOffset TetoAbsolutoEm);

/// <summary>
/// Dados que compõem o ticket persistido: só referências e o carimbo de segurança, sem concessões.
/// </summary>
/// <param name="IdSessao">Sessão.</param>
/// <param name="IdUsuario">Conta.</param>
/// <param name="IdTenant">Tenant da instalação.</param>
/// <param name="EpocaAcesso">Geração do marcador na emissão.</param>
/// <param name="CarimboSeguranca">SecurityStamp vigente na emissão.</param>
/// <param name="EmitidaEm">Instante da emissão.</param>
/// <param name="TetoAbsolutoEm">Teto absoluto.</param>
public sealed record DadosTicketSessao(
    Guid IdSessao,
    Guid IdUsuario,
    Guid IdTenant,
    Guid EpocaAcesso,
    string CarimboSeguranca,
    DateTimeOffset EmitidaEm,
    DateTimeOffset TetoAbsolutoEm);

/// <summary>
/// Estado persistido de uma sessão e da conta dona, lido do banco a cada operação.
/// </summary>
/// <param name="IdSessao">Sessão.</param>
/// <param name="IdUsuario">Conta.</param>
/// <param name="EpocaAcesso">Geração gravada na sessão.</param>
/// <param name="TetoAbsolutoEm">Teto absoluto gravado.</param>
/// <param name="RevogadaEm">Instante da revogação, quando revogada.</param>
/// <param name="Versao">Token de concorrência.</param>
/// <param name="TicketProtegido">Ticket protegido (nulo após revogação).</param>
/// <param name="ContaAtiva">Conta habilitada.</param>
/// <param name="BloqueioAte">Fim do lockout, quando houver.</param>
/// <param name="BloqueioHabilitado">Lockout habilitado na conta.</param>
/// <param name="CarimboSeguranca">SecurityStamp atual da conta.</param>
public sealed record EstadoSessaoPersistida(
    Guid IdSessao,
    Guid IdUsuario,
    Guid EpocaAcesso,
    DateTimeOffset TetoAbsolutoEm,
    DateTimeOffset? RevogadaEm,
    long Versao,
    byte[]? TicketProtegido,
    bool ContaAtiva,
    DateTimeOffset? BloqueioAte,
    bool BloqueioHabilitado,
    string? CarimboSeguranca);

/// <summary>
/// Identidade corrente devolvida por <c>GET /api/v1/me</c>.
/// </summary>
/// <param name="IdUsuario">Conta.</param>
/// <param name="IdTenant">Tenant da instalação.</param>
/// <param name="Email">Email da conta (dado pessoal).</param>
/// <param name="NomeExibicao">Nome de exibição (dado pessoal).</param>
/// <param name="SessaoExpiraEm">Prazo absoluto da sessão corrente.</param>
public sealed record IdentidadeAtual(Guid IdUsuario, Guid IdTenant, string Email, string NomeExibicao, DateTimeOffset SessaoExpiraEm);

/// <summary>
/// Contexto de acesso devolvido por <c>GET /api/v1/context</c>.
/// </summary>
/// <param name="Usuario">Identidade corrente.</param>
/// <param name="IdTenant">Tenant da instalação.</param>
/// <param name="NomeExibicaoTenant">Nome de apresentação da instalação.</param>
/// <param name="Permissoes">Concessões vigentes lidas agora no banco.</param>
public sealed record ContextoAcesso(IdentidadeAtual Usuario, Guid IdTenant, string NomeExibicaoTenant, IReadOnlyList<string> Permissoes);

/// <summary>
/// Leitura da conta e das concessões vigentes, sem cache.
/// </summary>
/// <param name="IdUsuario">Conta.</param>
/// <param name="Email">Email.</param>
/// <param name="NomeExibicao">Nome de exibição.</param>
/// <param name="Permissoes">Códigos concedidos.</param>
public sealed record DadosContaAcesso(Guid IdUsuario, string Email, string NomeExibicao, IReadOnlyList<string> Permissoes);

/// <summary>
/// Resultado tipado da verificação de credenciais, sem revelar o motivo ao consumidor.
/// </summary>
/// <param name="Valida">Credencial válida para conta ativa e não bloqueada.</param>
/// <param name="IdUsuario">Conta verificada (vazio quando inválida).</param>
/// <param name="CarimboSeguranca">SecurityStamp vigente (nulo quando inválida).</param>
public sealed record ResultadoCredenciais(bool Valida, Guid IdUsuario, string? CarimboSeguranca)
{
    /// <summary>
    /// Resultado genérico de credencial inválida.
    /// </summary>
    public static ResultadoCredenciais Invalida { get; } = new(false, Guid.Empty, null);
}
