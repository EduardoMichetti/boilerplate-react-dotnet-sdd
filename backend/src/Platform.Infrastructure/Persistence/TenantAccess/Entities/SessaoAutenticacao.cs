namespace Platform.Infrastructure.Persistence.TenantAccess.Entities;

using Platform.Application.Configuration;
using Platform.Domain.Auditoria;
using Platform.Domain.Tempo;

/// <summary>
/// Sessão autenticada persistida e revogável (L2.1, D31): expira por inatividade
/// (<see cref="UltimoUsoEm"/> + <see cref="JanelaInatividadeMinutos"/>) ou pelo
/// <see cref="TetoAbsolutoEm"/>, o que ocorrer primeiro; teto e janela são fixados na emissão.
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
    /// UTC, <c>CriadoEm</c> + <c>Sessao:TetoAbsolutoMinutos</c> vigente na emissão (padrão 600); imutável.
    /// </summary>
    public DateTimeOffset TetoAbsolutoEm { get; set; }

    /// <summary>
    /// Inteiro, <c>Sessao:InatividadeMinutos</c> vigente na emissão (padrão 30); imutável.
    /// </summary>
    public int JanelaInatividadeMinutos { get; set; }

    /// <summary>
    /// UTC; igual a <c>CriadoEm</c> na emissão; atualizado somente pelo registro de uso
    /// (no máximo 1 escrita/minuto); só cresce.
    /// </summary>
    public DateTimeOffset UltimoUsoEm { get; set; }

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
    /// Indica se a sessão não está revogada e o teto absoluto ainda não venceu
    /// (<c>agora &lt; TetoAbsolutoEm</c>). A inatividade passa a ser aplicada no R2 (Phase 21).
    /// </summary>
    /// <param name="agora">Instante atual vindo do <see cref="TimeProvider"/>.</param>
    /// <returns><c>true</c> quando ativa.</returns>
    public bool EstaAtivaEm(DateTimeOffset agora) => RevogadaEm is null && agora < TetoAbsolutoEm;

    /// <summary>
    /// Registra uso da sessão: avança <see cref="UltimoUsoEm"/> para <paramref name="instante"/>.
    /// Nunca altera teto, janela, ticket ou revogação. A limitação a 1 escrita/minuto é do chamador
    /// (<c>ExpiracaoSessao.DeveRegistrarUso</c>).
    /// </summary>
    /// <param name="instante">Instante da operação (UTC), não anterior ao último uso e anterior ao teto.</param>
    /// <exception cref="ArgumentException">Instante fora de UTC.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Instante anterior ao último uso ou não anterior ao teto.</exception>
    public void RegistrarUso(DateTimeOffset instante)
    {
        InstanteUtc.Validar(instante, nameof(instante));

        if (instante < UltimoUsoEm)
        {
            throw new ArgumentOutOfRangeException(nameof(instante), "UltimoUsoEm só cresce.");
        }

        if (instante >= TetoAbsolutoEm)
        {
            throw new ArgumentOutOfRangeException(nameof(instante), "Uso não pode ser registrado a partir do teto absoluto.");
        }

        UltimoUsoEm = instante;
    }

    /// <summary>
    /// Valida as invariantes de negócio da sessão (mesmas regras dos CHECKs do banco).
    /// </summary>
    /// <exception cref="ArgumentException">Invariante violada.</exception>
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

        InstanteUtc.Validar(CriadoEm, nameof(CriadoEm));
        InstanteUtc.Validar(TetoAbsolutoEm, nameof(TetoAbsolutoEm));
        InstanteUtc.Validar(UltimoUsoEm, nameof(UltimoUsoEm));

        TimeSpan teto = TetoAbsolutoEm - CriadoEm;
        if (teto < TimeSpan.FromMinutes(OpcoesSessao.TetoMinimoMinutos) || teto > TimeSpan.FromMinutes(OpcoesSessao.TetoMaximoMinutos))
        {
            throw new ArgumentException(
                $"TetoAbsolutoEm deve estar entre {OpcoesSessao.TetoMinimoMinutos} e {OpcoesSessao.TetoMaximoMinutos} minutos após CriadoEm.",
                nameof(TetoAbsolutoEm));
        }

        if (JanelaInatividadeMinutos < OpcoesSessao.InatividadeMinimaMinutos
            || JanelaInatividadeMinutos > OpcoesSessao.InatividadeMaximaMinutos
            || TimeSpan.FromMinutes(JanelaInatividadeMinutos) > teto)
        {
            throw new ArgumentException(
                $"JanelaInatividadeMinutos deve estar entre {OpcoesSessao.InatividadeMinimaMinutos} e {OpcoesSessao.InatividadeMaximaMinutos} e não exceder o teto.",
                nameof(JanelaInatividadeMinutos));
        }

        if (UltimoUsoEm < CriadoEm || UltimoUsoEm >= TetoAbsolutoEm)
        {
            throw new ArgumentException("UltimoUsoEm deve estar em [CriadoEm, TetoAbsolutoEm).", nameof(UltimoUsoEm));
        }

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
