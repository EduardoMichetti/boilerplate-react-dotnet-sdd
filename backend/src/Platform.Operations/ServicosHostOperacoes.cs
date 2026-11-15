namespace Platform.Operations;

using Microsoft.Extensions.DependencyInjection;
using Platform.Application.Auditoria;
using Platform.Infrastructure;

/// <summary>
/// Composição de serviços do host operacional, compartilhada pelo <c>Program.cs</c> e pelos
/// testes de integração para que ambos exercitem a mesma configuração.
/// O chamador fornece <see cref="TimeProvider"/>, <c>IProvedorSenha</c> e as opções da instalação.
/// </summary>
public static class ServicosHostOperacoes
{
    /// <summary>
    /// Registra o contexto de operação do operador (uma instância por escopo, exposta como
    /// <see cref="IContextoOperacao"/> e <see cref="IRegistroOperadorVerificado"/>), a
    /// infraestrutura de acesso e os casos de uso de operações restritas.
    /// </summary>
    /// <param name="servicos">Coleção de serviços.</param>
    /// <returns>A mesma coleção.</returns>
    public static IServiceCollection AdicionarServicosHostOperacoes(this IServiceCollection servicos)
    {
        ArgumentNullException.ThrowIfNull(servicos);

        servicos.AddScoped<ContextoOperacaoOperador>();
        servicos.AddScoped<IContextoOperacao>(p => p.GetRequiredService<ContextoOperacaoOperador>());
        servicos.AddScoped<IRegistroOperadorVerificado>(p => p.GetRequiredService<ContextoOperacaoOperador>());

        servicos.AdicionarInfraestruturaAcesso();
        servicos.AdicionarCasosUsoOperacoes();

        return servicos;
    }
}
