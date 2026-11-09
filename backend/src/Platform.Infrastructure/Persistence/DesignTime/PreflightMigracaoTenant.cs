namespace Platform.Infrastructure.Persistence.DesignTime;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Domain.Identificadores;
using Platform.Domain.Tempo;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Opções necessárias para a validação prévia de migração (preflight) antes de executar operações DDL.
/// </summary>
public sealed record OpcoesPreflightMigracao(
    string? CodigoAplicacao,
    Guid IdAmbiente,
    Guid IdTenant,
    EstadoTenant? EstadoTenant,
    string? StringConexao,
    string? PermissaoInicializarBancoVazioBruta,
    Guid? IdTenantCli = null,
    Guid? IdAmbienteCli = null);

/// <summary>
/// Representa o estado do marcador de identidade localizado em um banco de dados já existente.
/// </summary>
public sealed record MarcadorBancoExistente(
    bool TabelaExiste,
    bool LinhaExiste,
    Guid IdTenant,
    Guid IdAmbiente);

/// <summary>
/// Utilitário de pré-validação (preflight) executado antes da aplicação de migrations para garantir o isolamento e a segurança da base do tenant.
/// </summary>
public static class PreflightMigracaoTenant
{
    /// <summary>
    /// Converte o valor textual da flag de autorização para inicialização de banco de dados vazio.
    /// </summary>
    public static bool AnalisarPermissaoInicializacao(string? valorBruto)
    {
        if (string.IsNullOrWhiteSpace(valorBruto))
        {
            return false;
        }

        if (bool.TryParse(valorBruto, out bool resultado))
        {
            return resultado;
        }

        throw new ArgumentException(
            "Migracoes:PermitirInicializarBancoVazio possui sintaxe booleana inválida.",
            nameof(valorBruto));
    }

    /// <summary>
    /// Valida as opções informadas para o preflight antes de tentar abrir qualquer conexão.
    /// </summary>
    public static void ValidarOpcoes(OpcoesPreflightMigracao opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        if (string.IsNullOrWhiteSpace(opcoes.CodigoAplicacao) || opcoes.CodigoAplicacao.Length > 100)
        {
            throw new ArgumentException("CodigoAplicacao deve conter entre 1 e 100 caracteres.", nameof(opcoes));
        }

        if (opcoes.IdAmbiente == Guid.Empty)
        {
            throw new ArgumentException("IdAmbiente deve ser um UUID não vazio.", nameof(opcoes));
        }

        if (opcoes.IdTenant == Guid.Empty)
        {
            throw new ArgumentException("IdTenant deve ser um UUID não vazio.", nameof(opcoes));
        }

        if (opcoes.EstadoTenant != EstadoTenant.Inativo)
        {
            throw new InvalidOperationException(
                "A instalação deve estar em estado Inativo (janela de manutenção) para aplicar migrations.");
        }

        if (string.IsNullOrWhiteSpace(opcoes.StringConexao))
        {
            throw new ArgumentException("A string de conexão é obrigatória.", nameof(opcoes));
        }

        try
        {
            _ = new DbConnectionStringBuilder { ConnectionString = opcoes.StringConexao };
        }
        catch (ArgumentException ex)
        {
            throw new ArgumentException("A string de conexão possui sintaxe inválida.", nameof(opcoes), ex);
        }

        if (opcoes.IdTenantCli.HasValue && opcoes.IdTenantCli.Value != opcoes.IdTenant)
        {
            throw new InvalidOperationException(
                $"O argumento informado na CLI ({opcoes.IdTenantCli}) diverge do IdTenant configurado ({opcoes.IdTenant}).");
        }

        if (opcoes.IdAmbienteCli.HasValue && opcoes.IdAmbienteCli.Value != opcoes.IdAmbiente)
        {
            throw new InvalidOperationException(
                $"O argumento informado na CLI ({opcoes.IdAmbienteCli}) diverge do IdAmbiente configurado ({opcoes.IdAmbiente}).");
        }

        _ = AnalisarPermissaoInicializacao(opcoes.PermissaoInicializarBancoVazioBruta);
    }

