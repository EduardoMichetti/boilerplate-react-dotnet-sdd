namespace Platform.Domain.Access;

/// <summary>
/// Operações restritas do host operacional registradas em <c>access.operacoes_acesso</c> (G7).
/// </summary>
public enum TipoOperacaoAcesso
{
    /// <summary>Criação de conta local.</summary>
    CriarConta = 0,

    /// <summary>Desativação de conta (revoga sessões).</summary>
    DesativarConta = 1,

    /// <summary>Reativação de conta (não ressuscita sessões).</summary>
    ReativarConta = 2,

    /// <summary>Redefinição de senha (revoga sessões e remove lockout).</summary>
    RedefinirSenha = 3,

    /// <summary>Revogação de todas as sessões da conta.</summary>
    RevogarSessoes = 4,

    /// <summary>Concessão de permissão do catálogo.</summary>
    ConcederPermissao = 5,

    /// <summary>Retirada de permissão concedida.</summary>
    RevogarPermissao = 6,

    /// <summary>Remoção de sessões encerradas além da retenção.</summary>
    LimparSessoes = 7
}
