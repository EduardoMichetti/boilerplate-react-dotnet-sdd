namespace Platform.Infrastructure.Access.Operacoes;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using Platform.Application.Access.Operacoes.Portas;
using Platform.Application.Auditoria;
using Platform.Application.Configuration;
using Platform.Application.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess;

/// <summary>
/// Verificador de autorização do operador contra a conexão e o marcador do banco PostgreSQL.
/// Ao autorizar, define o login confirmado como autor da operação antes de qualquer gravação.
/// </summary>
public sealed class VerificadorAutorizacaoOperador(
    AcessoTenantDbContext contexto,
    IVerificadorIdentidadeBancoTenant verificadorIdentidade,
    IOptions<OpcoesInstalacaoTenant> opcoesInstalacao,
    IOptions<OpcoesOperacaoAcesso> opcoesOperacao,
    IRegistroOperadorVerificado registroOperador) : IVerificadorAutorizacaoOperador
{
    /// <inheritdoc />
    public async Task<ResultadoAutorizacaoOperador> VerificarAutorizacaoAsync(
        Guid idTenant,
        Guid idAmbiente,
        CancellationToken tokenCancelamento = default)
    {
        if (idTenant != opcoesInstalacao.Value.IdTenant || idAmbiente != opcoesInstalacao.Value.IdAmbiente)
        {
            return ResultadoAutorizacaoOperador.Negado(
                string.Empty,
                "ALVO_DIVERGENTE",
                "IdTenant ou IdAmbiente divergente da configuração da instalação.");
        }

        IdentidadeBancoTenantSnapshot snapshot;
        try
        {
            snapshot = await verificadorIdentidade.LerAsync(tokenCancelamento);
        }
        catch (Exception ex)
        {
            return ResultadoAutorizacaoOperador.Negado(
                string.Empty,
                "ALVO_DIVERGENTE",
                $"Falha ao ler marcador do banco de dados: {ex.Message}");
        }

        if (snapshot.IdTenant != idTenant || snapshot.IdAmbiente != idAmbiente || !snapshot.PossuiVersaoMigracaoEsperada)
        {
            return ResultadoAutorizacaoOperador.Negado(
                string.Empty,
                "ALVO_DIVERGENTE",
                "Marcador de identidade do banco não coincide com os argumentos ou cadeia de migrações incompleta.");
        }

        DbConnection conexao = contexto.Database.GetDbConnection();
        if (conexao.State != System.Data.ConnectionState.Open)
        {
            await contexto.Database.OpenConnectionAsync(tokenCancelamento);
        }

        await using DbCommand comando = conexao.CreateCommand();
        if (contexto.Database.CurrentTransaction is not null)
        {
            comando.Transaction = contexto.Database.CurrentTransaction.GetDbTransaction();
        }

        comando.CommandText = "SELECT current_database(), session_user, current_user, pg_has_role(current_user, current_database() || '_operador', 'MEMBER');";
        await using DbDataReader leitor = await comando.ExecuteReaderAsync(tokenCancelamento);
        if (!await leitor.ReadAsync(tokenCancelamento))
        {
            return ResultadoAutorizacaoOperador.Negado(
                string.Empty,
                "AUTORIZACAO_NEGADA",
                "Não foi possível obter a identidade do usuário da sessão PostgreSQL.");
        }

        string banco = leitor.GetString(0);
        string sessionUser = leitor.GetString(1);
        string currentUser = leitor.GetString(2);
        bool membroGrupoOperador = !leitor.IsDBNull(3) && leitor.GetBoolean(3);

        if (currentUser.EndsWith("_runtime", StringComparison.OrdinalIgnoreCase) ||
            currentUser.EndsWith("_owner", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentUser, banco + "_runtime", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(currentUser, banco + "_owner", StringComparison.OrdinalIgnoreCase))
        {
            return ResultadoAutorizacaoOperador.Negado(
                currentUser,
                "AUTORIZACAO_NEGADA",
                "Papel de runtime ou owner não é autorizado para operações restritas de operador.");
        }

        if (!string.Equals(sessionUser, currentUser, StringComparison.Ordinal))
        {
            return ResultadoAutorizacaoOperador.Negado(
                currentUser,
                "AUTORIZACAO_NEGADA",
                "SET ROLE detectado: session_user diverge de current_user.");
        }

        string? loginEsperado = opcoesOperacao.Value.LoginAutorizado;
        if (string.IsNullOrWhiteSpace(loginEsperado) || !string.Equals(currentUser, loginEsperado, StringComparison.Ordinal))
        {
            return ResultadoAutorizacaoOperador.Negado(
                currentUser,
                "AUTORIZACAO_NEGADA",
                $"O usuário conectado '{currentUser}' não coincide com o login autorizado configurado.");
        }

        if (!membroGrupoOperador)
        {
            return ResultadoAutorizacaoOperador.Negado(
                currentUser,
                "AUTORIZACAO_NEGADA",
                $"O usuário '{currentUser}' não é membro da role de grupo '{banco}_operador'.");
        }

        registroOperador.DefinirOperadorVerificado(currentUser);
        return ResultadoAutorizacaoOperador.Sucesso(currentUser);
    }
}
