namespace Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Representa uma permissão pontual de acesso concedida a um usuário dentro do escopo da instalação do inquilino.
/// </summary>
public sealed class PermissaoUsuario
{
    /// <summary>
    /// Identificador único do usuário que recebeu a permissão (chave estrangeira para UsuarioAplicacao).
    /// </summary>
    public Guid IdUsuario { get; set; }

    /// <summary>
    /// Código identificador da permissão concedida no sistema (de 1 a 128 caracteres, ex.: 'fundacao.contexto.ler').
    /// </summary>
    public string CodigoPermissao { get; set; } = string.Empty;

    /// <summary>
    /// Data e hora em UTC no momento em que a permissão foi concedida ao usuário.
    /// </summary>
    public DateTimeOffset ConcedidaEm { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Número de versão para controle de concorrência otimista durante concessões e revogações.
    /// </summary>
    public long Versao { get; set; }

    /// <summary>
    /// Propriedade de navegação para a entidade do usuário proprietário da concessão de permissão.
    /// </summary>
    public UsuarioAplicacao? Usuario { get; set; }

    /// <summary>
    /// Valida as invariantes obrigatórias dos campos de concessão da permissão.
    /// </summary>
    public void Validar()
    {
        if (IdUsuario == Guid.Empty)
        {
            throw new ArgumentException("IdUsuario deve ser um UUID não vazio.", nameof(IdUsuario));
        }

        if (string.IsNullOrWhiteSpace(CodigoPermissao) || CodigoPermissao.Length > 128)
        {
            throw new ArgumentException("CodigoPermissao deve conter entre 1 e 128 caracteres.", nameof(CodigoPermissao));
        }

        if (ConcedidaEm == default)
        {
            throw new ArgumentException("ConcedidaEm deve ser definido como um instante UTC válido.", nameof(ConcedidaEm));
        }
    }
}
