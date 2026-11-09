namespace Platform.Infrastructure.Persistence.TenantAccess.Entities;

using Platform.Domain.Auditoria;
using Platform.Domain.Tempo;

/// <summary>
/// Sessão autenticada persistida e revogável, com expiração absoluta fixada na emissão.
/// Alterada somente de forma rastreada, com <see cref="Versao"/> como token de concorrência.
/// </summary>
public sealed class SessaoAutenticacao : EntidadeAuditavel, IEntidadeVersionada
{
    /// <summary>
    /// UUID v7 não vazio, PK opaca gerada pelo servidor.
    /// </summary>
    public Guid IdSessao { get; set; }

    /// <summary>
    /// FK local para a conta (exclusão restrita para preservar rastreabilidade).
    /// </summary>
    public Guid FkIdUsuario { get; set; }

    /// <summary>
    /// UUID não vazio, cópia da geração (<c>EpocaAcesso</c>) vigente na emissão.
    /// </summary>
    public Guid EpocaAcesso { get; set; }

    /// <summary>
    /// <c>bytea</c> anulável: ticket nativo protegido por Data Protection; inserido na emissão e removido na revogação.
    /// </summary>
    public byte[]? TicketProtegido { get; set; }

    /// <summary>
    /// <c>CriadoEm</c> + <c>Sessao:PrazoAbsolutoMinutos</c> vigente na emissão (padrão 30); imutável.
    /// </summary>
    public DateTimeOffset ExpiraEm { get; set; }

    /// <summary>
    /// UTC anulável: estado da sessão, preenchido na revogação com o mesmo instante de <c>AlteradoEm</c>.
    /// </summary>
    public DateTimeOffset? RevogadaEm { get; set; }

    /// <summary>
    /// int64 positivo, token de concorrência incrementado pelo <c>InterceptadorAuditoria</c>.
    /// </summary>
    public long Versao { get; set; } = 1;

    /// <summary>
    /// Navegação para a conta dona da sessão.
    /// </summary>
    public UsuarioAplicacao? Usuario { get; set; }

    /// <summary>
    /// Indica se a sessão ainda autoriza uso no instante informado (não revogada e <c>agora &lt; ExpiraEm</c>).
    /// </summary>
    /// <param name="agora">Instante atual vindo do <see cref="TimeProvider"/>.</param>
    /// <returns><c>true</c> quando ativa.</returns>
    public bool EstaAtivaEm(DateTimeOffset agora) => RevogadaEm is null && agora < ExpiraEm;

    /// <summary>
    /// Valida as invariantes de negócio da sessão.
    /// </summary>
    public void Validar()
    {
        if (IdSessao == Guid.Empty)
        {
            throw new ArgumentException("IdSessao deve ser um UUID não vazio.", nameof(IdSessao));
        }

        if (FkIdUsuario == Guid.Empty)
        {
            throw new ArgumentException("FkIdUsuario deve ser um UUID não vazio.", nameof(FkIdUsuario));
        }

        if (EpocaAcesso == Guid.Empty)
        {
            throw new ArgumentException("EpocaAcesso deve ser um UUID não vazio.", nameof(EpocaAcesso));
        }

        InstanteUtc.Validar(ExpiraEm, nameof(ExpiraEm));

        if (RevogadaEm is { } revogadaEm)
        {
            InstanteUtc.Validar(revogadaEm, nameof(RevogadaEm));
            if (TicketProtegido is not null)
            {
                throw new ArgumentException("Sessão revogada não pode manter TicketProtegido.", nameof(TicketProtegido));
            }
        }

        if (Versao <= 0)
        {
            throw new ArgumentException("Versao deve ser positiva.", nameof(Versao));
        }
    }
}