    /// <summary>
    /// Avalia se o estado atual do banco de dados permite ou bloqueia a execução da migration.
    /// </summary>
    /// <param name="opcoes">Opções do preflight.</param>
    /// <param name="totalTabelasUsuario">Quantidade de tabelas de usuário existentes.</param>
    /// <param name="marcador">Marcador lido, quando há estrutura.</param>
    /// <param name="historicoAplicado">IDs em <c>foundation.__EFMigrationsHistory</c>, na ordem aplicada.</param>
    /// <param name="cadeiaConhecida">IDs das migrations conhecidas pelo binário, em ordem.</param>
    public static void AvaliarEstadoBanco(
        OpcoesPreflightMigracao opcoes,
        int totalTabelasUsuario,
        MarcadorBancoExistente? marcador,
        IReadOnlyList<string>? historicoAplicado = null,
        IReadOnlyList<string>? cadeiaConhecida = null)
    {
        ValidarOpcoes(opcoes);
        ValidarHistoricoComoPrefixo(historicoAplicado ?? [], cadeiaConhecida);
        bool permiteInicializar = AnalisarPermissaoInicializacao(opcoes.PermissaoInicializarBancoVazioBruta);

        if (totalTabelasUsuario == 0)
        {
            if (!permiteInicializar)
            {
                throw new InvalidOperationException(
                    "Inicialização de banco vazio não autorizada antes de DDL. " +
                    "Configure Migracoes:PermitirInicializarBancoVazio=true para autorizar.");
            }

            return;
        }

        if (marcador is null || !marcador.TabelaExiste || !marcador.LinhaExiste)
        {
            throw new InvalidOperationException(
                "Estrutura existente sem marcador IdentidadeBancoTenant. Operação bloqueada antes de DDL.");
        }

        if (marcador.IdTenant != opcoes.IdTenant || marcador.IdAmbiente != opcoes.IdAmbiente)
        {
            throw new ExcecaoIsolamentoTenant();
        }
    }

    /// <summary>
    /// Exige que o histórico aplicado seja prefixo válido da cadeia conhecida: qualquer ID
    /// desconhecido (banco de versão futura ou de outra linha) bloqueia antes de DDL, mesmo
    /// sem migrations pendentes.
    /// </summary>
    /// <param name="historicoAplicado">IDs aplicados, em ordem.</param>
    /// <param name="cadeiaConhecida">IDs conhecidos, em ordem; nulo dispensa a verificação (sem cadeia disponível).</param>
    /// <exception cref="InvalidOperationException">Histórico fora da cadeia conhecida.</exception>
    public static void ValidarHistoricoComoPrefixo(IReadOnlyList<string> historicoAplicado, IReadOnlyList<string>? cadeiaConhecida)
    {
        ArgumentNullException.ThrowIfNull(historicoAplicado);
        if (cadeiaConhecida is null)
        {
            return;
        }

        if (historicoAplicado.Count > cadeiaConhecida.Count)
        {
            throw new InvalidOperationException(
                "Histórico de migrations contém IDs desconhecidos por esta versão. Operação bloqueada antes de DDL.");
        }

        for (int i = 0; i < historicoAplicado.Count; i++)
        {
            if (!string.Equals(historicoAplicado[i], cadeiaConhecida[i], StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Histórico de migrations não é prefixo da cadeia conhecida. Operação bloqueada antes de DDL.");
            }
        }
    }

    /// <summary>
    /// Conecta ao banco de dados e executa todas as verificações prévias de segurança e isolamento.
    /// </summary>
    /// <param name="opcoes">Opções do preflight.</param>
    /// <param name="cadeiaConhecida">IDs das migrations conhecidas pelo binário, em ordem.</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    public static async Task ExecutarPreflightAsync(
        OpcoesPreflightMigracao opcoes,
        IReadOnlyList<string> cadeiaConhecida,
        CancellationToken tokenCancelamento = default)
    {
        ValidarOpcoes(opcoes);

        await using NpgsqlConnection conexao = new(opcoes.StringConexao);
        await conexao.OpenAsync(tokenCancelamento);

        await using (NpgsqlCommand checarPrivilegios = new(
            "SELECT rolsuper FROM pg_roles WHERE rolname = current_user", conexao))
        {
            object? ehSuperusuario = await checarPrivilegios.ExecuteScalarAsync(tokenCancelamento);
            if (ehSuperusuario is true)
            {
                throw new InvalidOperationException("A ferramenta de migration não deve ser executada como superusuário.");
            }
        }

        int totalTabelasUsuario;
        await using (NpgsqlCommand contarTabelas = new(
            "SELECT count(*) FROM information_schema.tables " +
            "WHERE table_schema NOT IN ('pg_catalog', 'information_schema')", conexao))
        {
            totalTabelasUsuario = Convert.ToInt32(await contarTabelas.ExecuteScalarAsync(tokenCancelamento));
        }

        MarcadorBancoExistente? marcador = null;
        if (totalTabelasUsuario > 0)
        {
            bool tabelaMarcadorExiste;
            await using (NpgsqlCommand checarTabelaMarcador = new(
                "SELECT count(*) FROM information_schema.tables " +
                "WHERE table_schema = 'foundation' AND table_name = 'identidade_banco'", conexao))
            {
                tabelaMarcadorExiste = Convert.ToInt32(await checarTabelaMarcador.ExecuteScalarAsync(tokenCancelamento)) > 0;
            }

            if (tabelaMarcadorExiste)
            {
                await using NpgsqlCommand lerMarcador = new(
                    "SELECT \"IdTenant\", \"IdAmbiente\" FROM \"foundation\".\"identidade_banco\" WHERE \"IdUnico\" = 1",
                    conexao);
                await using NpgsqlDataReader leitor = await lerMarcador.ExecuteReaderAsync(tokenCancelamento);
                if (await leitor.ReadAsync(tokenCancelamento))
                {
                    marcador = new MarcadorBancoExistente(
                        TabelaExiste: true,
                        LinhaExiste: true,
                        IdTenant: leitor.GetGuid(0),
                        IdAmbiente: leitor.GetGuid(1));
                }
                else
                {
                    marcador = new MarcadorBancoExistente(TabelaExiste: true, LinhaExiste: false, Guid.Empty, Guid.Empty);
                }
            }
            else
            {
                marcador = new MarcadorBancoExistente(TabelaExiste: false, LinhaExiste: false, Guid.Empty, Guid.Empty);
            }
        }

        List<string> historicoAplicado = [];
        bool historicoExiste;
        await using (NpgsqlCommand checarHistorico = new(
            "SELECT count(*) FROM information_schema.tables " +
            "WHERE table_schema = 'foundation' AND table_name = '__EFMigrationsHistory'", conexao))
        {
            historicoExiste = Convert.ToInt32(await checarHistorico.ExecuteScalarAsync(tokenCancelamento)) > 0;
        }

        if (historicoExiste)
        {
            await using NpgsqlCommand lerHistorico = new(
                "SELECT \"MigrationId\" FROM \"foundation\".\"__EFMigrationsHistory\" ORDER BY \"MigrationId\"", conexao);
            await using NpgsqlDataReader leitor = await lerHistorico.ExecuteReaderAsync(tokenCancelamento);
            while (await leitor.ReadAsync(tokenCancelamento))
            {
                historicoAplicado.Add(leitor.GetString(0));
            }
        }

        AvaliarEstadoBanco(opcoes, totalTabelasUsuario, marcador, historicoAplicado, cadeiaConhecida);
    }

