namespace Platform.Application.Access;

using Platform.Domain.Resultados;

/// <summary>
/// Catálogo de plataforma dos erros de acesso (únicos a usar <see cref="TipoErro.NaoAutenticado"/>, G5).
/// Códigos publicados nunca mudam de significado nem são reaproveitados.
/// </summary>
public static class ErrosAcesso
{
    /// <summary>
    /// Login recusado de forma genérica (tenant divergente/inativo, credencial inválida, conta bloqueada ou desativada).
    /// </summary>
    public static readonly Erro CredenciaisInvalidas = new("AUTH_INVALID", "Credenciais inválidas.", TipoErro.NaoAutenticado);

    /// <summary>
    /// Sessão ausente, inválida, expirada ou revogada.
    /// </summary>
    public static readonly Erro AutenticacaoNecessaria = new("AUTH_REQUIRED", "Autenticação necessária.", TipoErro.NaoAutenticado);

    /// <summary>
    /// Header de tenant divergente ou concessão local ausente.
    /// </summary>
    public static readonly Erro AcessoTenantNegado = new("TENANT_ACCESS_DENIED", "Acesso negado.", TipoErro.AcessoNegado);

    /// <summary>
    /// Instalação inativa: consultas suspensas para sessão válida.
    /// </summary>
    public static readonly Erro TenantIndisponivel = new("TENANT_UNAVAILABLE", "Instalação indisponível no momento.", TipoErro.Conflito);

    /// <summary>
    /// Login perdeu a disputa pela mesma sessão anterior.
    /// </summary>
    public static readonly Erro ConflitoSessao = new("SESSION_CONFLICT", "A sessão foi alterada por outra operação; obtenha um novo token e tente novamente.", TipoErro.Conflito);
}
