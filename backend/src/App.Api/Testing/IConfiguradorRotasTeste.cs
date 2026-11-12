namespace App.Api.Testing;

using Microsoft.AspNetCore.Routing;

/// <summary>
/// Ponto de extensão inerte em produção para registro de rotas sintéticas em testes de carga e contrato (T117).
/// Na API distribuída nenhuma implementação é registrada.
/// </summary>
public interface IConfiguradorRotasTeste
{
    /// <summary>
    /// Configura rotas adicionais no grupo /api/v1.
    /// </summary>
    /// <param name="grupo">Grupo /api/v1 configurado com filtros e autenticação.</param>
    void Configurar(RouteGroupBuilder grupo);
}
