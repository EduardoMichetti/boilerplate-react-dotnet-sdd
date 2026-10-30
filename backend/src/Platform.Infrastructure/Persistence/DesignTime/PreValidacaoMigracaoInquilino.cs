namespace Platform.Infrastructure.Persistence.DesignTime;

using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Platform.Domain.Tenancy;
using Platform.Infrastructure.Persistence.TenantAccess;
using Platform.Infrastructure.Persistence.TenantAccess.Entities;

/// <summary>
/// Opções necessárias para a validação prévia de migração (preflight) antes de executar operações DDL.
/// </summary>
public sealed record OpcoesPreValidacaoMigracao(
    string? IdAplicacao,
    Guid IdAmbiente,
    Guid IdInquilino,
    EstadoInquilino? EstadoInquilino,
    string? StringConexao,
    string? PermissaoInicializarBancoVazioBruta,
    Guid? IdInquilinoCli = null,
    Guid? IdAmbienteCli = null);

/// <summary>
/// Representa o estado do marcador de identidade localizado em um banco de dados já existente.
/// </summary>
public sealed record MarcadorBancoExistente(
    bool TabelaExiste,
    bool LinhaExiste,
    Guid IdInquilino,
    Guid IdAmbiente);

/// <summary>
/// Utilitário de pré-validação (preflight) executado antes da aplicação de migrations para garantir o isolamento e a segurança da base.
/// </summary>
public static class PreValidacaoMigracaoInquilino
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
            "Migrations:AllowInitializeEmptyDatabase possui sintaxe booleana inválida.",
            nameof(valorBruto));
    }

    /// <summary>
    /// Valida as opções informadas para a pré-validação antes de tentar abrir qualquer conexão.
    /// </summary>
    public static void ValidarOpcoes(OpcoesPreValidacaoMigracao opcoes)
    {
        ArgumentNullException.ThrowIfNull(opcoes);

        if (string.IsNullOrWhiteSpace(opcoes.IdAplicacao) || opcoes.IdAplicacao.Length > 100)
        {
            throw new ArgumentException("IdAplicacao deve conter entre 1 e 100 caracteres.", nameof(opcoes));
        }

        if (opcoes.IdAmbiente == Guid.Empty)
        {
            throw new ArgumentException("IdAmbiente deve ser um UUID não vazio.", nameof(opcoes));
        }

        if (opcoes.IdInquilino == Guid.Empty)
        {
            throw new ArgumentException("IdInquilino deve ser um UUID não vazio.", nameof(opcoes));
        }

        if (opcoes.EstadoInquilino != EstadoInquilino.Inativo)
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

        if (opcoes.IdInquilinoCli.HasValue && opcoes.IdInquilinoCli.Value != opcoes.IdInquilino)
        {
            throw new InvalidOperationException(
                $"O argumento informado na CLI ({opcoes.IdInquilinoCli}) diverge do IdInquilino configurado ({opcoes.IdInquilino}).");
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
    public static void AvaliarEstadoBanco(
        OpcoesPreValidacaoMigracao opcoes,
        int totalTabelasUsuario,
        MarcadorBancoExistente? marcador)
    {
        ValidarOpcoes(opcoes);
        bool permiteInicializar = AnalisarPermissaoInicializacao(opcoes.PermissaoInicializarBancoVazioBruta);

        if (totalTabelasUsuario == 0)
        {
            if (!permiteInicializar)
            {
                throw new InvalidOperationException(
                    "Inicialização de banco vazio não autorizada antes de DDL. " +
                    "Configure Migrations:AllowInitializeEmptyDatabase=true para autorizar.");
            }

            return;
        }

        if (marcador is null || !marcador.TabelaExiste || !marcador.LinhaExiste)
        {
            throw new InvalidOperationException(
                "Estrutura existente sem marcador IdentidadeBancoInquilino. Operação bloqueada antes de DDL.");
        }

        if (marcador.IdInquilino != opcoes.IdInquilino || marcador.IdAmbiente != opcoes.IdAmbiente)
        {
            throw new ExcecaoIsolamentoInquilino();
        }
    }

    /// <summary>
    /// Conecta ao banco de dados e executa todas as verificações prévias de segurança e isolamento.
    /// </summary>
    public static async Task ExecutarPreValidacaoAsync(
        OpcoesPreValidacaoMigracao opcoes,
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
                    "SELECT \"IdInquilino\", \"IdAmbiente\" FROM \"foundation\".\"identidade_banco\" WHERE \"IdUnico\" = 1",
                    conexao);
                await using NpgsqlDataReader leitor = await lerMarcador.ExecuteReaderAsync(tokenCancelamento);
                if (await leitor.ReadAsync(tokenCancelamento))
                {
                    marcador = new MarcadorBancoExistente(
                        TabelaExiste: true,
                        LinhaExiste: true,
                        IdInquilino: leitor.GetGuid(0),
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

        AvaliarEstadoBanco(opcoes, totalTabelasUsuario, marcador);
    }

    /// <summary>
    /// Realiza a inserção síncrona do registro de identidade do banco de dados na base.
    /// </summary>
    public static void PopularIdentidadeBanco(
        ContextoBancoAcessoInquilino contexto,
        Guid idInquilino,
        Guid idAmbiente)
    {
        IdentidadeBancoInquilino? existente = contexto.IdentidadeBanco
            .FirstOrDefault(e => e.IdUnico == IdentidadeBancoInquilino.IdUnicoPadrao);

        if (existente is null)
        {
            contexto.IdentidadeBanco.Add(new IdentidadeBancoInquilino
            {
                IdUnico = IdentidadeBancoInquilino.IdUnicoPadrao,
                IdInquilino = idInquilino,
                IdAmbiente = idAmbiente,
                EpocaAcesso = Guid.NewGuid(),
                InicializadoEm = DateTimeOffset.UtcNow
            });
            contexto.SaveChanges();
        }
        else
        {
            if (existente.IdInquilino != idInquilino || existente.IdAmbiente != idAmbiente)
            {
                throw new ExcecaoIsolamentoInquilino();
            }
        }
    }

    /// <summary>
    /// Realiza a inserção assíncrona do registro de identidade do banco de dados na base.
    /// </summary>
    public static async Task PopularIdentidadeBancoAsync(
        ContextoBancoAcessoInquilino contexto,
        Guid idInquilino,
        Guid idAmbiente,
        CancellationToken tokenCancelamento = default)
    {
        IdentidadeBancoInquilino? existente = await contexto.IdentidadeBanco
            .FirstOrDefaultAsync(e => e.IdUnico == IdentidadeBancoInquilino.IdUnicoPadrao, tokenCancelamento);

        if (existente is null)
        {
            contexto.IdentidadeBanco.Add(new IdentidadeBancoInquilino
            {
                IdUnico = IdentidadeBancoInquilino.IdUnicoPadrao,
                IdInquilino = idInquilino,
                IdAmbiente = idAmbiente,
                EpocaAcesso = Guid.NewGuid(),
                InicializadoEm = DateTimeOffset.UtcNow
            });
            await contexto.SaveChangesAsync(tokenCancelamento);
        }
        else
        {
            if (existente.IdInquilino != idInquilino || existente.IdAmbiente != idAmbiente)
            {
                throw new ExcecaoIsolamentoInquilino();
            }
        }
    }
}
