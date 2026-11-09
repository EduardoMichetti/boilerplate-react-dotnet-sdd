namespace Platform.Domain.Resultados;

/// <summary>
/// Categoria de uma falha de regra esperada; define o status HTTP num único ponto da API (ADR 0003 D5).
/// </summary>
public enum TipoErro
{
    /// <summary>Recurso inexistente (404).</summary>
    NaoEncontrado = 0,

    /// <summary>Conflita com o estado atual do recurso (409).</summary>
    Conflito = 1,

    /// <summary>Viola regra independentemente do estado (422).</summary>
    RegraNegocio = 2,

    /// <summary>Acesso negado ao recurso (403).</summary>
    AcessoNegado = 3,

    /// <summary>Não autenticado (401) — somente no catálogo de plataforma <c>ErrosAcesso</c>.</summary>
    NaoAutenticado = 4
}
