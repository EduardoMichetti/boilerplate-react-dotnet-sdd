namespace Platform.Application.Access.CasosUso;

using Microsoft.Extensions.Options;
using Platform.Application.Access.Contratos;
using Platform.Application.Access.Portas;
using Platform.Application.Auditoria;
using Platform.Application.Configuration;
using Platform.Domain.Identificadores;
using Platform.Domain.Resultados;
using Platform.Domain.Tenancy;

/// <summary>
/// Login local (<c>POST /api/v1/sessions</c>): instalação ativa → credenciais → troca atômica com a
/// sessão do cookie recebido. Executado depois de tenant, marcador, corpo e CSRF (G2).
/// </summary>
/// <param name="instalacao">Configuração da instalação.</param>
/// <param name="opcoesSessao">Prazo absoluto aplicado na emissão.</param>
/// <param name="contexto">Contexto da operação (instante único).</param>
/// <param name="credenciais">Verificação de senha e lockout.</param>
/// <param name="sessoes">Persistência das sessões.</param>
/// <param name="protetor">Proteção do ticket persistido.</param>
public sealed class CasoUsoLogin(
    IOptions<OpcoesInstalacaoTenant> instalacao,
    IOptions<OpcoesSessao> opcoesSessao,
    IContextoOperacao contexto,
    IServicoCredenciais credenciais,
    IRepositorioSessoes sessoes,
    IProtetorTicketSessao protetor)
{
    /// <summary>
    /// Executa o login.
    /// </summary>
    /// <param name="requisicao">Corpo já validado.</param>
    /// <param name="anterior">Sessão válida do cookie recebido, quando houver.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    /// <returns>Sessão emitida, <c>AUTH_INVALID</c> genérico ou <c>SESSION_CONFLICT</c>.</returns>
    public async Task<Resultado<SessaoEmitida>> ExecutarAsync(
        RequisicaoLogin requisicao,
        SessaoCorrente? anterior,
        CancellationToken tokenCancelamento)
    {
        ArgumentNullException.ThrowIfNull(requisicao);
        OpcoesInstalacaoTenant config = instalacao.Value;

        // Instalação inativa: genérico, sem verificar senha nem tocar a conta.
        if (config.EstadoTenant != EstadoTenant.Ativo)
        {
            return ErrosAcesso.CredenciaisInvalidas;
        }

        ResultadoCredenciais verificacao = await credenciais.VerificarAsync(requisicao.Email, requisicao.Senha, tokenCancelamento);
        if (!verificacao.Valida || verificacao.CarimboSeguranca is null)
        {
            return ErrosAcesso.CredenciaisInvalidas;
        }

        DateTimeOffset emitidaEm = contexto.InstanteOperacao;
        DateTimeOffset expiraEm = emitidaEm.AddMinutes(opcoesSessao.Value.PrazoAbsolutoMinutos);
        Guid idSessao = IdentificadorV7.Gerar(emitidaEm);
        Guid epoca = await sessoes.ObterEpocaAcessoAsync(tokenCancelamento);

        byte[] ticket = protetor.Proteger(new DadosTicketSessao(
            idSessao,
            verificacao.IdUsuario,
            config.IdTenant,
            epoca,
            verificacao.CarimboSeguranca,
            emitidaEm,
            expiraEm));

        return await sessoes.EmitirAsync(
            new NovaSessao(idSessao, verificacao.IdUsuario, epoca, expiraEm, ticket),
            anterior,
            tokenCancelamento);
    }
}
