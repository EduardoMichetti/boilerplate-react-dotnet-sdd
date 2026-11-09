namespace Platform.Domain.Auditoria;

/// <summary>
/// Origem da autoria de uma criação ou alteração auditada.
/// </summary>
public enum TipoAutor
{
    /// <summary>
    /// Operador individual autenticado no host operacional (login PostgreSQL individual).
    /// </summary>
    Operador = 0,

    /// <summary>
    /// Usuário da aplicação com credencial verificada.
    /// </summary>
    Usuario = 1,

    /// <summary>
    /// Identidade técnica do sistema (sem produtor na L2).
    /// </summary>
    Sistema = 2,

    /// <summary>
    /// Ação anônima (ex.: tentativa de login ainda não verificada); autor nulo.
    /// </summary>
    Anonimo = 3
}
