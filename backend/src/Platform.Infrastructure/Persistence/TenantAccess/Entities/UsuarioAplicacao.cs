namespace Platform.Infrastructure.Persistence.TenantAccess.Entities;

using Microsoft.AspNetCore.Identity;
using Platform.Domain.Auditoria;

/// <summary>
/// Conta local da aplicação, exclusiva desta instalação. Mantém <see cref="IdentityUser{TKey}"/>
/// (Email, UserName, PasswordHash, SecurityStamp, ConcurrencyStamp, lockout) e implementa os
/// metadados de auditoria. Propriedades herdadas não são renomeáveis: as colunas são mapeadas
/// para o padrão (<c>Id</c> → <c>IdUsuario</c>).
/// </summary>
public sealed class UsuarioAplicacao : IdentityUser<Guid>, IEntidadeAuditavel
{
    /// <summary>
    /// FK para <c>identidade_banco.IdTenant</c>: tenant proprietário desta conta (mesma instalação).
    /// </summary>
    public Guid FkIdTenant { get; set; }

    /// <summary>
    /// Nome de exibição obrigatório (1 a 120 caracteres).
    /// </summary>
    public string NomeExibicao { get; set; } = string.Empty;

    /// <summary>
    /// Conta habilitada; <c>false</c> nega login e próximas operações protegidas.
    /// </summary>
    public bool EstaAtivo { get; set; } = true;

    /// <summary>
    /// Concessões vigentes da conta.
    /// </summary>
    public ICollection<PermissaoUsuario> Permissoes { get; } = new List<PermissaoUsuario>();

    /// <inheritdoc />
    public DateTimeOffset CriadoEm { get; set; }

    /// <inheritdoc />
    public TipoAutor TipoAutorCriacao { get; set; }

    /// <inheritdoc />
    public string? AutorCriacao { get; set; }

    /// <inheritdoc />
    public Guid IdOperacaoCriacao { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? AlteradoEm { get; set; }

    /// <inheritdoc />
    public TipoAutor? TipoAutorAlteracao { get; set; }

    /// <inheritdoc />
    public string? AutorAlteracao { get; set; }

    /// <inheritdoc />
    public Guid? IdOperacaoAlteracao { get; set; }

    /// <summary>
    /// Valida as invariantes de negócio da conta (metadados de auditoria são validados pelo interceptor).
    /// </summary>
    public void Validar()
    {
        if (Id == Guid.Empty)
        {
            throw new ArgumentException("Id deve ser um UUID não vazio.", nameof(Id));
        }

        if (FkIdTenant == Guid.Empty)
        {
            throw new ArgumentException("FkIdTenant deve ser um UUID não vazio.", nameof(FkIdTenant));
        }

        if (string.IsNullOrWhiteSpace(NomeExibicao) || NomeExibicao.Length > 120)
        {
            throw new ArgumentException("NomeExibicao deve conter entre 1 e 120 caracteres.", nameof(NomeExibicao));
        }

        if (string.IsNullOrWhiteSpace(Email) || Email.Length > 254)
        {
            throw new ArgumentException("Email deve conter entre 1 e 254 caracteres.", nameof(Email));
        }
    }
}
