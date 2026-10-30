namespace Platform.Infrastructure.Persistence.TenantAccess.Entities;

using Microsoft.AspNetCore.Identity;

/// <summary>
/// Entidade de usuário da aplicação vinculada exclusivamente a um inquilino (tenant).
/// Herda os campos padrão de autenticação do ASP.NET Core Identity (como Email, UserName, PasswordHash).
/// </summary>
public sealed class UsuarioAplicacao : IdentityUser<Guid>
{
    /// <summary>
    /// Identificador único e imutável do inquilino (tenant) proprietário desta conta de usuário.
    /// Funciona como chave estrangeira para a identidade do banco de dados do inquilino.
    /// </summary>
    public Guid IdInquilino { get; set; }

    /// <summary>
    /// Nome amigável de exibição do usuário na interface (deve conter entre 1 e 120 caracteres).
    /// </summary>
    public string NomeExibicao { get; set; } = string.Empty;

    /// <summary>
    /// Indicador booleano que define se a conta do usuário está ativa e autorizada a operar no sistema.
    /// </summary>
    public bool EstaAtivo { get; set; } = true;

    /// <summary>
    /// Coleção de permissões de acesso associadas diretamente a este usuário.
    /// </summary>
    public ICollection<PermissaoUsuario> Permissoes { get; } = new List<PermissaoUsuario>();

    /// <summary>
    /// Valida as invariantes obrigatórias dos campos da conta de usuário.
    /// </summary>
    public void Validar()
    {
        if (Id == Guid.Empty)
        {
            throw new ArgumentException("Id deve ser um UUID não vazio.", nameof(Id));
        }

        if (IdInquilino == Guid.Empty)
        {
            throw new ArgumentException("IdInquilino deve ser um UUID não vazio.", nameof(IdInquilino));
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
