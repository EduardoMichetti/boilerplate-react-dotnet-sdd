namespace Platform.Infrastructure.Persistence.TenantAccess.Entities;

using Platform.Domain.Access;
using Platform.Domain.Auditoria;

/// <summary>
/// Concessão imutável de uma permissão do catálogo a uma conta local. Só INSERT/DELETE:
/// <see cref="EntidadeAuditavel.CriadoEm"/> é o instante da concessão e os metadados de
/// alteração permanecem nulos.
/// </summary>
public sealed class PermissaoUsuario : EntidadeAuditavel
{
    /// <summary>
    /// FK local para a conta (parte da PK).
    /// </summary>
    public Guid FkIdUsuario { get; set; }

    /// <summary>
    /// Código do catálogo <see cref="CodigosPermissao"/> (1 a 128 caracteres; parte da PK).
    /// </summary>
    public string CodigoPermissao { get; set; } = string.Empty;

    /// <summary>
    /// Navegação para a conta que recebeu a concessão.
    /// </summary>
    public UsuarioAplicacao? Usuario { get; set; }

    /// <summary>
    /// Valida as invariantes de negócio da concessão.
    /// </summary>
    public void Validar()
    {
        if (FkIdUsuario == Guid.Empty)
        {
            throw new ArgumentException("FkIdUsuario deve ser um UUID não vazio.", nameof(FkIdUsuario));
        }

        if (!CodigosPermissao.FormatoValido(CodigoPermissao))
        {
            throw new ArgumentException("CodigoPermissao deve ter de 1 a 128 caracteres no formato do catálogo.", nameof(CodigoPermissao));
        }

        if (!CodigosPermissao.EhConhecido(CodigoPermissao))
        {
            throw new ArgumentException("CodigoPermissao não pertence ao catálogo.", nameof(CodigoPermissao));
        }
    }
}