    /// <summary>
    /// Realiza a inserção síncrona do registro de identidade do banco de dados na base.
    /// </summary>
    /// <param name="contexto">Contexto do banco alvo.</param>
    /// <param name="idTenant">Tenant configurado.</param>
    /// <param name="idAmbiente">Ambiente configurado.</param>
    /// <param name="relogio">Fonte do instante (TimeProvider do tooling).</param>
    public static void PopularIdentidadeBanco(
        AcessoTenantDbContext contexto,
        Guid idTenant,
        Guid idAmbiente,
        TimeProvider relogio)
    {
        IdentidadeBancoTenant? existente = contexto.IdentidadeBanco
            .FirstOrDefault(e => e.IdUnico == IdentidadeBancoTenant.IdUnicoPadrao);

        if (existente is null)
        {
            contexto.IdentidadeBanco.Add(CriarMarcador(idTenant, idAmbiente, relogio));
            contexto.SaveChanges();
        }
        else
        {
            if (existente.IdTenant != idTenant || existente.IdAmbiente != idAmbiente)
            {
                throw new ExcecaoIsolamentoTenant();
            }
        }
    }

    /// <summary>
    /// Realiza a inserção assíncrona do registro de identidade do banco de dados na base.
    /// </summary>
    /// <param name="contexto">Contexto do banco alvo.</param>
    /// <param name="idTenant">Tenant configurado.</param>
    /// <param name="idAmbiente">Ambiente configurado.</param>
    /// <param name="relogio">Fonte do instante (TimeProvider do tooling).</param>
    /// <param name="tokenCancelamento">Token de cancelamento.</param>
    public static async Task PopularIdentidadeBancoAsync(
        AcessoTenantDbContext contexto,
        Guid idTenant,
        Guid idAmbiente,
        TimeProvider relogio,
        CancellationToken tokenCancelamento = default)
    {
        IdentidadeBancoTenant? existente = await contexto.IdentidadeBanco
            .FirstOrDefaultAsync(e => e.IdUnico == IdentidadeBancoTenant.IdUnicoPadrao, tokenCancelamento);

        if (existente is null)
        {
            contexto.IdentidadeBanco.Add(CriarMarcador(idTenant, idAmbiente, relogio));
            await contexto.SaveChangesAsync(tokenCancelamento);
        }
        else
        {
            if (existente.IdTenant != idTenant || existente.IdAmbiente != idAmbiente)
            {
                throw new ExcecaoIsolamentoTenant();
            }
        }
    }

    private static IdentidadeBancoTenant CriarMarcador(Guid idTenant, Guid idAmbiente, TimeProvider relogio)
    {
        ArgumentNullException.ThrowIfNull(relogio);
        DateTimeOffset agora = InstanteUtc.Normalizar(relogio.GetUtcNow());
        IdentidadeBancoTenant marcador = new()
        {
            IdUnico = IdentidadeBancoTenant.IdUnicoPadrao,
            IdTenant = idTenant,
            IdAmbiente = idAmbiente,
            EpocaAcesso = IdentificadorV7.Gerar(agora),
            InicializadoEm = agora
        };
        marcador.Validar();
        return marcador;
    }
}
